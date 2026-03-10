namespace ActiveForge
{
    /// <summary>
    /// Marker interface for application service classes that participate in ActiveForge ORM's
    /// automatic DI registration and Castle DynamicProxy interception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implement this interface (alongside a dedicated service interface) on any class that
    /// should have its connection lifecycle and transactions managed automatically via
    /// <see cref="ActiveForge.Transactions.ConnectionScopeInterceptor"/> and
    /// <see cref="ActiveForge.Transactions.TransactionInterceptor"/>.
    /// </para>
    /// <para>
    /// Call <c>builder.AddServices(typeof(MyApp).Assembly)</c> (on the
    /// <see cref="ActiveForge.Transactions.IActiveForgeBuilder"/> returned by
    /// <c>AddActiveForgeSqlServer</c> / <c>AddActiveForgePostgreSQL</c> / <c>AddActiveForgeMongoDB</c>)
    /// to automatically discover and register all <see cref="IService"/> implementations in the
    /// given assembly.
    /// </para>
    /// <example>
    /// <code>
    /// // 1. Define an interface for the service
    /// public interface IOrderService
    /// {
    ///     Order GetById(int id);
    ///     void Ship(int orderId);
    /// }
    ///
    /// // 2. Implement IOrderService + IService
    /// public class OrderService : IOrderService, IService
    /// {
    ///     private readonly DataConnection _conn;
    ///     public OrderService(DataConnection conn) { _conn = conn; }
    ///
    ///     [ConnectionScope]
    ///     public Order GetById(int id) { ... }
    ///
    ///     [ConnectionScope]
    ///     [Transaction]
    ///     public void Ship(int orderId) { ... }
    /// }
    ///
    /// // 3. Auto-register in Program.cs
    /// builder.Services
    ///     .AddActiveForgeSqlServer("Server=...;...")
    ///     .AddServices(typeof(Program).Assembly);
    ///
    /// // 4. Inject by interface — the proxy is transparent to consumers
    /// public class CheckoutController : ControllerBase
    /// {
    ///     public CheckoutController(IOrderService orders) { ... }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public interface IService { }
}
