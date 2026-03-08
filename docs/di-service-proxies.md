# DI Integration & Service Proxies

This document covers the DI extension methods, `IService` marker interface, and Castle
DynamicProxy service abstraction.  All features are
platform-agnostic — they work in ASP.NET Core, Worker Service, console, or any host that uses
`Microsoft.Extensions.DependencyInjection`.

---

## Overview

Turquoise ORM uses **Castle DynamicProxy** to manage the connection lifecycle and transaction
boundaries at the service-method level, with no framework middleware required.

| Attribute | Interceptor | What it does |
|-----------|-------------|--------------|
| `[ConnectionScope]` | `ConnectionScopeInterceptor` | Opens `DataConnection` before the method; closes it in `finally` |
| `[Transaction]` | `TransactionInterceptor` | Wraps the method in a `IUnitOfWork` transaction |

Services implement the `IService` marker interface and are automatically discovered and
registered as proxied scoped services via a single `.AddServices()` call.

---

## Quick Start

### 1. Register the provider + auto-scan services

```csharp
// Program.cs (or Startup.ConfigureServices)
builder.Services
    .AddTurquoiseSqlServer(
        "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;")
    .AddServices(typeof(Program).Assembly);   // scans for IService implementations

// PostgreSQL:
builder.Services
    .AddTurquoisePostgreSQL("Host=localhost;Database=demo;Username=app;Password=secret;")
    .AddServices(typeof(Program).Assembly);

// MongoDB:
builder.Services
    .AddTurquoiseMongoDB("mongodb://localhost:27017", "demo")
    .AddServices(typeof(Program).Assembly);

// SQLite:
builder.Services
    .AddTurquoiseSQLite("Data Source=app.db")
    .AddServices(typeof(Program).Assembly);
```

`AddServices()` scans the given assembly for all non-abstract classes that implement
`IService`, then registers each one as a scoped service behind its interface(s) — no manual
per-type registration needed.

### 2. Define the service interface + implementation

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;
using Turquoise.ORM.Transactions;

// ── Interface ────────────────────────────────────────────────────────────────
public interface IOrderService
{
    Order GetById(int id);
    void  Ship(int orderId);
}

// ── Implementation ────────────────────────────────────────────────────────────
// Implements IOrderService (the DI-facing interface) + IService (Turquoise marker).
// No virtual methods required — the interface proxy handles interception.
public class OrderService : IOrderService, IService
{
    private readonly DataConnection _conn;

    // DataConnection (or the concrete provider type) is injected by DI.
    public OrderService(DataConnection conn) { _conn = conn; }

    [ConnectionScope]
    public Order GetById(int id)
    {
        var order = new Order(_conn);
        order.ID.SetValue(id);
        return _conn.Read(order) ? order : null;
    }

    [ConnectionScope]
    [Transaction]
    public void Ship(int orderId)
    {
        var order    = new Order(_conn);
        var shipment = new Shipment(_conn);

        order.ID.SetValue(orderId);
        _conn.Read(order);

        order.Status.SetValue("Shipped");
        order.Update(DataObjectLock.UpdateOption.IgnoreLock);
        shipment.OrderID.SetValue(orderId);
        shipment.Insert();
        // Connection opened before this method; transaction commits here on success.
        // On exception: transaction rolls back; connection always closes in finally.
    }
}
```

### 3. Inject by interface

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) { _orders = orders; }

    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        var order = _orders.GetById(id);   // [ConnectionScope] handled by proxy
        return order != null ? Ok(order) : NotFound();
    }

    [HttpPost("{id}/ship")]
    public IActionResult Ship(int id)
    {
        _orders.Ship(id);                  // [ConnectionScope] + [Transaction] handled by proxy
        return NoContent();
    }
}
```

---

## IService marker interface

```csharp
// Turquoise.ORM namespace — import with "using Turquoise.ORM;"
public interface IService { }
```

Implementing `IService` on a class signals that it should be discovered by `AddServices()`.
No methods or properties are required; it is a pure marker.

A class may implement multiple non-system interfaces alongside `IService` — each will be
registered separately in the DI container:

```csharp
public class ReportService : IReportService, IAuditService, IService
{
    // Registered as both IReportService and IAuditService
    [ConnectionScope]
    public Report GenerateSales() { ... }
}
```

---

## Attributes

### `[ConnectionScope]`

```csharp
using Turquoise.ORM.Attributes;

// Method-level: only this method gets the connection scope
[ConnectionScope]
public void DoWork() { ... }

// Class-level: every method on the class gets the scope
[ConnectionScope]
public class OrderService : IOrderService, IService { ... }
```

**Nesting:** if a `[ConnectionScope]` method calls another `[ConnectionScope]` method on
the same proxy instance, the connection is opened once and closed when the outermost call
completes.

### `[Transaction]`

```csharp
using Turquoise.ORM.Transactions;

[Transaction]                                        // ReadCommitted (default)
[Transaction(IsolationLevel.Serializable)]           // explicit isolation
public void Process() { ... }
```

`[Transaction]` requires the connection to be open.  Always pair it with `[ConnectionScope]`
on the same method (or at the class level).

---

## ITurquoiseBuilder — fluent registration

All `AddTurquoise*` extension methods return `ITurquoiseBuilder` for chaining:

```csharp
public interface ITurquoiseBuilder
{
    IServiceCollection Services { get; }

    // Auto-scan assemblies for IService implementations
    ITurquoiseBuilder AddServices(params Assembly[] assemblies);

    // Explicit single-service registration — use when not implementing IService
    ITurquoiseBuilder AddService<TService>() where TService : class;
    ITurquoiseBuilder AddService<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface;
}
```

#### Auto-scan

```csharp
// Scan one or more assemblies
builder.Services
    .AddTurquoiseSqlServer("...")
    .AddServices(typeof(Program).Assembly,
                 typeof(SomeLibrary.Marker).Assembly);
```

If no assembly is supplied, `Assembly.GetEntryAssembly()` is used.

`AddServices` registers all non-abstract `IService` implementations it finds.  For each:
- All non-system interfaces the class implements (excluding `IService` itself) → interface proxy
- If the class has no qualifying interface → concrete class proxy

#### Explicit registration

When a service does not implement `IService` (e.g., a third-party class or a legacy type):

```csharp
builder.Services
    .AddTurquoiseSqlServer("...")
    .AddService<IOrderService, OrderService>()   // interface proxy
    .AddService<ReportEngine>();                 // class proxy (must be non-sealed + virtual)
```

---

## Proxy strategies

| Scenario | Proxy type | Requirements |
|----------|-----------|--------------|
| Service interface + `IService` (auto-scan) | `CreateInterfaceProxyWithTarget` | None — no virtual required |
| `AddService<TInterface, TImpl>()` | `CreateInterfaceProxyWithTarget` | None |
| `AddService<TClass>()` where TClass is a class | `CreateClassProxyWithTarget` | Non-sealed; intercepted methods `virtual` |

**Attribute placement:** when using interface proxies, place `[ConnectionScope]` and
`[Transaction]` on the **implementation class methods** (or the class itself).  The
interceptors use `IInvocation.MethodInvocationTarget` to find the concrete method, so
attributes on the implementation are always detected correctly.  Attributes on the interface
are also supported as a fallback.

---

## Interceptor ordering

```
Call enters proxy
  → ConnectionScopeInterceptor: conn.Connect()
      → TransactionInterceptor: uow.CreateTransaction()
          → Real method executes
      → TransactionInterceptor: uow.Commit()   (or Rollback on exception)
  → ConnectionScopeInterceptor: conn.Disconnect()   (always, in finally)
```

---

## Connection-level lifecycle — no proxy required

When working with entities directly (no service layer), assign `UnitOfWork` on the connection
once.  Every write then automatically opens the connection, begins a transaction, commits, and
closes — with no proxy wrapping of individual entity instances.

### Standalone usage

```csharp
var conn = new SqlServerConnection(
    "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;");
var uow  = new SqlServerUnitOfWork(conn);
conn.UnitOfWork = uow;   // wire once — all entity writes use this UoW automatically

var product = new Product(conn);   // plain entity, no proxy needed
product.Name.SetValue("Widget");
product.Price.SetValue(9.99m);

// Opens connection → begins transaction → inserts → commits → closes.
product.Insert();
```

Write operations that auto-manage lifecycle: `Insert`, `Update`, `UpdateAll`, `UpdateChanged`,
`Delete`, `ProcessActionQueue`, `ExecStoredProcedure`.
Read operations (`Read`, `QueryAll`, `QueryPage`, …) auto-connect/disconnect but do not start
a transaction.

### How it coordinates with `[ConnectionScope]` service proxies

`RunWrite` checks `IsOpen` before connecting, so no double-open occurs when a service proxy
has already opened the connection:

```
[ConnectionScope] service method
  → ConnectionScopeInterceptor: IsOpen == false → Connect()
      entity.Insert()
          → RunWrite: IsOpen == true           → no extra Connect()
          → RunWrite: UoW.InTransaction == true → no new transaction (enlists in ambient)
          → Insert SQL executes
          → RunWrite: did not start tx or open conn → no commit or disconnect
  → ConnectionScopeInterceptor: Disconnect()
```

### Without a `UnitOfWork`

Leave `conn.UnitOfWork` unset for read-only workloads or when you prefer manual transaction
control via `BeginTransaction` / `CommitTransaction`:

```csharp
var conn = new SqlServerConnection("...");
// conn.UnitOfWork not set — no auto-transaction, connection still auto-managed per call

var product = new Product(conn);
product.ID.SetValue(1);
product.Read();   // connects, reads, disconnects — no transaction
```

---

## Standalone service usage (no DI)

```csharp
var conn = new SqlServerConnection("Server=...;...");
var uow  = new SqlServerUnitOfWork(conn);
var svc  = TurquoiseServiceFactory.Create<IOrderService>(
    new OrderService(conn), conn, uow);

svc.Ship(42);  // proxy opens connection, starts transaction, calls Ship(), commits, closes
```

---

## Using `With.Transaction` alongside proxied services

`With.Transaction` still works and shares the same scoped `IUnitOfWork`.  Configure
`TurquoiseServiceLocator` once if you want the no-arg overload:

```csharp
// At startup, after building the container:
TurquoiseServiceLocator.SetProvider(app.Services);

// Then anywhere (inside a DI scope):
With.Transaction(() =>
{
    order.Status.SetValue("Shipped");
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);
});
```

---

## Testing

Unit tests can bypass the proxy entirely and call the real service directly:

```csharp
// Unit test — no proxy needed
var conn = new StubDataConnection();
var svc  = new OrderService(conn);  // concrete class, no interception
svc.Ship(1);

// Integration test — real proxy against test database
var conn  = new SqlServerConnection("Server=.;Database=Test;...");
var uow   = new SqlServerUnitOfWork(conn);
var proxy = TurquoiseServiceFactory.Create<IOrderService>(new OrderService(conn), conn, uow);
proxy.Ship(1);
```

Call `TurquoiseServiceLocator.Reset()` in test teardown to avoid leaking state between tests.

---

## Requirements

| Package | Purpose |
|---------|---------|
| `Turquoise.ORM` | `IService`, `ITurquoiseBuilder`, `ConnectionScopeAttribute`, `ConnectionScopeInterceptor`, `TurquoiseServiceFactory`, `DataConnection.UnitOfWork`, `AddTurquoiseService<T>()` |
| `Castle.Core` 5.x | DynamicProxy runtime (auto-included with `Turquoise.ORM`) |
| `Microsoft.Extensions.DependencyInjection.Abstractions` 8.x | DI integration (auto-included with `Turquoise.ORM`) |
| One of: `Turquoise.ORM.SqlServer`, `Turquoise.ORM.PostgreSQL`, `Turquoise.ORM.MongoDB`, `Turquoise.ORM.SQLite` | Provider + `AddTurquoise*` returning `ITurquoiseBuilder` |
