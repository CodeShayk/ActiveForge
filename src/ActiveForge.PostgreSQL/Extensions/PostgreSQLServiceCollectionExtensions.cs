using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ActiveForge.Transactions;

namespace ActiveForge
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering ActiveForge ORM
    /// with a PostgreSQL provider.
    /// </summary>
    public static class PostgreSQLServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ActiveForge ORM services for PostgreSQL and returns an
        /// <see cref="IActiveForgeBuilder"/> for chaining service proxy registrations.
        /// <list type="bullet">
        ///   <item><description>Scoped <see cref="PostgreSQLConnection"/> — one instance per DI scope.</description></item>
        ///   <item><description>Scoped <see cref="DataConnection"/> — forwards to the scoped <see cref="PostgreSQLConnection"/>.</description></item>
        ///   <item><description>Scoped <see cref="IUnitOfWork"/> — a <c>PostgreSQLUnitOfWork</c> wrapping the scoped connection.</description></item>
        /// </list>
        /// Chain <c>.AddServices(typeof(MyApp).Assembly)</c> on the returned builder to
        /// automatically discover and register all <see cref="IService"/> implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">PostgreSQL connection string.</param>
        /// <param name="factory">Optional polymorphic type factory.</param>
        /// <returns>An <see cref="IActiveForgeBuilder"/> for further configuration.</returns>
        public static IActiveForgeBuilder AddActiveForgePostgreSQL(
            this IServiceCollection services,
            string connectionString,
            BaseFactory factory = null)
        {
            if (services         == null) throw new ArgumentNullException(nameof(services));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            services.AddScoped<PostgreSQLConnection>(_ =>
                factory != null
                    ? new PostgreSQLConnection(connectionString, factory)
                    : new PostgreSQLConnection(connectionString));

            services.AddScoped<DataConnection>(sp => sp.GetRequiredService<PostgreSQLConnection>());

            services.AddScoped<IUnitOfWork>(sp =>
            {
                var conn   = sp.GetRequiredService<PostgreSQLConnection>();
                var logger = sp.GetService<ILogger<PostgreSQLUnitOfWork>>();
                return new PostgreSQLUnitOfWork(conn, logger);
            });

            return new ActiveForgeBuilder(services);
        }
    }
}
