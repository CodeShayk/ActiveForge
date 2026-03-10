using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Marks a method (or every virtual method on a class) so that
    /// <see cref="ActiveForge.Transactions.ConnectionScopeInterceptor"/> automatically opens the
    /// <see cref="DataConnection"/> before the method runs and closes it (returning the
    /// underlying ADO.NET connection to the pool) in a <c>finally</c> block when the method
    /// returns or throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Depth-tracking ensures that the connection is opened only once, even when a
    /// <c>[ConnectionScope]</c> method calls another intercepted <c>[ConnectionScope]</c> method
    /// on the same proxy.  The connection is closed only when the outermost scope exits.
    /// </para>
    /// <para>
    /// When used together with <see cref="ActiveForge.Transactions.TransactionAttribute"/>,
    /// apply <c>[ConnectionScope]</c> to the same method or class so the connection is open before
    /// the transaction begins:
    /// </para>
    /// <code>
    /// public class OrderService
    /// {
    ///     [ConnectionScope]
    ///     [Transaction]
    ///     public virtual void Ship(int orderId) { ... }
    ///
    ///     [ConnectionScope]
    ///     public virtual Order GetById(int id) { ... }
    /// }
    /// </code>
    /// <para>
    /// The intercepted class must be non-sealed and decorated methods must be <c>virtual</c> so
    /// Castle DynamicProxy can generate a subclass proxy.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ConnectionScopeAttribute : Attribute { }
}
