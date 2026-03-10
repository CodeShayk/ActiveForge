# Unit of Work & Automatic Transactions

This document covers the `ActiveForge.Transactions` namespace.

---

## Overview

ActiveForge ORM ships two complementary transaction models:

| Model | When to use |
|-------|-------------|
| **Manual transactions** (existing) | Explicit `BeginTransaction` / `CommitTransaction` on `DataConnection`. See [transactions.md](transactions.md). |
| **Unit of Work** (this document) | Automatic wrapping of methods via `IUnitOfWork`, `With.Transaction`, and Castle DynamicProxy interceptors. |

---

## Quick Start

### 1. Register the service locator (once at startup)

```csharp
// Microsoft.Extensions.DependencyInjection
services.AddScoped<IUnitOfWork>(sp =>
    new SqlServerUnitOfWork(sp.GetRequiredService<SqlServerConnection>()));

ActiveForgeServiceLocator.SetProvider(app.Services);
```

Or, without a DI container:

```csharp
ActiveForgeServiceLocator.SetUnitOfWorkFactory(
    () => new SqlServerUnitOfWork(connection));
```

### 2. Wrap work in a transaction

```csharp
// With an explicit IUnitOfWork instance
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);
With.Transaction(uow, () =>
{
    order.Status.Value = "Shipped";
    order.Update();
    shipment.Insert();
});

// Via the service locator (resolves automatically)
With.Transaction(() =>
{
    order.Status.Value = "Shipped";
    order.Update();
});
```

### 3. Automatic interception with `[Transaction]`

```csharp
public class OrderService
{
    [Transaction]
    public virtual void Ship(int orderId)
    {
        // All ORM calls here run inside a transaction automatically
        var order = new Order(conn);
        order.ID.Value = orderId;
        conn.Read(order);

        order.Status.Value = "Shipped";
        order.Update();
    }
}

// Create a proxied instance
IUnitOfWork uow  = new SqlServerUnitOfWork(conn);
OrderService svc = DataConnectionProxyFactory.Create(orderServiceInstance, uow);
svc.Ship(42);   // [Transaction] interceptor wraps this automatically
```

---

## IUnitOfWork Interface

```csharp
public interface IUnitOfWork : IDisposable
{
    bool InTransaction { get; }
    TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    void Commit();
    void Rollback();
}
```

### Nesting behaviour

`UnitOfWorkBase` maintains a depth counter:

- **Outer call** `CreateTransaction()` — depth 0 → 1; starts a real ADO.NET transaction.
- **Inner call** `CreateTransaction()` — depth 1 → 2; reuses the existing transaction.
- **Inner `Commit()`** — depth 2 → 1; no DB action yet.
- **Outer `Commit()`** — depth 1 → 0; actually commits.
- **Any `Rollback()`** — marks the transaction as rollback-only; the next outer `Commit()` rolls back instead.

This means nested transactional methods are safe without any extra configuration.

---

## With.Transaction Helper

`With.Transaction` is a static helper that manages the transaction lifecycle for a single `Action` or `Func<T>`.

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
int count = With.Transaction(uow, () =>
{
    product.Insert();
    return product.ID.Value;
});
```

### Isolation-level shorthands

```csharp
With.SerializableTransaction(uow, () => { /* ... */ });
With.RepeatableReadTransaction(uow, () => { /* ... */ });
With.SnapshotTransaction(uow, () => { /* ... */ });
```

---

## TransactionAttribute

`[Transaction]` decorates a method (or class) so that Castle DynamicProxy automatically wraps each invocation in a transaction.

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
- Methods **without** `[Transaction]` that are called while a transaction is already active enlist in the ambient transaction automatically (because the same `IUnitOfWork` is used).

---

## Castle DynamicProxy Interceptor

`TransactionInterceptor` implements `Castle.DynamicProxy.IInterceptor`.

### How it works

1. When an intercepted method is called:
   - If `[Transaction]` is present: calls `uow.CreateTransaction(level)`, then `Proceed()`.
   - On success: calls `uow.Commit()`.
   - On exception: calls `uow.Rollback()` and rethrows.
   - If no `[Transaction]`: delegates directly to the real method (`Proceed()`).
2. Nested `[Transaction]` methods increment the depth counter; only the outermost Commit/Rollback touches the database.

### DataConnectionProxyFactory

```csharp
// Proxy a SqlServerConnection — all [Transaction]-decorated methods are intercepted
IUnitOfWork uow    = new SqlServerUnitOfWork(conn);
SqlServerConnection proxied = DataConnectionProxyFactory.Create(conn, uow);

// Use 'proxied' everywhere instead of 'conn'
proxied.Insert(product);   // if Insert() is decorated with [Transaction]
```

**Requirements:**
- The target type must be **non-sealed** (all `DataConnection` subclasses already are).
- Intercepted methods must be **virtual** (`override` in `DBDataConnection` satisfies this).
- Castle.Core 5.x is added automatically by the NuGet package.

---

## ActiveForgeServiceLocator

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

Call `ActiveForgeServiceLocator.Reset()` in test teardown to avoid leaking state between tests.

---

## SqlServerUnitOfWork

Concrete implementation for SQL Server.

```csharp
var conn = new SqlServerConnection("Server=...;Database=...;...");
conn.Connect();

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

`SqlServerUnitOfWork` calls `conn.BeginTransaction(level)` when the depth transitions from 0 → 1. Subsequent nested calls reuse the same `TransactionBase`.

---

## Logging

`UnitOfWorkBase` and `TransactionInterceptor` both accept an `ILogger` (from `Microsoft.Extensions.Logging.Abstractions`). When the logger is omitted `NullLogger.Instance` is used silently.

```csharp
ILogger<SqlServerUnitOfWork> logger = loggerFactory.CreateLogger<SqlServerUnitOfWork>();
IUnitOfWork uow = new SqlServerUnitOfWork(conn, logger);
```

`With.Transaction` uses a static logger configured via:

```csharp
With.SetLogger(loggerFactory.CreateLogger("ActiveForge.Transactions.With"));
```

---

## Error handling

| Situation | Behaviour |
|-----------|-----------|
| Exception inside `With.Transaction` | Automatically rolls back; exception rethrown |
| Exception inside `[Transaction]` method | Interceptor rolls back; exception rethrown |
| `Dispose()` with open transaction | Rolls back and logs a warning |
| `Rollback()` inside nested call | Marks rollback-only; outermost Commit() rolls back |
| `Commit()` without active transaction | Throws `InvalidOperationException` |
| `CreateTransaction()` after `Dispose()` | Throws `ObjectDisposedException` |

---

## NuGet packages

| Package | Assembly | Purpose |
|---------|----------|---------|
| `Castle.Core` 5.1.1 | `ActiveForge` | DynamicProxy runtime for `TransactionInterceptor` / `DataConnectionProxyFactory` |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | `ActiveForge` | `ILogger` abstraction used by `UnitOfWorkBase` |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | `ActiveForge.SqlServer` | `ILogger<SqlServerUnitOfWork>` parameter |
| `Microsoft.Data.SqlClient` 5.2.1 | `ActiveForge.SqlServer` | ADO.NET SQL Server driver |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | `ActiveForge.PostgreSQL` | `ILogger<PostgreSQLUnitOfWork>` parameter |
| `Npgsql` 8.0.3 | `ActiveForge.PostgreSQL` | ADO.NET PostgreSQL driver |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | `ActiveForge.MongoDB` | `ILogger` parameter for `MongoUnitOfWork` |
| `MongoDB.Driver` 2.28.0 | `ActiveForge.MongoDB` | MongoDB driver |

`SqlServerUnitOfWork` is part of `ActiveForge.SqlServer`; `PostgreSQLUnitOfWork` is part of `ActiveForge.PostgreSQL`; `MongoUnitOfWork` is part of `ActiveForge.MongoDB`. All other Unit of Work types (`IUnitOfWork`, `UnitOfWorkBase`, `With`, `TransactionInterceptor`, `ActiveForgeServiceLocator`) are in `ActiveForge` and have no provider dependency.

## PostgreSQL Unit of Work

Usage is identical to the SQL Server form — just swap the connection and UoW types:

```csharp
using ActiveForge;
using ActiveForge.Transactions;

var conn = new PostgreSQLConnection("Host=localhost;Database=demo;Username=app;Password=secret;");
conn.Connect();

using IUnitOfWork uow = new PostgreSQLUnitOfWork(conn);

With.Transaction(uow, () =>
{
    var product = new Product(conn);
    product.name.SetValue("Widget");
    product.price.SetValue(9.99m);
    conn.Insert(product);
});
```

## MongoDB Unit of Work

`MongoUnitOfWork` wraps `MongoDataConnection` in the same nested-transaction model:

```csharp
using ActiveForge;
using ActiveForge.Transactions;

var conn = new MongoDataConnection("mongodb://localhost:27017", "demo");
conn.Connect();

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
> On a standalone server the `BeginTransaction` call will throw a MongoDB driver error.
> For single-document atomicity no transaction is required — individual CRUD operations
> are inherently atomic in MongoDB.
