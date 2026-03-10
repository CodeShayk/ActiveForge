using System;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Ambient static service locator for resolving <see cref="IUnitOfWork"/> instances.
    /// Call <see cref="SetProvider"/> once at application startup (e.g. in Program.cs or
    /// Startup.ConfigureServices) with your DI container's <see cref="IServiceProvider"/>.
    /// Works as a thin bridge that lets any DI container back the locator.
    /// </summary>
    public static class ActiveForgeServiceLocator
    {
        private static IServiceProvider _provider;
        private static Func<IUnitOfWork> _factory;

        /// <summary>
        /// Registers an <see cref="IServiceProvider"/> for subsequent <see cref="Resolve{T}"/> calls.
        /// </summary>
        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _factory  = null;
        }

        /// <summary>
        /// Registers a factory delegate used to construct <see cref="IUnitOfWork"/> instances.
        /// Useful when no DI container is present.
        /// </summary>
        public static void SetUnitOfWorkFactory(Func<IUnitOfWork> factory)
        {
            _factory  = factory ?? throw new ArgumentNullException(nameof(factory));
            _provider = null;
        }

        /// <summary>
        /// Resolves a service of type <typeparamref name="T"/> from the registered provider.
        /// </summary>
        public static T Resolve<T>()
        {
            if (_provider != null)
            {
                object svc = _provider.GetService(typeof(T));
                if (svc == null)
                    throw new InvalidOperationException(
                        $"ActiveForgeServiceLocator: no service registered for {typeof(T).FullName}. " +
                        "Register it in your DI container before calling Resolve<T>().");
                return (T)svc;
            }

            if (_factory != null && typeof(T) == typeof(IUnitOfWork))
                return (T)_factory();

            throw new InvalidOperationException(
                "ActiveForgeServiceLocator has not been initialised. " +
                "Call ActiveForgeServiceLocator.SetProvider(IServiceProvider) at startup.");
        }

        /// <summary>
        /// Convenience shorthand for <c>Resolve&lt;IUnitOfWork&gt;()</c>.
        /// </summary>
        public static IUnitOfWork GetUnitOfWork() => Resolve<IUnitOfWork>();

        /// <summary>Clears the registered provider/factory. Primarily for unit-test teardown.</summary>
        public static void Reset()
        {
            _provider = null;
            _factory  = null;
        }
    }
}
