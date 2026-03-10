using System;
using System.Data;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Castle DynamicProxy interceptor that automatically wraps methods decorated with
    /// <see cref="TransactionAttribute"/> (or methods on classes decorated with it) in a
    /// database transaction managed by <see cref="IUnitOfWork"/>.
    ///
    /// <para>
    /// If a transaction is already active (InTransaction == true) the method enlists in the
    /// ambient transaction instead of starting a new one; the depth counter in
    /// <see cref="UnitOfWorkBase"/> handles the nesting safely.
    /// </para>
    /// <para>
    /// <b>Interface proxy support:</b> attribute resolution checks
    /// <c>IInvocation.MethodInvocationTarget</c> (the concrete implementation method) first,
    /// then the interface method.  Place <c>[Transaction]</c> on the implementing class or
    /// the interface — both are detected correctly.
    /// </para>
    /// </summary>
    public class TransactionInterceptor : IInterceptor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger     _logger;

        public TransactionInterceptor(IUnitOfWork unitOfWork, ILogger<TransactionInterceptor> logger = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger     = (ILogger)logger ?? NullLogger.Instance;
        }

        public void Intercept(IInvocation invocation)
        {
            TransactionAttribute attr = GetTransactionAttribute(invocation);

            if (attr == null)
            {
                // No [Transaction] — if already in a transaction enlist, otherwise pass through.
                invocation.Proceed();
                return;
            }

            IsolationLevel level = attr.IsolationLevel;
            _logger.LogDebug("TransactionInterceptor: starting transaction for {Method} (isolation={Level})",
                invocation.Method.Name, level);

            _unitOfWork.CreateTransaction(level);
            try
            {
                invocation.Proceed();
                _unitOfWork.Commit();
                _logger.LogDebug("TransactionInterceptor: committed for {Method}", invocation.Method.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionInterceptor: rolling back for {Method}", invocation.Method.Name);
                _unitOfWork.Rollback();
                throw;
            }
        }

        // ── Attribute resolution ─────────────────────────────────────────────────────

        private static TransactionAttribute GetTransactionAttribute(IInvocation invocation)
        {
            // Prefer the concrete implementation method so that attributes placed on the
            // implementing class (not the interface) are found correctly for interface proxies.
            var implMethod = invocation.MethodInvocationTarget ?? invocation.Method;

            var attr = implMethod.GetCustomAttribute<TransactionAttribute>(inherit: true);
            if (attr != null) return attr;

            attr = implMethod.DeclaringType?.GetCustomAttribute<TransactionAttribute>(inherit: true);
            if (attr != null) return attr;

            // Fall back to the interface/proxy method.
            if (implMethod != invocation.Method)
            {
                attr = invocation.Method.GetCustomAttribute<TransactionAttribute>(inherit: true);
                if (attr != null) return attr;

                attr = invocation.Method.DeclaringType?
                                 .GetCustomAttribute<TransactionAttribute>(inherit: true);
                if (attr != null) return attr;
            }

            return null;
        }
    }
}
