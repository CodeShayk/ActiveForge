# Turquoise.ORM

A lightweight, Active Record-style ORM for .NET 8.

## Packages

| Package | Description |
|---------|-------------|
| `Turquoise.ORM` | Core library — entities, fields, predicates, LINQ, transactions, abstractions |
| `Turquoise.ORM.SqlServer` | SQL Server provider — `SqlServerConnection`, adapter implementations, `SqlServerUnitOfWork` |
| `Turquoise.ORM.PostgreSQL` | PostgreSQL provider — `PostgreSQLConnection`, adapter implementations, `PostgreSQLUnitOfWork` |
| `Turquoise.ORM.MongoDB` | MongoDB provider — `MongoDataConnection`, BSON mapping, `MongoUnitOfWork` |

## Features

- **Active Record pattern** — entities manage their own persistence
- **Type-safe field wrappers** — `TString`, `TInt`, `TDecimal`, `TPrimaryKey`, `TForeignKey`, and 25 more
- **Composable query predicates** — `EqualTerm`, `ContainsTerm`, `InTerm`, `ExistsTerm`, composed with `&`, `|`, `!`
- **LINQ query support** *(v1.1)* — `conn.Query<T>().Where(...).OrderBy(...).Take(...).Skip(...)` translated to native ORM predicates
- **Pagination** — `QueryPage` with `StartRecord`, `PageSize`, `IsMoreData`, `TotalRowCount`
- **Lazy streaming** — `LazyQueryAll<T>` streams rows without loading all into memory
- **Field subsets** — partial SELECTs and partial UPDATEs using `FieldSubset`
- **Transactions** — nested transaction support with `BeginTransaction` / `CommitTransaction` / `RollbackTransaction`
- **Unit of Work** *(v1.1)* — `IUnitOfWork`, `UnitOfWorkBase`, `SqlServerUnitOfWork`, `PostgreSQLUnitOfWork`, `With.Transaction`, `[Transaction]` attribute, Castle DynamicProxy interceptor
- **Action queue** — batch operations via `QueueForInsert` / `QueueForUpdate` / `QueueForDelete` then `ProcessActionQueue`
- **Field encryption** — transparent encrypt/decrypt via `[Encrypted]` attribute
- **Custom field mappers** — implement `IDBFieldMapper` for non-standard type conversions
- **Polymorphic mapping** — map abstract base types to concrete subtypes via `FactoryBase`

## Requirements

- .NET 8.0
- For SQL Server: `Turquoise.ORM.SqlServer` (wraps `Microsoft.Data.SqlClient` 5.2.1)
- For PostgreSQL: `Turquoise.ORM.PostgreSQL` (wraps `Npgsql` 8.0.3)
- For MongoDB: `Turquoise.ORM.MongoDB` (wraps `MongoDB.Driver` 2.28.0)

## Quick Start

### SQL Server

```csharp
using Turquoise.ORM;          // SqlServerConnection is in this namespace

var conn = new SqlServerConnection(
    "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;",
    new FactoryBase());
conn.Connect();
```

### PostgreSQL

```csharp
using Turquoise.ORM;          // PostgreSQLConnection is in this namespace

var conn = new PostgreSQLConnection(
    "Host=localhost;Database=demo;Username=app;Password=secret;",
    new FactoryBase());
conn.Connect();
```

### MongoDB

```csharp
using Turquoise.ORM;          // MongoDataConnection is in this namespace

var conn = new MongoDataConnection(
    "mongodb://localhost:27017",
    "myDatabase");
conn.Connect();
```

### Define an entity (provider-agnostic)

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;

[Table("products")]
public class Product : IdentDataObject
{
    [Column("name")]     public TString  Name     = new TString();
    [Column("price")]    public TDecimal Price    = new TDecimal();
    [Column("in_stock")] public TBool    InStock  = new TBool();

    public Product() { }
    public Product(DataConnection conn) : base(conn) { }
}
```

> **Note:** PostgreSQL folds unquoted identifiers to lower-case. Use lower-case `[Table]` and `[Column]` attribute values to match the default PostgreSQL naming convention.

### Use (same API for both providers)

```csharp
// Insert
var p = new Product(conn);
p.Name.SetValue("Widget");
p.Price.SetValue(9.99m);
p.InStock.SetValue(true);
p.Insert();

// Query
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, true);
var results = conn.QueryAll(template, term, null, 0, null);

// Update
p.Price.SetValue(14.99m);
p.Update(DataObjectLock.UpdateOption.IgnoreLock);

// Delete
p.Delete();
```

## Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Step-by-step tutorial |
| [Field Types](field-types.md) | All `TField` types and their operators |
| [Query Builder](query-builder.md) | Composing WHERE, ORDER BY, and pagination |
| [Transactions](transactions.md) | Transaction and action queue patterns |
| [Unit of Work](unit-of-work.md) | IUnitOfWork, With.Transaction, Castle interceptor *(v1.1)* |
| [LINQ Querying](linq-querying.md) | `conn.Query<T>()` LINQ support *(v1.1)* |
| [Field Subsets](field-subsets.md) | Partial fetches and partial updates |
| [Advanced](advanced.md) | Encryption, custom mappers, polymorphism |
| [**Wiki**](wiki.md) | Comprehensive reference — all concepts with examples |

## Project Layout

```
Turquoise.ORM/
├── src/
│   ├── Turquoise.ORM/                  ← Core library (provider-agnostic)
│   │   ├── Attributes/                 ← [Table], [Column], [Identity], etc.
│   │   ├── Adapters/                   ← Abstract adapter interfaces (ConnectionBase, etc.)
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
│       ├── Internal/                   ← MongoFieldDescriptor, MongoMapper, MongoQueryTranslator
│       ├── Transactions/               ← MongoUnitOfWork
│       └── MongoDataConnection.cs
├── tests/
│   ├── Turquoise.ORM.Tests/            ← Core library tests (314 tests)
│   ├── Turquoise.ORM.SqlServer.Tests/  ← SQL Server provider tests (50 tests)
│   ├── Turquoise.ORM.PostgreSQL.Tests/ ← PostgreSQL provider tests (52 tests)
│   └── Turquoise.ORM.MongoDB.Tests/   ← MongoDB provider tests (79 tests)
├── examples/
│   └── Turquoise.ORM.Examples/         ← Runnable console examples
└── docs/                               ← This documentation
```
