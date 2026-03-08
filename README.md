# Turquoise.ORM

A lightweight, Active Record-style ORM for .NET 8, with support for SQL Server, PostgreSQL, and MongoDB.

---

## Packages

| Package | Description |
|---------|-------------|
| `Turquoise.ORM` | Core — entities, fields, predicates, LINQ, transactions, adapters (provider-agnostic) |
| `Turquoise.ORM.SqlServer` | SQL Server provider — `SqlServerConnection`, ADO.NET adapters, `SqlServerUnitOfWork` |
| `Turquoise.ORM.PostgreSQL` | PostgreSQL provider — `PostgreSQLConnection`, Npgsql adapters, `PostgreSQLUnitOfWork` |
| `Turquoise.ORM.MongoDB` | MongoDB provider — `MongoDataConnection`, BSON mapping, `MongoUnitOfWork` |

All connection types live in the `Turquoise.ORM` namespace, so a single `using Turquoise.ORM;` is sufficient regardless of the provider chosen.

---

## Features

- **Active Record pattern** — entities carry both data and persistence behaviour; no separate repository class required
- **Type-safe field wrappers** — `TString`, `TInt`, `TDecimal`, `TPrimaryKey`, `TForeignKey`, and 25+ more; each tracks null/loaded state and supports implicit conversion
- **Composable query predicates** — `EqualTerm`, `ContainsTerm`, `InTerm`, `GreaterThanTerm`, `LessThanTerm`, `IsNullTerm`, composed with `&`, `|`, `!`
- **LINQ query support** *(v1.1)* — `conn.Query<T>().Where(...).OrderBy(...).Take(...).Skip(...)` translated to native ORM predicates
- **Pagination** — `QueryPage` with `StartRecord`, `PageSize`, `IsMoreData`, `TotalRowCount`
- **Lazy streaming** — `LazyQueryAll<T>` streams rows without buffering the full result set
- **Field subsets** — partial SELECTs and partial UPDATEs via `FieldSubset`
- **Transactions** — manual nested transactions via `BeginTransaction` / `CommitTransaction` / `RollbackTransaction`
- **Unit of Work** *(v1.1)* — `IUnitOfWork`, `UnitOfWorkBase`, provider-specific implementations, `With.Transaction`, `[Transaction]` attribute, Castle DynamicProxy interceptor
- **Action queue** — batch operations via `QueueForInsert` / `QueueForUpdate` / `QueueForDelete` → `ProcessActionQueue`
- **Field encryption** — transparent encrypt/decrypt via `[Encrypted]` attribute
- **Custom field mappers** — implement `IDBFieldMapper` for non-standard type conversions
- **Polymorphic mapping** — map abstract base types to concrete subtypes via `FactoryBase`

---

## Requirements

- .NET 8.0
- One provider package:
  - SQL Server → `Turquoise.ORM.SqlServer` (wraps `Microsoft.Data.SqlClient` 5.2.1)
  - PostgreSQL → `Turquoise.ORM.PostgreSQL` (wraps `Npgsql` 8.0.3)
  - MongoDB → `Turquoise.ORM.MongoDB` (wraps `MongoDB.Driver` 2.28.0)

---

## Quick Start

### 1. Connect

```csharp
using Turquoise.ORM;

// SQL Server
var conn = new SqlServerConnection(
    "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;",
    new FactoryBase());

// PostgreSQL
var conn = new PostgreSQLConnection(
    "Host=localhost;Database=demo;Username=app;Password=secret;",
    new FactoryBase());

// MongoDB
var conn = new MongoDataConnection(
    "mongodb://localhost:27017",
    "demo");

conn.Connect();
```

### 2. Define an entity

Entity classes are provider-agnostic — the same class works with any connection type.

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

### 3. CRUD

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

### 4. Unit of Work

```csharp
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);  // or PostgreSQLUnitOfWork / MongoUnitOfWork

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
| [Unit of Work](docs/unit-of-work.md) | `IUnitOfWork`, `With.Transaction`, Castle interceptor *(v1.1)* |
| [LINQ Querying](docs/linq-querying.md) | `conn.Query<T>()` LINQ support *(v1.1)* |
| [Field Subsets](docs/field-subsets.md) | Partial fetches and partial updates |
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
│   │   └── Transactions/               ← IUnitOfWork, UnitOfWorkBase, With, interceptors
│   ├── Turquoise.ORM.SqlServer/        ← SQL Server provider
│   │   ├── Adapters/                   ← SqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Transactions/               ← SqlServerUnitOfWork
│   │   └── SqlServerConnection.cs
│   ├── Turquoise.ORM.PostgreSQL/       ← PostgreSQL provider
│   │   ├── Adapters/                   ← NpgsqlAdapterCommand/Connection/Reader/Transaction
│   │   ├── Transactions/               ← PostgreSQLUnitOfWork
│   │   └── PostgreSQLConnection.cs
│   └── Turquoise.ORM.MongoDB/          ← MongoDB provider
│       ├── Internal/                   ← MongoMapper, MongoQueryTranslator, MongoTypeCache
│       ├── Transactions/               ← MongoUnitOfWork
│       └── MongoDataConnection.cs
├── tests/
│   ├── Turquoise.ORM.Tests/            ← Core library tests        (314 tests)
│   ├── Turquoise.ORM.SqlServer.Tests/  ← SQL Server provider tests  (50 tests)
│   ├── Turquoise.ORM.PostgreSQL.Tests/ ← PostgreSQL provider tests  (52 tests)
│   └── Turquoise.ORM.MongoDB.Tests/   ← MongoDB provider tests     (79 tests)
├── examples/
│   └── Turquoise.ORM.Examples/         ← Runnable console examples
└── docs/                               ← Documentation
```
