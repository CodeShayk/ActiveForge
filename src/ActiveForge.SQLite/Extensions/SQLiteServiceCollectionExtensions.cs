using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ActiveForge.Transactions;

namespace ActiveForge
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering ActiveForge ORM
    /// with a SQLite provider.
    /// </summary>
    public static class SQLiteServiceCollectionExtensions
    {
        /// <summary>
        /// Registers ActiveForge ORM services for SQLite and returns an
        /// <see cref="IActiveForgeBuilder"/> for chaining service proxy registrations.
        /// <list type="bullet">
        ///   <item><description>Scoped <see cref="SQLiteConnection"/> — one instance per DI scope.</description></item>
        ///   <item><description>Scoped <see cref="DataConnection"/> — forwards to the scoped <see cref="SQLiteConnection"/>.</description></item>
        ///   <item><description>Scoped <see cref="IUnitOfWork"/> — a <c>SQLiteUnitOfWork</c> wrapping the scoped connection.</description></item>
        /// </list>
        /// Chain <c>.AddServices(typeof(MyApp).Assembly)</c> on the returned builder to
        /// automatically discover and register all <see cref="IService"/> implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">SQLite connection string (e.g. <c>Data Source=app.db</c>).</param>
        /// <param name="factory">Optional polymorphic type factory.</param>
        /// <returns>An <see cref="IActiveForgeBuilder"/> for further configuration.</returns>
        public static IActiveForgeBuilder AddActiveForgeSQLite(
            this IServiceCollection services,
            string connectionString,
            BaseFactory factory = null)
        {
            if (services         == null) throw new ArgumentNullException(nameof(services));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            services.AddScoped<SQLiteConnection>(_ =>
                factory != null
                    ? new SQLiteConnection(connectionString, factory)
                    : new SQLiteConnection(connectionString));

            services.AddScoped<DataConnection>(sp => sp.GetRequiredService<SQLiteConnection>());

            services.AddScoped<IUnitOfWork>(sp =>
            {
                var conn   = sp.GetRequiredService<SQLiteConnection>();
                var logger = sp.GetService<ILogger<SQLiteUnitOfWork>>();
                return new SQLiteUnitOfWork(conn, logger);
            });

            return new ActiveForgeBuilder(services);
        }
    }
}
