using System;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turquoise.ORM.Attributes;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// Castle DynamicProxy interceptor that manages the <see cref="DataConnection"/> lifecycle
    /// for methods decorated with <see cref="ConnectionScopeAttribute"/> (or classes decorated
    /// with it).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interceptor opens the connection before the first <c>[ConnectionScope]</c> method
    /// in a call chain and closes it (in a <c>finally</c> block) when that outermost method
    /// returns or throws.  Nested calls to other <c>[ConnectionScope]</c> methods on the same
    /// proxy reuse the open connection; the depth counter ensures the connection is only closed
    /// once — when execution returns to the outermost scope.
    /// </para>
    /// <para>
    /// <b>Interface proxy support:</b> when the proxy is an interface proxy (created via
    /// <see cref="TurquoiseServiceFactory"/>), attribute resolution checks
    /// <c>IInvocation.MethodInvocationTarget</c> (the concrete implementation method) first,
    /// then falls back to the interface method.  This means <c>[ConnectionScope]</c> can be
    /// placed on the concrete class or interface — both are detected correctly.
    /// </para>
    /// <para>
    /// <b>Connection coordination:</b> the interceptor checks <see cref="DataConnection.IsOpen"/>
    /// before opening and only closes the connection if it opened it.  This means entity-level
    /// writes that auto-open the connection via <see cref="DataConnection.RunWrite{T}"/> share
    /// the same physical connection without double-opening or prematurely closing it — no
    /// separate depth tracker is required.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> a single proxy instance must not be called concurrently from
    /// multiple threads.  When registered as a scoped DI service (one proxy per request) this
    /// constraint is always satisfied.
    /// </para>
    /// </remarks>
    public sealed class ConnectionScopeInterceptor : IInterceptor
    {
        private readonly DataConnection _connection;
        private readonly ILogger        _logger;

        public ConnectionScopeInterceptor(DataConnection connection, ILogger logger = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger     = logger     ?? NullLogger.Instance;
        }

        public void Intercept(IInvocation invocation)
        {
            if (!HasConnectionScope(invocation))
            {
                invocation.Proceed();
                return;
            }

            bool openedConn = !_connection.IsOpen;

            try
            {
                if (openedConn)
                {
                    _logger.LogDebug(
                        "ConnectionScopeInterceptor: opening connection for {Method}",
                        invocation.Method.Name);
                    _connection.Connect();
                }

                invocation.Proceed();
            }
            catch
            {
                throw;
            }
            finally
            {
                if (openedConn)
                {
                    _connection.Disconnect();
                    _logger.LogDebug(
                        "ConnectionScopeInterceptor: closed connection for {Method}",
                        invocation.Method.Name);
                }
            }
        }

        // ── Attribute resolution ─────────────────────────────────────────────────────

        private static bool HasConnectionScope(IInvocation invocation)
        {
            // Prefer the concrete implementation method so that attributes placed on the
            // implementing class (not the interface) are found correctly for interface proxies.
            var implMethod = invocation.MethodInvocationTarget ?? invocation.Method;

            if (implMethod.GetCustomAttribute<ConnectionScopeAttribute>(inherit: true) != null)
                return true;

            if (implMethod.DeclaringType?
                          .GetCustomAttribute<ConnectionScopeAttribute>(inherit: true) != null)
                return true;

            // Also check the interface/proxy method in case the attribute is on the interface.
            if (implMethod != invocation.Method)
            {
                if (invocation.Method.GetCustomAttribute<ConnectionScopeAttribute>(inherit: true) != null)
                    return true;

                if (invocation.Method.DeclaringType?
                              .GetCustomAttribute<ConnectionScopeAttribute>(inherit: true) != null)
                    return true;
            }

            return false;
        }
    }
}
