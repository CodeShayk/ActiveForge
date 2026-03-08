using System;
using System.Collections.Generic;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// Creates Castle DynamicProxy wrappers around application service classes, stacking the
    /// connection-lifecycle and transaction interceptors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Interface types</b> (<c>TService</c> is an interface) — uses
    /// <c>CreateInterfaceProxyWithTarget</c>.  The concrete class does not need to be non-sealed
    /// or have virtual methods.  Attributes (<c>[ConnectionScope]</c>, <c>[Transaction]</c>)
    /// must be placed on the concrete implementation methods; the interceptors always check the
    /// implementation method (<c>IInvocation.MethodInvocationTarget</c>) first.
    /// </para>
    /// <para>
    /// <b>Class types</b> (<c>TService</c> is a concrete class) — uses
    /// <c>CreateClassProxyWithTarget</c>.  The class must be non-sealed and intercepted methods
    /// must be <c>virtual</c>.
    /// </para>
    /// <para>
    /// Interceptor order: <see cref="ConnectionScopeInterceptor"/> (outer) →
    /// <see cref="TransactionInterceptor"/> (inner).  The connection is opened first and closed
    /// last; the transaction always runs against an open connection.
    /// </para>
    /// </remarks>
    public static class TurquoiseServiceFactory
    {
        private static readonly ProxyGenerator _generator = new ProxyGenerator();

        // ── Generic typed overload ────────────────────────────────────────────────────

        /// <summary>
        /// Returns a proxy of <paramref name="instance"/>.
        /// When <typeparamref name="TService"/> is an interface, an interface proxy is created
        /// (no virtual-method requirement on the implementation).
        /// When it is a class, a class proxy is created (class must be non-sealed, methods virtual).
        /// </summary>
        public static TService Create<TService>(
            TService       instance,
            DataConnection connection,
            IUnitOfWork    unitOfWork = null,
            ILogger        logger     = null)
            where TService : class
        {
            if (instance   == null) throw new ArgumentNullException(nameof(instance));
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            return (TService)Create(typeof(TService), instance, connection, unitOfWork, logger);
        }

        // ── Non-generic overload (used by auto-scan registration) ─────────────────────

        /// <summary>
        /// Returns a proxy registered against <paramref name="serviceType"/>.
        /// Dispatches to interface proxy or class proxy based on whether
        /// <paramref name="serviceType"/> is an interface.
        /// </summary>
        public static object Create(
            Type           serviceType,
            object         instance,
            DataConnection connection,
            IUnitOfWork    unitOfWork = null,
            ILogger        logger     = null)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance    == null) throw new ArgumentNullException(nameof(instance));
            if (connection  == null) throw new ArgumentNullException(nameof(connection));

            var interceptors = BuildInterceptors(connection, unitOfWork, logger);

            if (serviceType.IsInterface)
            {
                // Interface proxy — implementation does not need virtual methods.
                // MethodInvocationTarget on IInvocation will point to the concrete method
                // so attribute resolution in the interceptors finds [ConnectionScope] /
                // [Transaction] even though they are on the implementation, not the interface.
                return _generator.CreateInterfaceProxyWithTarget(
                    serviceType,
                    instance,
                    interceptors);
            }

            // Class proxy — class must be non-sealed, intercepted methods must be virtual.
            return _generator.CreateClassProxyWithTarget(
                serviceType,
                instance,
                interceptors);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static IInterceptor[] BuildInterceptors(
            DataConnection connection,
            IUnitOfWork    unitOfWork,
            ILogger        logger)
        {
            var list = new List<IInterceptor>
            {
                new ConnectionScopeInterceptor(connection, logger)
            };

            if (unitOfWork != null)
                list.Add(new TransactionInterceptor(unitOfWork));

            return list.ToArray();
        }
    }
}
