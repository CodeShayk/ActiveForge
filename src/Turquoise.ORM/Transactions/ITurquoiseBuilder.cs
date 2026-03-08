using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// Fluent builder returned by <c>AddTurquoiseSqlServer</c>, <c>AddTurquoisePostgreSQL</c>,
    /// and <c>AddTurquoiseMongoDB</c>.  Provides methods to register application service classes
    /// with Castle DynamicProxy interception.
    /// </summary>
    public interface ITurquoiseBuilder
    {
        /// <summary>Gets the underlying <see cref="IServiceCollection"/>.</summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// Scans <paramref name="assemblies"/> for all non-abstract classes that implement
        /// <see cref="IService"/> and registers each one as a scoped service backed by a
        /// Castle DynamicProxy.  The proxy is registered against every non-system interface
        /// the class implements (excluding <see cref="IService"/> itself), so consumers inject
        /// by interface.  If a class has no qualifying interface it is registered against its
        /// own concrete type.
        /// </summary>
        /// <param name="assemblies">
        /// One or more assemblies to scan.  When none are supplied,
        /// <see cref="Assembly.GetEntryAssembly"/> is used.
        /// </param>
        ITurquoiseBuilder AddServices(params Assembly[] assemblies);

        /// <summary>
        /// Registers <typeparamref name="TService"/> as a scoped service with proxy
        /// interception.  Use when you want to register a single class explicitly.
        /// </summary>
        ITurquoiseBuilder AddService<TService>() where TService : class;

        /// <summary>
        /// Registers <typeparamref name="TImplementation"/> as a scoped service exposed as
        /// <typeparamref name="TInterface"/> with proxy interception.
        /// </summary>
        ITurquoiseBuilder AddService<TInterface, TImplementation>()
            where TInterface      : class
            where TImplementation : class, TInterface;
    }

    /// <summary>
    /// Default implementation of <see cref="ITurquoiseBuilder"/>.
    /// </summary>
    public sealed class TurquoiseBuilder : ITurquoiseBuilder
    {
        public IServiceCollection Services { get; }

        public TurquoiseBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public ITurquoiseBuilder AddServices(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                var entry = Assembly.GetEntryAssembly();
                assemblies = entry != null ? new[] { entry } : Array.Empty<Assembly>();
            }

            foreach (var assembly in assemblies)
                ScanAssembly(Services, assembly);

            return this;
        }

        public ITurquoiseBuilder AddService<TService>() where TService : class
        {
            Services.AddTurquoiseService<TService>();
            return this;
        }

        public ITurquoiseBuilder AddService<TInterface, TImplementation>()
            where TInterface      : class
            where TImplementation : class, TInterface
        {
            Services.AddTurquoiseService<TInterface, TImplementation>();
            return this;
        }

        // ── Assembly scanning ────────────────────────────────────────────────────────

        private static readonly Type _iService    = typeof(IService);
        private static readonly Type _iDisposable = typeof(IDisposable);

        internal static void ScanAssembly(IServiceCollection services, Assembly assembly)
        {
            var implTypes = assembly.GetTypes()
                .Where(t => t.IsClass
                         && !t.IsAbstract
                         && !t.IsGenericTypeDefinition
                         && _iService.IsAssignableFrom(t));

            foreach (var implType in implTypes)
                RegisterImpl(services, implType);
        }

        private static void RegisterImpl(IServiceCollection services, Type implType)
        {
            // Collect interfaces exposed to DI consumers:
            //   - exclude IService (marker only)
            //   - exclude IDisposable
            //   - exclude interfaces from System.* / Microsoft.* namespaces
            var serviceInterfaces = implType.GetInterfaces()
                .Where(i => i != _iService
                         && i != _iDisposable
                         && !IsSystemInterface(i))
                .ToList();

            if (serviceInterfaces.Count > 0)
            {
                foreach (var iface in serviceInterfaces)
                    RegisterProxied(services, iface, implType);
            }
            else
            {
                // No qualifying interface — register against the concrete type.
                RegisterProxied(services, implType, implType);
            }
        }

        private static void RegisterProxied(
            IServiceCollection services,
            Type               serviceType,
            Type               implType)
        {
            services.AddScoped(serviceType, sp =>
            {
                var instance   = ActivatorUtilities.CreateInstance(sp, implType);
                var connection = sp.GetRequiredService<DataConnection>();
                var unitOfWork = sp.GetService<IUnitOfWork>();
                var logger     = sp.GetService<ILoggerFactory>()?.CreateLogger(implType);

                return TurquoiseServiceFactory.Create(serviceType, instance, connection, unitOfWork, logger);
            });
        }

        private static bool IsSystemInterface(Type iface)
        {
            var ns = iface.Namespace;
            return ns != null
                && (ns.StartsWith("System.", StringComparison.Ordinal)
                 || ns.StartsWith("Microsoft.", StringComparison.Ordinal)
                 || ns == "System"
                 || ns == "Microsoft");
        }
    }
}
