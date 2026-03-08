using System;
using System.Data;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Turquoise.ORM.Transactions
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
            TransactionAttribute attr = GetTransactionAttribute(invocation.Method);

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

        private static TransactionAttribute GetTransactionAttribute(MethodInfo method)
        {
            // Method-level attribute takes precedence.
            var methodAttr = method.GetCustomAttribute<TransactionAttribute>(inherit: true);
            if (methodAttr != null) return methodAttr;

            // Fall back to class-level attribute.
            return method.DeclaringType?.GetCustomAttribute<TransactionAttribute>(inherit: true);
        }
    }
}
