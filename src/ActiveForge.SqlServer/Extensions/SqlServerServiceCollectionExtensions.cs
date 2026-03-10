using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ActiveForge.Transactions;

namespace ActiveForge
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering Turquoise ORM
    /// with a SQL Server provider.
    /// </summary>
    public static class SqlServerServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Turquoise ORM services for SQL Server and returns an
        /// <see cref="IActiveForgeBuilder"/> for chaining service proxy registrations.
        /// <list type="bullet">
        ///   <item><description>Scoped <see cref="SqlServerConnection"/> — one instance per DI scope.</description></item>
        ///   <item><description>Scoped <see cref="DataConnection"/> — forwards to the scoped <see cref="SqlServerConnection"/>.</description></item>
        ///   <item><description>Scoped <see cref="IUnitOfWork"/> — a <c>SqlServerUnitOfWork</c> wrapping the scoped connection.</description></item>
        /// </list>
        /// Chain <c>.AddServices(typeof(MyApp).Assembly)</c> on the returned builder to
        /// automatically discover and register all <see cref="IService"/> implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">SQL Server connection string.</param>
        /// <param name="factory">Optional polymorphic type factory.</param>
        /// <returns>An <see cref="IActiveForgeBuilder"/> for further configuration.</returns>
        public static IActiveForgeBuilder AddActiveForgeSqlServer(
            this IServiceCollection services,
            string connectionString,
            FactoryBase factory = null)
        {
            if (services         == null) throw new ArgumentNullException(nameof(services));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            services.AddScoped<SqlServerConnection>(_ =>
                factory != null
                    ? new SqlServerConnection(connectionString, factory)
                    : new SqlServerConnection(connectionString));

            services.AddScoped<DataConnection>(sp => sp.GetRequiredService<SqlServerConnection>());

            services.AddScoped<IUnitOfWork>(sp =>
            {
                var conn   = sp.GetRequiredService<SqlServerConnection>();
                var logger = sp.GetService<ILogger<SqlServerUnitOfWork>>();
                return new SqlServerUnitOfWork(conn, logger);
            });

            return new ActiveForgeBuilder(services);
        }
    }
}
