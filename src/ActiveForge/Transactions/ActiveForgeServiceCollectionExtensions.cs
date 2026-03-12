using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extension methods for registering individual ActiveForge
    /// ORM service proxies.  For bulk auto-registration use
    /// <see cref="IActiveForgeBuilder.AddServices"/> on the builder returned by
    /// <c>AddActiveForgeSqlServer</c> / <c>AddActiveForgePostgreSQL</c> / <c>AddActiveForgeMongoDB</c>.
    /// </summary>
    public static class ActiveForgeServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TService"/> as a scoped service backed by a Castle
        /// DynamicProxy that automatically manages the <see cref="DataConnection"/> lifecycle
        /// and (when <see cref="IUnitOfWork"/> is available) database transactions.
        /// <para>
        /// When <typeparamref name="TService"/> is an <b>interface</b>, an interface proxy is
        /// created — the implementation does not need to be non-sealed or have virtual methods.
        /// When it is a <b>class</b>, a class proxy is created — the class must be non-sealed
        /// and intercepted methods must be virtual.
        /// </para>
        /// </summary>
        public static IServiceCollection AddActiveForgeService<TService>(
            this IServiceCollection services)
            where TService : class
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddScoped<TService>(sp =>
            {
                var instance   = ActivatorUtilities.CreateInstance<TService>(sp);
                var connection = sp.GetRequiredService<DataConnection>();
                var unitOfWork = sp.GetService<IUnitOfWork>();
                var logger     = sp.GetService<ILogger<TService>>();

                return ActiveForgeServiceFactory.Create(instance, connection, unitOfWork, logger);
            });

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TImplementation"/> as a scoped service exposed as
        /// <typeparamref name="TInterface"/> with Castle DynamicProxy interception.
        /// <para>
        /// An interface proxy is created so <typeparamref name="TImplementation"/> does not
        /// need virtual methods.  Place <c>[Transaction]</c> on
        /// <typeparamref name="TImplementation"/> — the interceptor resolves attributes
        /// from the concrete method via <c>IInvocation.MethodInvocationTarget</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="TInterface">The interface to register against in DI.</typeparam>
        /// <typeparam name="TImplementation">
        /// The concrete implementation.  Must implement <typeparamref name="TInterface"/>.
        /// </typeparam>
        public static IServiceCollection AddActiveForgeService<TInterface, TImplementation>(
            this IServiceCollection services)
            where TInterface      : class
            where TImplementation : class, TInterface
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddScoped<TInterface>(sp =>
            {
                var instance   = ActivatorUtilities.CreateInstance<TImplementation>(sp);
                var connection = sp.GetRequiredService<DataConnection>();
                var unitOfWork = sp.GetService<IUnitOfWork>();
                var logger     = sp.GetService<ILogger<TImplementation>>();

                // Always use the interface type so an interface proxy is created.
                return (TInterface)ActiveForgeServiceFactory.Create(
                    typeof(TInterface), instance, connection, unitOfWork, logger);
            });

            return services;
        }
    }
}
