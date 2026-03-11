# ActiveForge ORM v1.0.0
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/CodeShayk/ActiveForge/blob/master/LICENSE.md) 
[![GitHub Release](https://img.shields.io/github/v/release/CodeShayk/ActiveForge?logo=github&sort=semver)](https://github.com/CodeShayk/ActiveForge/releases/latest)
[![master-build](https://github.com/CodeShayk/ActiveForge/actions/workflows/master-build.yml/badge.svg)](https://github.com/CodeShayk/ActiveForge/actions/workflows/master-build.yml)
[![master-codeql](https://github.com/CodeShayk/ActiveForge/actions/workflows/master-codeql.yml/badge.svg)](https://github.com/CodeShayk/ActiveForge/actions/workflows/master-codeql.yml)

A lightweight, Active Record-style ORM for .NET 8, with first-class support for SQL Server, PostgreSQL, and MongoDB.

---

## Packages

| Package | Description |
|---------|-------------|
| `ActiveForge` | Core — entities, fields, predicates, LINQ, transactions, adapters, Castle proxy factory |
| `ActiveForge.SqlServer` | SQL Server provider — `SqlServerConnection`, ADO.NET adapters, `SqlServerUnitOfWork`, DI extensions |
| `ActiveForge.PostgreSQL` | PostgreSQL provider — `PostgreSQLConnection`, Npgsql adapters, `PostgreSQLUnitOfWork`, DI extensions |
| `ActiveForge.MongoDB` | MongoDB provider — `MongoDataConnection`, BSON mapping, `MongoUnitOfWork`, DI extensions |
| `ActiveForge.SQLite` | SQLite provider — `SQLiteConnection`, Microsoft.Data.Sqlite adapters, `SQLiteUnitOfWork`, DI extensions |

All connection types live in the `ActiveForge` namespace, so a single `using ActiveForge;` is sufficient regardless of the provider chosen.

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
- **Unit of Work** — `IUnitOfWork`, `UnitOfWorkBase`, provider-specific implementations, `With.Transaction`, `[Transaction]` attribute, Castle DynamicProxy interceptor. `[Transaction]` — wraps a service method in a `IUnitOfWork` transaction via Castle DynamicProxy; combine with `[ConnectionScope]` for the full open → begin → commit → close lifecycle
- **Connection-level lifecycle** — set `conn.UnitOfWork = uow` once; every write operation (`Insert`, `Update`, `Delete`, `ProcessActionQueue`, `ExecStoredProcedure`) automatically opens the connection, begins a transaction, commits, and closes — no proxy required; coordinates transparently with `[ConnectionScope]` via `IsOpen`. `[ConnectionScope]` — marks service methods/classes where the `DataConnection` is opened before and closed after; nested calls share one connection via `IsOpen` state.
- **Action queue** — batch operations via `QueueForInsert` / `QueueForUpdate` / `QueueForDelete` → `ProcessActionQueue`

### 🌐 DI & Service Proxy Integration
- **Auto-scan registration** — `AddActiveForgeSqlServer(...).AddServices(assembly)` discovers all `IService` implementations and registers them as interface-proxied scoped services in one call. `IService` marker — implement on any service class for automatic discovery and proxy registration. `IActiveForgeBuilder` — fluent builder returned by all `AddTurquoise*` methods; chain `.AddServices()`, `.AddService<I, T>()` for granular control.

---

## Requirements

- .NET 8.0
- One provider package:
  - SQL Server → `ActiveForge.SqlServer` (wraps `Microsoft.Data.SqlClient` 5.2.1)
  - PostgreSQL → `ActiveForge.PostgreSQL` (wraps `Npgsql` 8.0.3)
  - MongoDB → `ActiveForge.MongoDB` (wraps `MongoDB.Driver` 2.28.0)
  - SQLite → `ActiveForge.SQLite` (wraps `Microsoft.Data.Sqlite` 8.0.0)

---

## Quick Start

### 1. Connect (standalone)

```csharp
using ActiveForge;

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
The provider-specific `AddActiveForge*` call registers the connection + UoW and returns an `IActiveForgeBuilder`.
Chain `.AddServices()` to auto-scan your assembly for `IService` implementations.

```csharp
// Program.cs — choose one provider, then scan for IService implementations:
builder.Services
    .AddActiveForgeSqlServer(
        "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddActiveForgePostgreSQL("Host=localhost;Database=demo;Username=app;Password=secret;")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddActiveForgeMongoDB("mongodb://localhost:27017", "demo")
    .AddServices(typeof(Program).Assembly);

builder.Services
    .AddActiveForgeSQLite("Data Source=app.db")
    .AddServices(typeof(Program).Assembly);
```

### 3. Define entities

Entity classes are provider-agnostic — the same class works with SQL Server, PostgreSQL, MongoDB, and SQLite.

```csharp
using ActiveForge;
using ActiveForge.Attributes;

[Table("categories")]
public class Category : IdentityRecord
{
    [Column("name")] public TString Name = new TString();

    public Category() { }
    public Category(DataConnection conn) : base(conn) { }
}

[Table("products")]
public class Product : IdentityRecord
{
    [Column("name")]        public TString     Name       = new TString();
    [Column("price")]       public TDecimal    Price      = new TDecimal();
    [Column("in_stock")]
    [DefaultValue(true)]    public TBool       InStock    = new TBool();
    [Column("created_at")]
    [ReadOnly]              public TDateTime   CreatedAt  = new TDateTime();
    [Column("notes")]
    [NoPreload]             public TString     Notes      = new TString();
    [Column("CategoryID")]  public TForeignKey CategoryID = new TForeignKey();

    // Embedded Record — triggers automatic INNER JOIN in queries
    public Category Category;

    public Product()                          { Category = new Category(); }
    public Product(DataConnection conn) : base(conn) { Category = new Category(conn); }
}
```

> **Naming conventions:** PostgreSQL folds unquoted identifiers to lower-case — use lower-case `[Table]` and `[Column]` values. MongoDB uses the attribute values as BSON field and collection names verbatim.
>
> **Key attributes:** `[ReadOnly]` — included in SELECT but never written. `[NoPreload]` — excluded from the default SELECT; include via `FieldSubset`. `[DefaultValue]` — pre-populates the field on construction. `[Sensitive]` — masks values in diagnostic output. `[Encrypted]` — transparent encrypt/decrypt at ORM layer.

### 4. CRUD

```csharp
using ActiveForge.Query;
using ActiveForge.Linq;

// ── INSERT ────────────────────────────────────────────────────────────────────
var p = new Product(conn);
p.Name.SetValue("Widget");
p.Price.SetValue(9.99m);
p.InStock.SetValue(true);
p.Insert();   // p.ID is populated automatically after insert

// ── READ by primary key ───────────────────────────────────────────────────────
var p2 = new Product(conn);
p2.ID.SetValue(1);
p2.Read();   // throws PersistenceException if not found

// ── QUERY (QueryTerm API) ─────────────────────────────────────────────────────
var template = new Product(conn);
var inStock  = new EqualTerm(template, template.InStock, true);
var byName   = new OrderAscending(template, template.Name);
var results  = conn.QueryAll(template, inStock, byName, 0, null);

// ── QUERY (LINQ) ──────────────────────────────────────────────────────────────
List<Product> page = conn.Query(new Product(conn))
    .Where(x => x.InStock == true && x.Price < 50m)
    .OrderBy(x => x.Name)
    .Skip(0).Take(20)
    .ToList();

// ── QUERY with JOIN filter ────────────────────────────────────────────────────
List<Product> electronics = conn.Query(new Product(conn))
    .Where(x => x.Category.Name == "Electronics")
    .OrderBy(x => x.Price)
    .ToList();

// ── UPDATE ────────────────────────────────────────────────────────────────────
p.Price.SetValue(14.99m);
p.Update(RecordLock.UpdateOption.IgnoreLock);   // update all columns

p.Notes.SetValue("On sale");
p.UpdateChanged();                              // update only changed columns

// ── DELETE ────────────────────────────────────────────────────────────────────
p.Delete();                                     // delete by PK

// Delete by predicate:
var disc = new EqualTerm(template, template.InStock, false);
template.Delete(disc);
```

### 5. Service proxy — automatic connection & transaction management

Implement `IService` on your class alongside a service interface. Castle DynamicProxy handles
open → begin → commit → close with no virtual methods or framework coupling required.

```csharp
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Transactions;

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
        order.Update(RecordLock.UpdateOption.IgnoreLock);
        // commit on success; rollback + connection close on exception
    }
}

// Register — auto-scan picks up OrderService, registers as IOrderService:
builder.Services
    .AddActiveForgeSqlServer("Server=...;...")
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
var svc  = ActiveForgeServiceFactory.Create(new OrderService(conn), conn, uow);
svc.Ship(42);

// Or With.Transaction for ad-hoc work:
With.Transaction(uow, () =>
{
    order.Status.SetValue("Shipped");
    order.Update(RecordLock.UpdateOption.IgnoreLock);
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
| [DI & Service Proxies](docs/di-service-proxies.md) | `AddActiveForge*`, `AddActiveForgeService<T>`, `[ConnectionScope]`, `ActiveForgeServiceFactory` |
| [Advanced](docs/advanced.md) | Encryption, custom mappers, polymorphism |
| [**Wiki**](docs/wiki.md) | Comprehensive reference — all concepts with examples |

---

## Project Layout

```
ActiveForge/
├── src/
│   ├── ActiveForge/                  ← Core library (provider-agnostic)
│   │   ├── Attributes/                 ← [Table], [Column], [Identity], etc.
│   │   ├── Adapters/                   ← Abstract adapter interfaces
│   │   ├── Fields/                     ← TString, TInt, TDecimal, ... (25+ types)
│   │   ├── Linq/                       ← LINQ query support
│   │   ├── Query/                      ← QueryTerm, SortOrder, EqualTerm, ...
│   │   └── Transactions/               ← IUnitOfWork, UnitOfWorkBase, With, ConnectionScopeInterceptor,
│   │                                      TransactionInterceptor, ActiveForgeServiceFactory
│   ├── ActiveForge.SqlServer/        ← SQL Server provider
│   │   ├── Adapters/                   ← SqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Extensions/                 ← AddActiveForgeSqlServer()
│   │   ├── Transactions/               ← SqlServerUnitOfWork
│   │   └── SqlServerConnection.cs
│   ├── ActiveForge.PostgreSQL/       ← PostgreSQL provider
│   │   ├── Adapters/                   ← NpgsqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Extensions/                 ← AddActiveForgePostgreSQL()
│   │   ├── Transactions/               ← PostgreSQLUnitOfWork
│   │   └── PostgreSQLConnection.cs
│   ├── ActiveForge.MongoDB/          ← MongoDB provider
│   │   ├── Extensions/                 ← AddActiveForgeMongoDB()
│   │   ├── Internal/                   ← MongoMapper, MongoQueryTranslator, MongoTypeCache
│   │   ├── Transactions/               ← MongoUnitOfWork
│   │   └── MongoDataConnection.cs
│   └── ActiveForge.SQLite/           ← SQLite provider
│       ├── Adapters/                   ← SQLiteAdapterCommand/Connection/Reader/Transaction
│       ├── Extensions/                 ← AddActiveForgeSQLite()
│       ├── Transactions/               ← SQLiteUnitOfWork
│       └── SQLiteConnection.cs
├── tests/
│   ├── ActiveForge.Tests/            ← Core library tests        (340 tests)
│   ├── ActiveForge.SqlServer.Tests/  ← SQL Server provider tests  (80 tests)
│   ├── ActiveForge.PostgreSQL.Tests/ ← PostgreSQL provider tests  (81 tests)
│   ├── ActiveForge.MongoDB.Tests/    ← MongoDB provider tests    (126 tests)
│   └── ActiveForge.SQLite.Tests/     ← SQLite provider tests      (52 tests)
├── examples/
│   └── ActiveForge.Examples/         ← Runnable console examples
└── docs/                               ← Documentation
```
