# Transactions, Unit of Work & DI Integration

This document covers all transaction and connection-lifecycle features in ActiveForge: manual
transactions, the `IUnitOfWork` layer, `With.Transaction`, the `[Transaction]` Castle
DynamicProxy interceptor, and DI service proxy registration.

---

## Table of Contents

1. [Manual Transactions](#1-manual-transactions)
2. [Optimistic Locking](#2-optimistic-locking)
3. [Action Queue (Batch Operations)](#3-action-queue-batch-operations)
4. [Read-for-Update (Advisory Lock)](#4-read-for-update-advisory-lock)
5. [Unit of Work — IUnitOfWork](#5-unit-of-work--iunitofwork)
6. [With.Transaction Helper](#6-withtransaction-helper)
7. [Connection Lifecycle](#7-connection-lifecycle)
8. [TransactionAttribute and Castle DynamicProxy](#8-transactionattribute-and-castle-dynamicproxy)
9. [ActiveForgeServiceLocator (Ambient DI)](#9-activeforgeservicelocator-ambient-di)
10. [Logging](#10-logging)
11. [Error Handling](#11-error-handling)
12. [DI Integration & Service Proxies](#12-di-integration--service-proxies)
13. [IActiveForgeBuilder — Fluent Registration](#13-iactiveforgebuilder--fluent-registration)
14. [Proxy Strategies](#14-proxy-strategies)
15. [Standalone Usage (No DI)](#15-standalone-usage-no-di)
16. [Testing](#16-testing)
17. [NuGet Packages](#17-nuget-packages)

---

## 1. Manual Transactions

Wrap multiple operations in a single atomic unit using `BeginTransaction` /
`CommitTransaction` / `RollbackTransaction` directly on `DataConnection`.

```csharp
BaseTransaction tx = conn.BeginTransaction();
try
{
    var order = new Order(conn);
    order.CustomerName.SetValue("Alice Smith");
    order.OrderDate.SetValue(DateTime.UtcNow);
    order.TotalAmount.SetValue(0m);
    order.Insert();

    var line = new OrderLine(conn);
    line.OrderID.SetValue((int)order.ID.GetValue());
    line.ProductID.SetValue(7);
    line.Quantity.SetValue(2);
    line.UnitPrice.SetValue(29.99m);
    line.Insert();

    order.TotalAmount.SetValue(59.98m);
    order.Update(RecordLock.UpdateOption.IgnoreLock);

    conn.CommitTransaction(tx);
}
catch
{
    conn.RollbackTransaction(tx);
    throw;
}
```

All `Record` instances bound to the same `DataConnection` automatically participate
in that connection's active transaction — there is no need to pass the transaction handle
to each entity.

### Isolation levels

```csharp
using System.Data;

var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);
// ... operations ...
conn.CommitTransaction(tx);
```

Supported levels: `ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`,
`Serializable`, `Snapshot`.

### Checking transaction state

```csharp
TransactionStates state = conn.TransactionState(tx);
// state is one of: None, Active, Committed, RolledBack
```

### Nested transactions

`DBDataConnection` tracks a transaction depth counter. Calling `BeginTransaction`
inside an already-active transaction increments the counter rather than opening a new
database transaction. Only the outermost `CommitTransaction` or `RollbackTransaction`
actually issues `COMMIT` / `ROLLBACK` to the database.

```csharp
var outer = conn.BeginTransaction();      // depth: 1 — real BEGIN TRANSACTION

var inner = conn.BeginTransaction();      // depth: 2 — logical only
conn.CommitTransaction(inner);            // depth: 1 — no DB commit yet

conn.CommitTransaction(outer);            // depth: 0 — real COMMIT
```

> If any level rolls back, the entire transaction rolls back.

---

## 2. Optimistic Locking

`Update` accepts a `RecordLock.UpdateOption` that controls how the ORM handles
concurrent writes:

| Option | Behaviour |
|--------|-----------|
| `IgnoreLock` | No locking check — simplest option for non-concurrent tables |
| `ReleaseLock` | Checks an optimistic lock column and releases it after update |
| `RetainLock` | Checks an optimistic lock column and keeps the lock for further updates |

```csharp
// Most common — no concurrent locking needed
product.Price.SetValue(14.99m);
product.Update(RecordLock.UpdateOption.IgnoreLock);

// Optimistic lock — throws ObjectLockException if another writer changed the row
product.Update(RecordLock.UpdateOption.ReleaseLock);
```

Catch `ObjectLockException` to handle a lost update gracefully:

```csharp
try
{
    product.Update(RecordLock.UpdateOption.ReleaseLock);
}
catch (ObjectLockException)
{
    // Another process updated this row — re-read and retry
    product.Read();
    product.Price.SetValue(14.99m);
    product.Update(RecordLock.UpdateOption.ReleaseLock);
}
```

---

## 3. Action Queue (Batch Operations)

The action queue lets you accumulate INSERT / UPDATE / DELETE operations and flush
them all in a single database round-trip, wrapped in an automatic transaction.

### Queuing operations

```csharp
var p1 = new Product(conn);
p1.Name.SetValue("Widget A");
p1.Price.SetValue(9.99m);
p1.QueueForInsert();

var p2 = new Product(conn);
p2.Name.SetValue("Widget B");
p2.Price.SetValue(14.99m);
p2.QueueForInsert();

p1.Price.SetValue(7.99m);
p1.QueueForUpdate();

var old = new Product(conn);
old.ID.SetValue(99);
old.QueueForDelete();
```

### Flushing the queue

```csharp
conn.ProcessActionQueue();   // executes all queued operations atomically
```

If any operation fails, the entire batch is rolled back.

### Clearing the queue

```csharp
conn.ClearActionQueue();     // discard pending operations without executing
```

### Deleting by query via queue

```csharp
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.QueueForDelete(term);   // DELETE FROM Products WHERE InStock = 0

conn.ProcessActionQueue();
```

---

## 4. Read-for-Update (Advisory Lock)

`ReadForUpdate` acquires a row-level update lock on SQL Server (`SELECT ... WITH (UPDLOCK)`)
and PostgreSQL (`SELECT ... FOR UPDATE`) so that no other session can update the row between
your read and your subsequent write:

```csharp
var product = new Product(conn);
product.ID.SetValue(42);

var tx = conn.BeginTransaction();
conn.ReadForUpdate(product, null);   // SELECT ... WITH (UPDLOCK)

product.Price.SetValue(product.Price + 5m);
product.Update(RecordLock.UpdateOption.IgnoreLock);

conn.CommitTransaction(tx);          // lock released
```

---

## 5. Unit of Work — IUnitOfWork

`IUnitOfWork` is the recommended approach for automatic transaction management across
multiple service methods.

```csharp
public interface IUnitOfWork : IDisposable
{
    bool InTransaction { get; }
    BaseTransaction CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    void Commit();
    void Rollback();
}
```

### Connection lifetime

`BaseUnitOfWork` manages connection lifetime automatically:

- When `CreateTransaction()` is called and depth is 0 **and the connection is not already
  open**, the UoW opens the connection itself.
- When the outermost `Commit()` or `Rollback()` completes, the UoW closes the connection
  — but **only if it was the one that opened it**.
- If the connection was already open before `CreateTransaction()` was called (e.g., opened
  by the caller), the UoW never closes it.

This means **you do not need to call `Connect()` manually** when using `IUnitOfWork`.

### Nesting behaviour

`BaseUnitOfWork` maintains a depth counter:

- **Outer call** `CreateTransaction()` — depth 0 → 1; opens the connection (if not already
  open) and starts a real ADO.NET transaction.
- **Inner call** `CreateTransaction()` — depth 1 → 2; reuses the existing transaction.
- **Inner `Commit()`** — depth 2 → 1; no DB action yet.
- **Outer `Commit()`** — depth 1 → 0; actually commits, then closes the connection (if owned).
- **Any `Rollback()`** — marks the transaction as rollback-only; the outermost `Commit()`
  rolls back instead.

Nested transactional methods are safe with no extra configuration.

### Provider-specific implementations

| Provider | Class | Package |
|----------|-------|---------|
| SQL Server | `SqlServerUnitOfWork` | `ActiveForge.SqlServer` |
| PostgreSQL | `PostgreSQLUnitOfWork` | `ActiveForge.PostgreSQL` |
| MongoDB | `MongoUnitOfWork` | `ActiveForge.MongoDB` |
| SQLite | `SQLiteUnitOfWork` | `ActiveForge.SQLite` |

All other UoW types (`IUnitOfWork`, `BaseUnitOfWork`, `With`, `TransactionInterceptor`,
`ActiveForgeServiceLocator`) are in `ActiveForge` with no provider dependency.

### Direct usage (SQL Server)

```csharp
// No Connect() needed — BaseUnitOfWork opens the connection on first CreateTransaction()
var conn = new SqlServerConnection("Server=...;Database=...;...");
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);

uow.CreateTransaction();
try
{
    // ORM operations against conn ...
    uow.Commit();
}
catch
{
    uow.Rollback();
    throw;
}
```

### Direct usage (PostgreSQL)

```csharp
var conn = new PostgreSQLConnection("Host=localhost;Database=demo;Username=app;Password=secret;");
using IUnitOfWork uow = new PostgreSQLUnitOfWork(conn);

With.Transaction(uow, () =>
{
    var product = new Product(conn);
    product.name.SetValue("Widget");
    product.price.SetValue(9.99m);
    conn.Insert(product);
});
```

### Direct usage (MongoDB)

```csharp
var conn = new MongoDataConnection("mongodb://localhost:27017", "demo");
using IUnitOfWork uow = new MongoUnitOfWork(conn);

With.Transaction(uow, () =>
{
    var product = new Product(conn);
    product.name.SetValue("Widget");
    product.price.SetValue(9.99m);
    conn.Insert(product);
});
```

> **Note:** MongoDB multi-document transactions require a replica set or sharded cluster.
> On a standalone server `BeginTransaction` will throw a MongoDB driver error. Single-document
> operations are inherently atomic without a transaction.

---

## 6. With.Transaction Helper

`With.Transaction` is a static helper that manages the transaction lifecycle for a single
`Action` or `Func<T>`.

### Action overloads

```csharp
// Explicit IUnitOfWork
With.Transaction(uow, () => { /* work */ });
With.Transaction(uow, () => { /* work */ }, IsolationLevel.Serializable);

// Resolved via ActiveForgeServiceLocator
With.Transaction(() => { /* work */ });
```

### Func<T> overloads (return a value)

```csharp
int id = With.Transaction(uow, () =>
{
    product.Insert();
    return (int)product.ID.GetValue();
});
```

### Isolation-level shorthands

```csharp
With.SerializableTransaction(uow, () => { /* ... */ });
With.RepeatableReadTransaction(uow, () => { /* ... */ });
With.SnapshotTransaction(uow, () => { /* ... */ });
```

---

## 7. Connection Lifecycle

### When using IUnitOfWork

`BaseUnitOfWork` owns connection lifetime when it opens the connection itself:

```
uow.CreateTransaction()
  → depth 0 → 1: IsOpen == false → Connect()  (connection owned by UoW)
                  BeginTransactionCore(level)   (real ADO.NET transaction)
  → depth 1 → 2: reuse existing transaction
uow.Commit()       depth 2 → 1: no DB action
uow.Commit()       depth 1 → 0: Commit → Dispose transaction → Disconnect()
```

If the connection was already open when `CreateTransaction()` was called, the UoW skips
`Connect()` and sets `_ownedConnection = false`. In that case `Commit()`/`Rollback()`
never call `Disconnect()` — the caller keeps ownership of the connection.

### Without a UnitOfWork

Read operations (`Read`, `QueryAll`, `QueryPage`, …) auto-connect and auto-disconnect
per call. Write operations (`Insert`, `Update`, `Delete`, `ProcessActionQueue`, …) also
auto-connect/disconnect when no ambient transaction is active.

```csharp
var conn = new SqlServerConnection("...");
// conn.UnitOfWork not set — no auto-transaction, connection auto-managed per call

var product = new Product(conn);
product.ID.SetValue(1);
product.Read();   // connects, reads, disconnects — no transaction
```

### Connection-level UoW (no proxy)

Assign `conn.UnitOfWork = uow` once. Every write then automatically opens the connection,
begins a transaction, commits, and closes — no service proxy required:

```csharp
var conn = new SqlServerConnection("Server=...;...");
var uow  = new SqlServerUnitOfWork(conn);
conn.UnitOfWork = uow;

var product = new Product(conn);
product.Name.SetValue("Widget");
product.Price.SetValue(9.99m);

// UoW opens connection → begins transaction → inserts → commits → closes.
product.Insert();
```

---

## 8. TransactionAttribute and Castle DynamicProxy

### TransactionAttribute

`[Transaction]` decorates a method (or class) so Castle DynamicProxy automatically wraps
each invocation in an `IUnitOfWork` transaction.

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class TransactionAttribute : Attribute
{
    public IsolationLevel IsolationLevel { get; }
    public TransactionAttribute(IsolationLevel level = IsolationLevel.ReadCommitted);
}
```

- **Method-level** — only that method is intercepted.
- **Class-level** — every virtual method on the class is intercepted.
- Methods **without** `[Transaction]` that are called while a transaction is active enlist
  in the ambient transaction automatically (same `IUnitOfWork` depth counter).

### How TransactionInterceptor works

`TransactionInterceptor` implements `Castle.DynamicProxy.IInterceptor`:

1. If `[Transaction]` is present: calls `uow.CreateTransaction(level)`, then `Proceed()`.
2. On success: calls `uow.Commit()`.
3. On exception: calls `uow.Rollback()` and rethrows.
4. If no `[Transaction]`: delegates directly to the real method.

Nested `[Transaction]` methods increment the depth counter; only the outermost
Commit/Rollback touches the database.

### Connection is managed by the UoW

The `TransactionInterceptor` does not open or close the connection itself. When
`CreateTransaction()` is first called (depth 0 → 1), `BaseUnitOfWork` opens the connection
if it is not already open. The connection is closed when the outermost Commit/Rollback
completes. No separate connection-scope attribute is required.

```csharp
// Proxy ordering (single interceptor):
Call enters proxy
  → TransactionInterceptor: uow.CreateTransaction() [opens connection + begins tx at depth 0]
      → Real method executes
  → TransactionInterceptor: uow.Commit()   (or Rollback on exception)
      [at depth 0: commits tx, closes connection]
```

---

## 9. ActiveForgeServiceLocator (Ambient DI)

A thin static bridge that lets any DI container back the locator.

```csharp
// Register once at startup:
ActiveForgeServiceLocator.SetProvider(serviceProvider);      // any IServiceProvider
// OR:
ActiveForgeServiceLocator.SetUnitOfWorkFactory(() => new SqlServerUnitOfWork(conn));

// Resolve anywhere (e.g. inside With.Transaction or your own code):
IUnitOfWork uow = ActiveForgeServiceLocator.GetUnitOfWork();
T svc           = ActiveForgeServiceLocator.Resolve<T>();
```

Call `ActiveForgeServiceLocator.Reset()` in test teardown to avoid leaking state between
tests.

---

## 10. Logging

`BaseUnitOfWork` and `TransactionInterceptor` both accept an `ILogger` (from
`Microsoft.Extensions.Logging.Abstractions`). When omitted, `NullLogger.Instance` is used
silently.

```csharp
ILogger<SqlServerUnitOfWork> logger = loggerFactory.CreateLogger<SqlServerUnitOfWork>();
IUnitOfWork uow = new SqlServerUnitOfWork(conn, logger);
```

`With.Transaction` uses a static logger configured via:

```csharp
With.SetLogger(loggerFactory.CreateLogger("ActiveForge.Transactions.With"));
```

---

## 11. Error Handling

| Situation | Behaviour |
|-----------|-----------|
| Exception inside `With.Transaction` | Automatically rolls back; exception rethrown |
| Exception inside `[Transaction]` method | Interceptor rolls back; exception rethrown |
| `Dispose()` with open transaction | Rolls back and logs a warning |
| `Rollback()` inside nested call | Marks rollback-only; outermost Commit() rolls back instead |
| `Commit()` without active transaction | Throws `InvalidOperationException` |
| `CreateTransaction()` after `Dispose()` | Throws `ObjectDisposedException` |

---

## 12. DI Integration & Service Proxies

All features below are platform-agnostic — they work in ASP.NET Core, Worker Service,
console, or any host that uses `Microsoft.Extensions.DependencyInjection`.

### Quick start

#### 1. Register the provider and auto-scan services

```csharp
// Program.cs (or Startup.ConfigureServices)
builder.Services
    .AddActiveForgeSqlServer(
        "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;")
    .AddServices(typeof(Program).Assembly);   // scans for IService implementations

// PostgreSQL:
builder.Services
    .AddActiveForgePostgreSQL("Host=localhost;Database=demo;Username=app;Password=secret;")
    .AddServices(typeof(Program).Assembly);

// MongoDB:
builder.Services
    .AddActiveForgeMongoDB("mongodb://localhost:27017", "demo")
    .AddServices(typeof(Program).Assembly);

// SQLite:
builder.Services
    .AddActiveForgeSQLite("Data Source=app.db")
    .AddServices(typeof(Program).Assembly);
```

`AddServices()` scans the given assembly for all non-abstract classes that implement
`IService`, then registers each one as a scoped service behind its interface(s) — no
manual per-type registration needed.

#### 2. Define the service interface and implementation

```csharp
using ActiveForge;
using ActiveForge.Transactions;

// ── Interface ─────────────────────────────────────────────────────────────────
public interface IOrderService
{
    Order GetById(int id);
    void  Ship(int orderId);
}

// ── Implementation ────────────────────────────────────────────────────────────
// Implements IOrderService (the DI-facing interface) + IService (ActiveForge marker).
// No virtual methods required — the interface proxy handles interception.
public class OrderService : IOrderService, IService
{
    private readonly DataConnection _conn;

    public OrderService(DataConnection conn) { _conn = conn; }

    // No transaction needed — read-only, no UoW required.
    public Order GetById(int id)
    {
        var order = new Order(_conn);
        order.ID.SetValue(id);
        _conn.Read(order);
        return order;
    }

    // [Transaction] opens the connection, begins a transaction, and closes on completion.
    [Transaction]
    public void Ship(int orderId)
    {
        var order    = new Order(_conn);
        var shipment = new Shipment(_conn);

        order.ID.SetValue(orderId);
        _conn.Read(order);

        order.Status.SetValue("Shipped");
        order.Update(RecordLock.UpdateOption.IgnoreLock);
        shipment.OrderID.SetValue(orderId);
        shipment.Insert();
        // Transaction commits here on success; rolls back on exception.
        // Connection is closed by the UoW when the outermost Commit completes.
    }
}
```

#### 3. Inject by interface

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
        var order = _orders.GetById(id);
        return order != null ? Ok(order) : NotFound();
    }

    [HttpPost("{id}/ship")]
    public IActionResult Ship(int id)
    {
        _orders.Ship(id);   // [Transaction] proxy opens conn, begins tx, commits/rolls back
        return NoContent();
    }
}
```

### IService marker interface

```csharp
// ActiveForge namespace — import with "using ActiveForge;"
public interface IService { }
```

Implementing `IService` on a class signals that it should be discovered by `AddServices()`.
No methods or properties are required.

A class may implement multiple non-system interfaces alongside `IService` — each will be
registered separately in the DI container:

```csharp
public class ReportService : IReportService, IAuditService, IService
{
    // Registered as both IReportService and IAuditService
    [Transaction]
    public Report GenerateSales() { ... }
}
```

### `[Transaction]` in DI services

```csharp
using ActiveForge.Transactions;

[Transaction]                                        // ReadCommitted (default)
[Transaction(IsolationLevel.Serializable)]           // explicit isolation
public void Process() { ... }
```

`[Transaction]` on a service method is sufficient — the `TransactionInterceptor` opens the
connection (via `BaseUnitOfWork.CreateTransaction`) and closes it on completion. No
additional attribute is needed.

**Attribute placement:** when using interface proxies (the default for `IService` services),
place `[Transaction]` on the **implementation class method** (or the class itself).
The interceptor uses `IInvocation.MethodInvocationTarget` to find the concrete method, so
attributes on the implementation are always detected correctly. Attributes on the interface
are supported as a fallback.

---

## 13. IActiveForgeBuilder — Fluent Registration

All `AddActiveForge*` extension methods return `IActiveForgeBuilder` for chaining:

```csharp
public interface IActiveForgeBuilder
{
    IServiceCollection Services { get; }

    // Auto-scan assemblies for IService implementations
    IActiveForgeBuilder AddServices(params Assembly[] assemblies);

    // Explicit single-service registration
    IActiveForgeBuilder AddService<TService>() where TService : class;
    IActiveForgeBuilder AddService<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface;
}
```

#### Auto-scan

```csharp
builder.Services
    .AddActiveForgeSqlServer("...")
    .AddServices(typeof(Program).Assembly,
                 typeof(SomeLibrary.Marker).Assembly);
```

If no assembly is supplied, `Assembly.GetEntryAssembly()` is used.

`AddServices` registers all non-abstract `IService` implementations it finds. For each:
- All non-system interfaces the class implements (excluding `IService` itself) → interface proxy
- If the class has no qualifying interface → concrete class proxy

#### Explicit registration

When a service does not implement `IService`:

```csharp
builder.Services
    .AddActiveForgeSqlServer("...")
    .AddService<IOrderService, OrderService>()   // interface proxy
    .AddService<ReportEngine>();                 // class proxy (non-sealed + virtual)
```

---

## 14. Proxy Strategies

| Scenario | Proxy type | Requirements |
|----------|-----------|--------------|
| Service interface + `IService` (auto-scan) | `CreateInterfaceProxyWithTarget` | None — no virtual required |
| `AddService<TInterface, TImpl>()` | `CreateInterfaceProxyWithTarget` | None |
| `AddService<TClass>()` where TClass is a class | `CreateClassProxyWithTarget` | Non-sealed; intercepted methods `virtual` |

`TransactionInterceptor` is the sole interceptor registered by the proxy factory. When
`IUnitOfWork` is not registered in DI, no interceptors are applied and the proxy passes
through directly.

---

## 15. Standalone Usage (No DI)

```csharp
var conn = new SqlServerConnection("Server=...;...");
var uow  = new SqlServerUnitOfWork(conn);
var svc  = ActiveForgeServiceFactory.Create<IOrderService>(
    new OrderService(conn), conn, uow);

svc.Ship(42);  // proxy begins transaction, calls Ship(), commits, closes connection
```

`With.Transaction` also works alongside proxied services and shares the same scoped
`IUnitOfWork`. Configure `ActiveForgeServiceLocator` once if you want the no-arg overload:

```csharp
// At startup, after building the container:
ActiveForgeServiceLocator.SetProvider(app.Services);

// Then anywhere (inside a DI scope):
With.Transaction(() =>
{
    order.Status.SetValue("Shipped");
    order.Update(RecordLock.UpdateOption.IgnoreLock);
});
```

---

## 16. Testing

Unit tests can bypass the proxy entirely and call the real service directly:

```csharp
// Unit test — no proxy needed
var conn = new StubDataConnection();
var svc  = new OrderService(conn);   // concrete class, no interception
svc.GetById(1);

// Integration test — real proxy against test database
var conn  = new SqlServerConnection("Server=.;Database=Test;...");
var uow   = new SqlServerUnitOfWork(conn);
var proxy = ActiveForgeServiceFactory.Create<IOrderService>(new OrderService(conn), conn, uow);
proxy.Ship(1);
```

Call `ActiveForgeServiceLocator.Reset()` in test teardown to avoid leaking state between
tests.

---

## 17. NuGet Packages

| Package | Purpose |
|---------|---------|
| `Castle.Core` 5.1.1 | DynamicProxy runtime for `TransactionInterceptor` |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | `ILogger` abstraction used by `BaseUnitOfWork` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0 | DI integration (`IActiveForgeBuilder`, `AddActiveForgeService<T>`) |
| `ActiveForge.SqlServer` | `SqlServerUnitOfWork`, `AddActiveForgeSqlServer()` |
| `ActiveForge.PostgreSQL` | `PostgreSQLUnitOfWork`, `AddActiveForgePostgreSQL()` |
| `ActiveForge.MongoDB` | `MongoUnitOfWork`, `AddActiveForgeMongoDB()` |
| `ActiveForge.SQLite` | `SQLiteUnitOfWork`, `AddActiveForgeSQLite()` |

---

## Summary

| Scenario | Recommended approach |
|----------|----------------------|
| Single entity save | `Insert()` / `Update()` / `Delete()` directly |
| Multiple entities atomically (manual) | `BeginTransaction` → operations → `CommitTransaction` |
| Multiple entities atomically (automatic) | `With.Transaction(uow, () => { … })` |
| Service method auto-transaction | `[Transaction]` attribute + Castle DynamicProxy |
| Bulk inserts / updates | `QueueForInsert` / `QueueForUpdate` → `ProcessActionQueue` |
| Preventing lost updates | `ReadForUpdate` inside a transaction |
| Optimistic concurrency | `Update(RecordLock.UpdateOption.ReleaseLock)` + catch `ObjectLockException` |
