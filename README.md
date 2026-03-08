# Turquoise.ORM

A lightweight, Active Record-style ORM for .NET 8, with first-class support for SQL Server, PostgreSQL, and MongoDB.

---

## Packages

| Package | Description |
|---------|-------------|
| `Turquoise.ORM` | Core — entities, fields, predicates, LINQ, transactions, adapters, Castle proxy factory |
| `Turquoise.ORM.SqlServer` | SQL Server provider — `SqlServerConnection`, ADO.NET adapters, `SqlServerUnitOfWork`, DI extensions |
| `Turquoise.ORM.PostgreSQL` | PostgreSQL provider — `PostgreSQLConnection`, Npgsql adapters, `PostgreSQLUnitOfWork`, DI extensions |
| `Turquoise.ORM.MongoDB` | MongoDB provider — `MongoDataConnection`, BSON mapping, `MongoUnitOfWork`, DI extensions |
| `Turquoise.ORM.SQLite` | SQLite provider — `SQLiteConnection`, Microsoft.Data.Sqlite adapters, `SQLiteUnitOfWork`, DI extensions |

All connection types live in the `Turquoise.ORM` namespace, so a single `using Turquoise.ORM;` is sufficient regardless of the provider chosen.

---

## Features

### 🗂 Entities & Mapping
- **Active Record pattern** — entities carry both data and persistence behaviour; no separate repository class required
- **Type-safe field wrappers** — `TString`, `TInt`, `TDecimal`, `TPrimaryKey`, `TForeignKey`, and 25+ more; each tracks null/loaded state and supports implicit conversion
- **Polymorphic mapping** — map abstract base types to concrete subtypes via `FactoryBase`
- **Custom field mappers** — implement `IDBFieldMapper` for non-standard type conversions
- **Field encryption** — transparent encrypt/decrypt via `[Encrypted]` attribute

### 🔍 Querying
- **Composable query predicates** — `EqualTerm`, `ContainsTerm`, `InTerm`, `GreaterThanTerm`, `LessThanTerm`, `LessOrEqualTerm`, `GreaterOrEqualTerm`, `IsNullTerm`, `LikeTerm`, composed with `&`, `|`, `!`
- **LINQ query support** — `conn.Query<T>().Where(...).OrderBy(...).Take(...).Skip(...)` translated to native ORM predicates
- **Pagination** — `QueryPage` with `StartRecord`, `PageSize`, `IsMoreData`, `TotalRowCount`
- **Lazy streaming** — `LazyQueryAll<T>` streams rows without buffering the full result set
- **Field subsets** — partial SELECTs and partial UPDATEs via `FieldSubset`

### 💾 Data Management
- **Transactions** — manual nested transactions via `BeginTransaction` / `CommitTransaction` / `RollbackTransaction`
- **Unit of Work** — `IUnitOfWork`, `UnitOfWorkBase`, provider-specific implementations, `With.Transaction`, `[Transaction]` attribute, Castle DynamicProxy interceptor
- **`[Transaction]`** — wraps a service method in a `IUnitOfWork` transaction via Castle DynamicProxy; combine with `[ConnectionScope]` for the full open → begin → commit → close lifecycle
- **`[ConnectionScope]`** — marks service methods/classes where the `DataConnection` is opened before and closed after; nested calls share one connection via `IsOpen` state
- **Connection-level lifecycle** — set `conn.UnitOfWork = uow` once; every write operation (`Insert`, `Update`, `Delete`, `ProcessActionQueue`, `ExecStoredProcedure`) automatically opens the connection, begins a transaction, commits, and closes — no proxy required; coordinates transparently with `[ConnectionScope]` via `IsOpen`
- **Action queue** — batch operations via `QueueForInsert` / `QueueForUpdate` / `QueueForDelete` → `ProcessActionQueue`

### 🌐 DI & Service Proxy Integration
- **`IService` marker** — implement on any service class for automatic discovery and proxy registration
- **Auto-scan registration** — `AddTurquoiseSqlServer(...).AddServices(assembly)` discovers all `IService` implementations and registers them as interface-proxied scoped services in one call
- **`ITurquoiseBuilder`** — fluent builder returned by all `AddTurquoise*` methods; chain `.AddServices()`, `.AddService<I, T>()` for granular control

---

## Requirements

- .NET 8.0
- One provider package:
  - SQL Server → `Turquoise.ORM.SqlServer` (wraps `Microsoft.Data.SqlClient` 5.2.1)
  - PostgreSQL → `Turquoise.ORM.PostgreSQL` (wraps `Npgsql` 8.0.3)
  - MongoDB → `Turquoise.ORM.MongoDB` (wraps `MongoDB.Driver` 2.28.0)
  - SQLite → `Turquoise.ORM.SQLite` (wraps `Microsoft.Data.Sqlite` 8.0.0)

---

## Quick Start

### 1. Connect (standalone)

```csharp
using Turquoise.ORM;

// SQL Server
var conn = new SqlServerConnection(
    "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;");

// PostgreSQL
var conn = new PostgreSQLConnection(
    "Host=localhost;Database=demo;Username=app;Password=secret;");

// MongoDB
var conn = new MongoDataConnection(
    "mongodb://localhost:27017",
    "demo");

// SQLite
var conn = new SQLiteConnection("Data Source=app.db");

conn.Connect();
```

### 2. Register with DI

Works in any DI host — ASP.NET Core, Worker Service, console, etc.
The `AddTurquoise*` call registers the connection + UoW and returns an `ITurquoiseBuilder`.
Chain `.AddServices()` to auto-scan your assembly for `IService` implementations.

```csharp
// Program.cs — choose one provider, then scan for IService implementations:
builder.Services
    .AddTurquoiseSqlServer(
        "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddTurquoisePostgreSQL("Host=localhost;Database=demo;Username=app;Password=secret;")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddTurquoiseMongoDB("mongodb://localhost:27017", "demo")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddTurquoiseSQLite("Data Source=app.db")
    .AddServices(typeof(Program).Assembly);
```

### 3. Define an entity

Entity classes are provider-agnostic — the same class works with SQL Server, PostgreSQL, and MongoDB.

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;

[Table("products")]
public class Product : IdentDataObject
{
    [Column("name")]     public TString  Name    = new TString();
    [Column("price")]    public TDecimal Price   = new TDecimal();
    [Column("in_stock")] public TBool    InStock = new TBool();

    public Product() { }
    public Product(DataConnection conn) : base(conn) { }
}
```

> **Naming conventions:** PostgreSQL folds unquoted identifiers to lower-case — use lower-case `[Table]` and `[Column]` values. MongoDB uses the attribute values as BSON field and collection names verbatim.

### 4. CRUD

```csharp
// Insert
var p = new Product(conn);
p.Name.SetValue("Widget");
p.Price.SetValue(9.99m);
p.InStock.SetValue(true);
p.Insert();   // p.ID is populated automatically after insert

// Read by primary key
var p2 = new Product(conn);
p2.ID.SetValue(1);
bool found = p2.Read();

// Query
var template = new Product(conn);
var inStock  = new EqualTerm(template, template.InStock, true);
var results  = conn.QueryAll(template, inStock, null, 0, null);

// Update
p.Price.SetValue(14.99m);
p.Update(DataObjectLock.UpdateOption.IgnoreLock);

// Delete
p.Delete();
```

### 5. Service proxy — automatic connection & transaction management

Implement `IService` on your class alongside a service interface. Castle DynamicProxy handles
open → begin → commit → close with no virtual methods or framework coupling required.

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;
using Turquoise.ORM.Transactions;

// ── Interface (consumed by controllers / other services)
public interface IOrderService
{
    Order GetById(int id);
    void  Ship(int orderId);
}

// ── Implementation — IService triggers auto-registration
public class OrderService : IOrderService, IService
{
    private readonly DataConnection _conn;
    public OrderService(DataConnection conn) { _conn = conn; }

    [ConnectionScope]               // connection opens before, closes after
    public Order GetById(int id) { ... }

    [ConnectionScope]               // connection opens
    [Transaction]                   // transaction wraps the body
    public void Ship(int orderId)
    {
        var order = new Order(_conn);
        order.ID.SetValue(orderId);
        _conn.Read(order);
        order.Status.SetValue("Shipped");
        order.Update(DataObjectLock.UpdateOption.IgnoreLock);
        // commit on success; rollback + connection close on exception
    }
}

// Register — auto-scan picks up OrderService, registers as IOrderService:
builder.Services
    .AddTurquoiseSqlServer("Server=...;...")
    .AddServices(typeof(Program).Assembly);

// Inject by interface — proxy is transparent:
public class CheckoutController : ControllerBase
{
    public CheckoutController(IOrderService orders) { _orders = orders; }
    [HttpPost("{id}/ship")]
    public IActionResult Ship(int id) { _orders.Ship(id); return NoContent(); }
}
```

Manual usage (standalone, no DI):

```csharp
var conn = new SqlServerConnection("...");
var uow  = new SqlServerUnitOfWork(conn);
var svc  = TurquoiseServiceFactory.Create(new OrderService(conn), conn, uow);
svc.Ship(42);

// Or With.Transaction for ad-hoc work:
With.Transaction(uow, () =>
{
    order.Status.SetValue("Shipped");
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);
    shipment.Insert();
});
```

---

## Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Step-by-step tutorial |
| [Field Types](docs/field-types.md) | All `TField` types and their operators |
| [Query Builder](docs/query-builder.md) | Composing WHERE, ORDER BY, and pagination |
| [Transactions](docs/transactions.md) | Manual transaction and action queue patterns |
| [Unit of Work](docs/unit-of-work.md) | `IUnitOfWork`, `With.Transaction`, Castle interceptor |
| [LINQ Querying](docs/linq-querying.md) | `conn.Query<T>()` LINQ support |
| [Field Subsets](docs/field-subsets.md) | Partial fetches and partial updates |
| [DI & Service Proxies](docs/di-service-proxies.md) | `AddTurquoise*`, `AddTurquoiseService<T>`, `[ConnectionScope]`, `TurquoiseServiceFactory` |
| [Advanced](docs/advanced.md) | Encryption, custom mappers, polymorphism |
| [**Wiki**](docs/wiki.md) | Comprehensive reference — all concepts with examples |

---

## Project Layout

```
Turquoise.ORM/
├── src/
│   ├── Turquoise.ORM/                  ← Core library (provider-agnostic)
│   │   ├── Attributes/                 ← [Table], [Column], [Identity], etc.
│   │   ├── Adapters/                   ← Abstract adapter interfaces
│   │   ├── Fields/                     ← TString, TInt, TDecimal, ... (25+ types)
│   │   ├── Linq/                       ← LINQ query support
│   │   ├── Query/                      ← QueryTerm, SortOrder, EqualTerm, ...
│   │   └── Transactions/               ← IUnitOfWork, UnitOfWorkBase, With, ConnectionScopeInterceptor,
│   │                                      TransactionInterceptor, TurquoiseServiceFactory
│   ├── Turquoise.ORM.SqlServer/        ← SQL Server provider
│   │   ├── Adapters/                   ← SqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Extensions/                 ← AddTurquoiseSqlServer()
│   │   ├── Transactions/               ← SqlServerUnitOfWork
│   │   └── SqlServerConnection.cs
│   ├── Turquoise.ORM.PostgreSQL/       ← PostgreSQL provider
│   │   ├── Adapters/                   ← NpgsqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Extensions/                 ← AddTurquoisePostgreSQL()
│   │   ├── Transactions/               ← PostgreSQLUnitOfWork
│   │   └── PostgreSQLConnection.cs
│   ├── Turquoise.ORM.MongoDB/          ← MongoDB provider
│   │   ├── Extensions/                 ← AddTurquoiseMongoDB()
│   │   ├── Internal/                   ← MongoMapper, MongoQueryTranslator, MongoTypeCache
│   │   ├── Transactions/               ← MongoUnitOfWork
│   │   └── MongoDataConnection.cs
│   └── Turquoise.ORM.SQLite/           ← SQLite provider
│       ├── Adapters/                   ← SQLiteAdapterCommand/Connection/Reader/Transaction
│       ├── Extensions/                 ← AddTurquoiseSQLite()
│       ├── Transactions/               ← SQLiteUnitOfWork
│       └── SQLiteConnection.cs
├── tests/
│   ├── Turquoise.ORM.Tests/            ← Core library tests        (314 tests)
│   ├── Turquoise.ORM.SqlServer.Tests/  ← SQL Server provider tests  (50 tests)
│   ├── Turquoise.ORM.PostgreSQL.Tests/ ← PostgreSQL provider tests  (52 tests)
│   ├── Turquoise.ORM.MongoDB.Tests/    ← MongoDB provider tests     (79 tests)
│   └── Turquoise.ORM.SQLite.Tests/     ← SQLite provider tests      (in-memory integration)
├── examples/
│   └── Turquoise.ORM.Examples/         ← Runnable console examples
└── docs/                               ← Documentation
```
