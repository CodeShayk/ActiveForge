# Turquoise.ORM

A lightweight, Active Record-style ORM for .NET 8 targeting SQL Server.

## Features

- **Active Record pattern** — entities manage their own persistence
- **Type-safe field wrappers** — `TString`, `TInt`, `TDecimal`, `TPrimaryKey`, `TForeignKey`, and 25 more
- **Composable query predicates** — `EqualTerm`, `ContainsTerm`, `InTerm`, `ExistsTerm`, composed with `&`, `|`, `!`
- **Pagination** — `QueryPage` with `StartRecord`, `PageSize`, `IsMoreData`, `TotalRowCount`
- **Lazy streaming** — `LazyQueryAll<T>` streams rows without loading all into memory
- **Field subsets** — partial SELECTs and partial UPDATEs using `FieldSubset`
- **Transactions** — nested transaction support with `BeginTransaction` / `CommitTransaction` / `RollbackTransaction`
- **Action queue** — batch operations via `QueueForInsert` / `QueueForUpdate` / `QueueForDelete` then `ProcessActionQueue`
- **Field encryption** — transparent encrypt/decrypt via `[Encrypted]` attribute
- **Custom field mappers** — implement `IDBFieldMapper` for non-standard type conversions
- **Polymorphic mapping** — map abstract base types to concrete subtypes via `FactoryBase`

## Requirements

- .NET 8.0
- SQL Server (via `Microsoft.Data.SqlClient` 5.2.1)

## Quick Start

### 1. Create a table

```sql
CREATE TABLE Products (
    ID       INT            IDENTITY(1,1) PRIMARY KEY,
    Name     NVARCHAR(200)  NOT NULL,
    Price    DECIMAL(10,2)  NOT NULL,
    InStock  BIT            NOT NULL DEFAULT 1
);
```

### 2. Define an entity

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;

[Table("Products")]
public class Product : IdentDataObject
{
    [Column("Name")]    public TString  Name    = new TString();
    [Column("Price")]   public TDecimal Price   = new TDecimal();
    [Column("InStock")] public TBool    InStock = new TBool();

    public Product() { }
    public Product(DataConnection conn) : base(conn) { }
}
```

### 3. Connect and use

```csharp
using Turquoise.ORM;

var conn = new SqlServerConnection(
    "Server=.;Database=Demo;Integrated Security=True;TrustServerCertificate=True;",
    new FactoryBase());
conn.Connect();

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
| [Field Subsets](field-subsets.md) | Partial fetches and partial updates |
| [Advanced](advanced.md) | Encryption, custom mappers, polymorphism |

## Project Layout

```
Turquoise.ORM/
├── src/
│   └── Turquoise.ORM/
│       ├── Attributes/         ← [Table], [Column], [Identity], etc.
│       ├── Adapters/           ← Adapter interfaces + SQL Server impl
│       ├── Fields/             ← TString, TInt, TDecimal, ... (25+ types)
│       └── Query/              ← QueryTerm, SortOrder, EqualTerm, ...
├── tests/
│   └── Turquoise.ORM.Tests/    ← xUnit tests (232 tests)
├── examples/
│   └── Turquoise.ORM.Examples/ ← Runnable console examples
└── docs/                       ← This documentation
```
