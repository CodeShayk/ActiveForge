using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ActiveForge.Transactions;

namespace ActiveForge
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering Turquoise ORM
    /// with a MongoDB provider.
    /// </summary>
    public static class MongoServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Turquoise ORM services for MongoDB and returns an
        /// <see cref="IActiveForgeBuilder"/> for chaining service proxy registrations.
        /// <list type="bullet">
        ///   <item><description>Singleton <see cref="MongoClient"/> — owns the connection pool.</description></item>
        ///   <item><description>Scoped <see cref="MongoDataConnection"/> — one per DI scope, backed by the singleton client.</description></item>
        ///   <item><description>Scoped <see cref="DataConnection"/> — forwards to the scoped <see cref="MongoDataConnection"/>.</description></item>
        ///   <item><description>Scoped <see cref="IUnitOfWork"/> — a <c>MongoUnitOfWork</c> wrapping the scoped connection.</description></item>
        /// </list>
        /// Chain <c>.AddServices(typeof(MyApp).Assembly)</c> on the returned builder to
        /// automatically discover and register all <see cref="IService"/> implementations.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="connectionString">MongoDB connection string.</param>
        /// <param name="databaseName">Name of the MongoDB database to target.</param>
        /// <param name="factory">Optional polymorphic type factory.</param>
        /// <returns>An <see cref="IActiveForgeBuilder"/> for further configuration.</returns>
        /// <remarks>
        /// MongoDB multi-document transactions require a replica set or sharded cluster.
        /// </remarks>
        public static IActiveForgeBuilder AddActiveForgeMongoDB(
            this IServiceCollection services,
            string connectionString,
            string databaseName,
            FactoryBase factory = null)
        {
            if (services         == null) throw new ArgumentNullException(nameof(services));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            if (databaseName     == null) throw new ArgumentNullException(nameof(databaseName));

            services.TryAddSingleton<MongoClient>(_ => new MongoClient(connectionString));

            services.AddScoped<MongoDataConnection>(sp =>
            {
                var client = sp.GetRequiredService<MongoClient>();
                var logger = sp.GetService<ILogger<MongoDataConnection>>();
                return new MongoDataConnection(client, databaseName, factory, logger);
            });

            services.AddScoped<DataConnection>(sp => sp.GetRequiredService<MongoDataConnection>());

            services.AddScoped<IUnitOfWork>(sp =>
            {
                var conn   = sp.GetRequiredService<MongoDataConnection>();
                var logger = sp.GetService<ILogger<MongoUnitOfWork>>();
                return new MongoUnitOfWork(conn, logger);
            });

            return new ActiveForgeBuilder(services);
        }
    }
}
