using System;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Creates Castle DynamicProxy proxies around <see cref="DataConnection"/> subclasses so
    /// that methods decorated with <see cref="TransactionAttribute"/> are automatically wrapped
    /// in a database transaction managed by an <see cref="IUnitOfWork"/>.
    ///
    /// <para>
    /// Strategy C1 (default): the proxy wraps <see cref="DataConnection"/> — no changes to
    /// <see cref="Record"/> are required. The concrete connection type must be non-sealed
    /// and its CRUD methods must be <c>virtual</c> (which they are in <see cref="DBDataConnection"/>
    /// because they <c>override</c> the abstract declarations in <see cref="DataConnection"/>).
    /// </para>
    ///
    /// <example>
    /// <code>
    /// var uow  = new SqlServerUnitOfWork(conn);
    /// var proxy = DataConnectionProxyFactory.Create(conn, uow);
    ///
    /// // proxy.Insert / Delete / Update / Read will now be wrapped in transactions
    /// // when the target method carries [Transaction].
    /// </code>
    /// </example>
    /// </summary>
    public static class DataConnectionProxyFactory
    {
        private static readonly ProxyGenerator _generator = new ProxyGenerator();

        /// <summary>
        /// Returns a proxy of <paramref name="connection"/> that intercepts calls to methods
        /// decorated with <see cref="TransactionAttribute"/>.
        /// </summary>
        /// <typeparam name="TConnection">
        /// A concrete, non-sealed subclass of <see cref="DataConnection"/> whose CRUD methods
        /// are virtual (e.g. <c>SqlServerConnection</c> from <c>ActiveForge.SqlServer</c>).
        /// </typeparam>
        /// <param name="connection">The real connection instance to wrap.</param>
        /// <param name="unitOfWork">The unit-of-work that manages the transaction lifecycle.</param>
        /// <param name="logger">Optional logger forwarded to the interceptor.</param>
        public static TConnection Create<TConnection>(
            TConnection  connection,
            IUnitOfWork  unitOfWork,
            ILogger<TransactionInterceptor> logger = null)
            where TConnection : DataConnection
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));

            var interceptor = new TransactionInterceptor(unitOfWork, logger);

            // Castle intercepts virtual members on a class-proxy; the real target
            // (connection) provides state through constructor arguments.
            return (TConnection)_generator.CreateClassProxyWithTarget(
                typeof(TConnection),
                connection,
                interceptor);
        }

        /// <summary>
        /// Non-generic overload — returns a <see cref="DataConnection"/> reference.
        /// Use when the concrete type is not statically known.
        /// </summary>
        public static DataConnection Create(
            DataConnection connection,
            IUnitOfWork    unitOfWork,
            ILogger<TransactionInterceptor> logger = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));

            var interceptor = new TransactionInterceptor(unitOfWork, logger);
            Type targetType = connection.GetType();

            return (DataConnection)_generator.CreateClassProxyWithTarget(
                targetType,
                connection,
                interceptor);
        }
    }
}
