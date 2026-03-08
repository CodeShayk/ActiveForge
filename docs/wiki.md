# Turquoise.ORM — Comprehensive Developer Wiki

A complete reference for every concept in Turquoise.ORM: architecture, field types, querying, transactions, unit of work, LINQ support, and advanced features.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Entities — DataObject and IdentDataObject](#2-entities--dataobject-and-identdataobject)
3. [Field Types (TField System)](#3-field-types-tfield-system)
4. [DataConnection — The Gateway](#4-dataconnection--the-gateway)
5. [CRUD Operations](#5-crud-operations)
6. [Query Predicates (QueryTerm API)](#6-query-predicates-queryterm-api)
7. [Sorting and Pagination](#7-sorting-and-pagination)
8. [LINQ Query Support](#8-linq-query-support)
9. [Transactions (Manual API)](#9-transactions-manual-api)
10. [Unit of Work (IUnitOfWork)](#10-unit-of-work-iunitofwork)
11. [Action Queue (Batch Operations)](#11-action-queue-batch-operations)
12. [Field Subsets (Partial Fetch / Update)](#12-field-subsets-partial-fetch--update)
13. [Field Encryption](#13-field-encryption)
14. [Custom Field Mappers](#14-custom-field-mappers)
15. [Polymorphic Mapping (FactoryBase)](#15-polymorphic-mapping-factorybase)
16. [Optimistic Locking](#16-optimistic-locking)
17. [Lazy Streaming](#17-lazy-streaming)
18. [Raw SQL and Stored Procedures](#18-raw-sql-and-stored-procedures)
19. [Lookup / Cached Reference Tables](#19-lookup--cached-reference-tables)
20. [Architecture Deep Dive](#20-architecture-deep-dive)
21. [Quick Reference Cheat Sheet](#21-quick-reference-cheat-sheet)

---

## 1. Architecture Overview

Turquoise.ORM is a **lightweight Active Record ORM** for .NET 8 targeting SQL Server.

```
┌─────────────────────────────────────────────────────────────────┐
│  Your Application                                               │
│                                                                 │
│  DataObject subclass  ──── CRUD calls ────► DataConnection      │
│  (fields, business logic)                   (SqlServerConnection)│
│                                                 │               │
│  QueryTerm tree  ──────── query calls ──────────┤               │
│  LINQ (conn.Query<T>())  ─ translated ──────────┤               │
│                                                 ▼               │
│                                           ADO.NET / SQL Server  │
└─────────────────────────────────────────────────────────────────┘
```

**Core principles:**
- **Active Record** — `DataObject` instances know how to persist themselves via a shared `DataConnection`.
- **Type-safe fields** — every column is represented by a `TField` subclass, not a bare property. This tracks null/loaded state and enables predicate construction.
- **Composable predicates** — `QueryTerm` objects compose with C# `&`, `|`, `!` operators to build arbitrary WHERE clauses.
- **Connection-centric** — `DataConnection` is the single point of query execution; entities delegate to it.

---

## 2. Entities — DataObject and IdentDataObject

### 2.1 Base Classes

| Class | When to use |
|-------|-------------|
| `DataObject` | Tables without a single integer auto-identity primary key |
| `IdentDataObject` | Tables with an `INT IDENTITY(1,1)` primary key (exposed as `ID: TPrimaryKey`) |

### 2.2 Defining an Entity

```csharp
using Turquoise.ORM;
using Turquoise.ORM.Attributes;

[Table("Products")]
public class Product : IdentDataObject
{
    // Each public field maps to a column.
    // The [Column] attribute sets the DB column name.
    [Column("Name")]      public TString  Name      = new TString();
    [Column("Price")]     public TDecimal Price     = new TDecimal();
    [Column("InStock")]   public TBool    InStock   = new TBool();
    [Column("CreatedAt")] public TDateTime CreatedAt = new TDateTime();

    // Required: parameterless constructor for ORM hydration.
    public Product() { }

    // Constructor accepting a connection (used in application code).
    public Product(DataConnection conn) : base(conn) { }
}
```

**Conventions:**
- `[Table("TableName")]` — maps the class to a DB table.
- `[Column("ColumnName")]` — maps a field to a DB column (required).
- `[Identity]` — marks a field as auto-generated (implicit on `IdentDataObject.ID`).
- Fields are **public instance fields**, not properties. The ORM finds them via reflection.
- A no-arg constructor is mandatory; the ORM calls it when hydrating query results.

### 2.3 IdentDataObject.ID

`IdentDataObject` adds:

```csharp
[Column("ID")]
[Identity]
public TPrimaryKey ID = new TPrimaryKey();
```

After `Insert()`, `ID.GetValue()` returns the new auto-generated integer.

### 2.4 Embedded / Joined Objects

Embed another `DataObject` as a field to express a JOIN:

```csharp
[Table("OrderLines")]
public class OrderLine : IdentDataObject
{
    [Column("OrderID")]  public TForeignKey OrderID  = new TForeignKey();
    [Column("Qty")]      public TInt        Qty      = new TInt();
    [Column("Price")]    public TDecimal    UnitPrice = new TDecimal();

    // Embedded — triggers a JOIN in SELECT queries.
    public Order Order = new Order();

    public OrderLine() { }
    public OrderLine(DataConnection conn) : base(conn) { }
}
```

---

## 3. Field Types (TField System)

### 3.1 Common API

Every `TField` subtype shares this interface:

```csharp
// Read value
object  GetValue();           // returns the typed value, or null if IsNull()
object  GetRawValue();        // raw DB value (may differ for encrypted fields)

// Write value
void    SetValue(object value);
void    SetNull();

// State
bool    IsNull();             // true if value is SQL NULL
bool    IsLoaded();           // true if value was loaded from DB (or set)

// Copy
void    CopyFrom(TField other);
```

Many subtypes also expose **implicit conversions** so you can write:

```csharp
string name = product.Name;          // TString → string implicit cast
product.Name.SetValue("Widget");     // always works (explicit)
```

> **Note:** `TField.Value` is `protected`. Never assign `.Value =` directly from outside the class — use `SetValue()`.

### 3.2 Numeric Types

| Type | CLR type | DB type |
|------|----------|---------|
| `TInt` | `int` | INT |
| `TInt16` | `short` | SMALLINT |
| `TInt64` / `TLong` | `long` | BIGINT |
| `TByte` | `byte` | TINYINT |
| `TSByte` | `sbyte` | TINYINT (signed) |
| `TUInt` | `uint` | INT (unsigned) |
| `TUInt16` | `ushort` | SMALLINT (unsigned) |
| `TUInt64` | `ulong` | BIGINT (unsigned) |
| `TFloat` | `float` | REAL |
| `TDouble` | `double` | FLOAT |
| `TDecimal` | `decimal` | DECIMAL / NUMERIC |

```csharp
product.Price.SetValue(19.99m);
decimal price = (decimal)product.Price.GetValue();
// or via implicit conversion where available:
decimal price2 = product.Price;
```

### 3.3 String Types

| Type | Notes |
|------|-------|
| `TString` | `NVARCHAR` / `VARCHAR` |
| `TChar` | Single character |
| `THtmlString` | Same as TString, marks HTML content |
| `TIpAddress` | Stored as VARCHAR, parsed as IP |

```csharp
product.Name.SetValue("Widget");
string name = (string)product.Name.GetValue();

// Null check — use (TString)null to avoid ambiguity with == overloads:
var term = new EqualTerm(template, template.Name, (TString)null); // IS NULL
```

### 3.4 Key Types

| Type | Notes |
|------|-------|
| `TPrimaryKey` | Auto-identity integer PK; read-only after insert |
| `TForeignKey` | Integer FK referencing another table |

```csharp
int id = (int)order.ID.GetValue();          // primary key after insert
orderLine.OrderID.SetValue(id);              // set foreign key
```

### 3.5 Date / Time Types

| Type | CLR type | Notes |
|------|----------|-------|
| `TDateTime` | `DateTime` | Local or unspecified |
| `TUtcDateTime` | `DateTime` | Stored/read as UTC |
| `TLocalDateTime` | `DateTime` | Converted to local on read |
| `TDate` | `DateTime` (date only) | Time portion zeroed |
| `TUtcDate` | `DateTime` | Date in UTC |
| `TLocalDate` | `DateTime` | Date in local TZ |
| `TTime` | `TimeSpan` | Time-of-day |

```csharp
product.CreatedAt.SetValue(DateTime.UtcNow);
DateTime created = (DateTime)product.CreatedAt.GetValue();
```

### 3.6 Boolean and Binary

```csharp
// TBool — maps to SQL BIT
product.InStock.SetValue(true);
bool inStock = (bool)product.InStock.GetValue();

// TByteArray — maps to VARBINARY / IMAGE
attachment.Data.SetValue(File.ReadAllBytes("file.pdf"));
byte[] bytes = (byte[])attachment.Data.GetValue();

// TGuid — maps to UNIQUEIDENTIFIER
record.ExternalId.SetValue(Guid.NewGuid());
Guid id = (Guid)record.ExternalId.GetValue();
```

### 3.7 Null Handling

```csharp
product.Name.SetNull();              // explicitly set to NULL
if (product.Name.IsNull()) { ... }   // check for NULL
if (!product.Name.IsLoaded()) { ... } // never set at all
```

---

## 4. DataConnection — The Gateway

`DataConnection` is the abstract base; `SqlServerConnection` is the concrete SQL Server implementation.

### 4.1 Creating a Connection

```csharp
using Turquoise.ORM;

var factory = new ShopFactory();   // your FactoryBase subclass (can be FactoryBase if no polymorphism)
var conn = new SqlServerConnection(
    "Server=.;Database=MyDB;Integrated Security=True;TrustServerCertificate=True;",
    factory);
conn.Connect();
```

### 4.2 Lifecycle

```csharp
conn.Connect();     // opens the ADO.NET connection
conn.Disconnect();  // closes it
```

### 4.3 Factory Pattern

Pass a `FactoryBase` to control how the ORM instantiates objects. The default `FactoryBase` uses `Activator.CreateInstance`. Override `Create(Type)` for polymorphic mapping (see [§15](#15-polymorphic-mapping-factorybase)).

---

## 5. CRUD Operations

### 5.1 Insert

```csharp
var product = new Product(conn);
product.Name.SetValue("Widget");
product.Price.SetValue(9.99m);
product.InStock.SetValue(true);
product.CreatedAt.SetValue(DateTime.UtcNow);

product.Insert();                    // executes INSERT; product.ID is populated

Console.WriteLine(product.ID.GetValue()); // e.g. 42
```

Alternatively, use `conn.Insert(product)`.

### 5.2 Read (by Primary Key)

```csharp
var product = new Product(conn);
product.ID.SetValue(42);
product.Read();

Console.WriteLine(product.Name.GetValue()); // "Widget"
```

Alternatively, use `conn.Read(product)`.

### 5.3 Update

```csharp
// Update all mapped columns:
product.Price.SetValue(14.99m);
product.Update();

// Update only changed columns (requires initial snapshot):
product.Price.SetValue(19.99m);
product.UpdateChanged();

// With locking options:
product.Update(DataObjectLock.UpdateOption.IgnoreLock);
```

### 5.4 Delete

```csharp
product.Delete();                    // deletes by PK

// Delete by predicate:
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.Delete(term);               // DELETE WHERE InStock = 0
```

### 5.5 ReadForUpdate (Advisory Lock)

```csharp
product.ID.SetValue(1);
product.ReadForUpdate();             // SELECT ... WITH (UPDLOCK)
product.Price.SetValue(29.99m);
product.Update();
```

---

## 6. Query Predicates (QueryTerm API)

### 6.1 Equality

```csharp
var template = new Product(conn);
var term = new EqualTerm(template, template.Name, "Widget");
// → WHERE Name = @Name
```

### 6.2 Comparisons

```csharp
new GreaterThanTerm(template, template.Price, 50m)     // >
new GreaterOrEqualTerm(template, template.Price, 50m)  // >=
new LessThanTerm(template, template.Price, 10m)        // <
new LessOrEqualTerm(template, template.Price, 10m)     // <=
```

### 6.3 String Matching

```csharp
new LikeTerm(template, template.Name, "%widget%")       // LIKE
new ContainsTerm(template, template.Name, "widget")     // LIKE '%widget%' (built-in)
```

### 6.4 Null Checks

```csharp
new IsNullTerm(template, template.Name)     // IS NULL
!new IsNullTerm(template, template.Name)   // IS NOT NULL (via NotTerm)
```

### 6.5 IN Clause

```csharp
var ids = new List<int> { 1, 2, 3 };
new InTerm(template, template.ID, ids)
// → WHERE ID IN (@IN_ID0, @IN_ID1, @IN_ID2)
```

### 6.6 Full-Text Search

```csharp
new FullTextTerm(template, template.Name, "widget NEAR gadget")
// → WHERE CONTAINS(Name, 'widget NEAR gadget')
```

### 6.7 EXISTS Sub-query

```csharp
// "Products that have at least one OrderLine"
var orderLine = new OrderLine(conn);
var subQuery = conn.Query<OrderLine>()
    .Where(ol => ol.OrderID == order.ID);  // LINQ style

// Or using the classic Query<T> builder:
var exists = new ExistsTerm(
    template,
    conn.Query<OrderLine>(orderLine)
        .Where(new EqualTerm(orderLine, orderLine.OrderID, template.ID)));
```

### 6.8 Raw SQL Predicate

```csharp
new RawSqlTerm("Price BETWEEN 10 AND 50")
// Injected verbatim — use sparingly; prefer parameterized terms.
```

### 6.9 Logical Composition

```csharp
var inStock = new EqualTerm(template, template.InStock, true);
var cheap   = new LessThanTerm(template, template.Price, 20m);
var named   = new ContainsTerm(template, template.Name, "widget");

// AND
QueryTerm both   = inStock & cheap;

// OR
QueryTerm either = inStock | cheap;

// NOT
QueryTerm notIn  = !inStock;

// Complex
QueryTerm complex = (inStock & cheap) | (named & !inStock);
```

### 6.10 Executing Queries

```csharp
var template = new Product(conn);

// All matching rows (loads into memory):
ObjectCollection results = conn.QueryAll(template, term, sortOrder, 0, null);

// First match only:
DataObject first = conn.QueryFirst(template, term, sortOrder);

// Count:
int count = conn.QueryCount(template, term);

// Page:
var page = conn.QueryPage(template, term, sortOrder, startRecord: 0, pageSize: 20);
// page.StartRecord, page.PageSize, page.IsMoreData, page.TotalRowCount

// Lazy stream (no memory buffer):
IEnumerable<Product> stream = conn.LazyQueryAll<Product>(template, term, sortOrder);
```

---

## 7. Sorting and Pagination

### 7.1 Sort Orders

```csharp
var template = new Product(conn);

SortOrder byName      = new OrderAscending(template, template.Name);
SortOrder byPriceDesc = new OrderDescending(template, template.Price);

// Compose multiple sorts:
// Use CombinedSortOrder (Linq namespace) or pass the first to QueryAll
// and rely on secondary ORDER BY via SQL.
```

### 7.2 QueryPage

```csharp
QueryPage page = conn.QueryPage(
    template,
    new EqualTerm(template, template.InStock, true),
    new OrderAscending(template, template.Name),
    startRecord: 0,
    pageSize: 20);

Console.WriteLine($"Page 1: {page.Count} items, more={page.IsMoreData}, total={page.TotalRowCount}");

// Next page:
page = conn.QueryPage(template, term, sort, startRecord: 20, pageSize: 20);
```

---

## 8. LINQ Query Support

*(Requires `using Turquoise.ORM.Linq;`)*

### 8.1 Entry Point

```csharp
IQueryable<Product> q = conn.Query<Product>();
```

Internally creates a template instance and wraps it in `OrmQueryable<T>`. The database is NOT queried until you enumerate.

### 8.2 Where (Predicates)

```csharp
// Equality / inequality
conn.Query<Product>().Where(p => p.Name == "Widget").ToList();
conn.Query<Product>().Where(p => p.Name != "Widget").ToList();

// Comparisons
conn.Query<Product>().Where(p => p.Price >= 50m).ToList();
conn.Query<Product>().Where(p => p.Price <  10m).ToList();

// Null checks (cast null explicitly to avoid ambiguity)
conn.Query<Product>().Where(p => p.Name == (TString)null).ToList();  // IS NULL
conn.Query<Product>().Where(p => p.Name != (TString)null).ToList();  // IS NOT NULL

// Logical AND / OR / NOT
conn.Query<Product>().Where(p => p.InStock == true && p.Price > 20m).ToList();
conn.Query<Product>().Where(p => p.Price < 5m  || p.Price > 100m).ToList();
conn.Query<Product>().Where(p => !(p.InStock == true)).ToList();

// IN clause — use List<T>, NOT arrays (array causes MemoryExtensions ambiguity)
var names = new List<string> { "Widget", "Gadget", "Gizmo" };
conn.Query<Product>().Where(p => names.Contains(p.Name)).ToList();

// Local variable capture (evaluated at translation time)
string target  = "Widget";
decimal minP   = 10m;
conn.Query<Product>().Where(p => p.Name == target && p.Price >= minP).ToList();
```

### 8.3 Chained Where (auto-ANDed)

```csharp
conn.Query<Product>()
    .Where(p => p.InStock == true)
    .Where(p => p.Price   >  10m)
    .Where(p => p.Name    != "Discontinued")
    .ToList();
// Equivalent to: WHERE InStock = 1 AND Price > 10 AND Name <> 'Discontinued'
```

### 8.4 Sorting

```csharp
conn.Query<Product>().OrderBy(p => p.Name).ToList();
conn.Query<Product>().OrderByDescending(p => p.Price).ToList();

// Multi-column:
conn.Query<Product>()
    .OrderBy(p => p.Name)
    .ThenByDescending(p => p.Price)
    .ToList();
```

### 8.5 Pagination

```csharp
// Page 2, 20 items per page:
conn.Query<Product>()
    .Where(p => p.InStock == true)
    .OrderBy(p => p.Name)
    .Skip(20)
    .Take(20)
    .ToList();
```

When `Take` or `Skip` is set, `QueryPage` is used internally.
Without `Skip`/`Take`, `LazyQueryAll` is used (memory-efficient streaming).

### 8.6 Full Chain

```csharp
using Turquoise.ORM.Linq;

var featured = new List<string> { "Widget", "Gadget" };
decimal min  = 5m;

List<Product> results = conn.Query<Product>()
    .Where(p => featured.Contains(p.Name) || p.Price > min)
    .Where(p => p.InStock == true)
    .OrderBy(p => p.Price)
    .ThenBy(p => p.Name)
    .Skip(0)
    .Take(10)
    .ToList();
```

### 8.7 Supported Operators

| LINQ | Generated Term | Notes |
|------|----------------|-------|
| `x.F == v` | `EqualTerm` | |
| `x.F != v` | `!EqualTerm` | |
| `x.F > v` | `GreaterThanTerm` | |
| `x.F >= v` | `GreaterOrEqualTerm` | |
| `x.F < v` | `LessThanTerm` | |
| `x.F <= v` | `LessOrEqualTerm` | |
| `x.F == null` | `IsNullTerm` | Cast null: `(TString)null` |
| `x.F != null` | `!IsNullTerm` | |
| `a && b` | `AndTerm` | |
| `a \|\| b` | `OrTerm` | |
| `!a` | `NotTerm` | |
| `list.Contains(x.F)` | `InTerm` | Use `List<T>`, not arrays |
| `OrderBy` | `OrderAscending` | |
| `OrderByDescending` | `OrderDescending` | |
| `ThenBy` | Appended ascending | |
| `ThenByDescending` | Appended descending | |
| `Take(n)` | `pageSize` | |
| `Skip(n)` | `startRecord` | |

### 8.8 Limitations

| Limitation | Workaround |
|------------|-----------|
| No `GroupBy` | Use raw SQL (`ExecSQL`) |
| No `Join` | Use embedded `DataObject` fields |
| No `Select` projection | Use `FieldSubset` on template |
| No `Count()`, `First()` | Use `conn.QueryCount()`, `conn.QueryFirst()` |
| No async | Async planned for a future release |

### 8.9 Mixing LINQ with QueryTerm

```csharp
OrmQueryable<Product> orm = (OrmQueryable<Product>)conn.Query<Product>()
    .Where(p => p.InStock == true);

// Combine accumulated term with additional classic terms:
var template = orm.Template;
QueryTerm combined = orm.WhereTerm & new ContainsTerm(template, template.Name, "premium");

// Execute via classic API:
var results = conn.QueryAll(template, combined, null, 0, null);
```

---

## 9. Transactions (Manual API)

### 9.1 Explicit Transaction

```csharp
conn.BeginTransaction();
try
{
    product.Insert();
    orderLine.Insert();
    conn.CommitTransaction();
}
catch
{
    conn.RollbackTransaction();
    throw;
}
```

### 9.2 Isolation Levels

```csharp
using System.Data;

conn.BeginTransaction(IsolationLevel.Serializable);
// ... work ...
conn.CommitTransaction();
```

Available: `ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`, `Serializable`, `Snapshot`.

### 9.3 Nested Transactions

Turquoise uses a **depth counter** internally. Outer `BeginTransaction` starts the real ADO.NET transaction; inner calls increment depth only:

```csharp
conn.BeginTransaction();            // depth: 0→1, real tx starts
    conn.BeginTransaction();        // depth: 1→2, reuses same tx
    conn.CommitTransaction();       // depth: 2→1, no DB commit yet
conn.CommitTransaction();           // depth: 1→0, real COMMIT
```

If any inner scope calls `RollbackTransaction()`, the entire outer transaction will roll back when it unwinds.

### 9.4 TransactionState

```csharp
TransactionState state = conn.TransactionState();
// returns: None, Active, Committed, Aborted
```

---

## 10. Unit of Work (IUnitOfWork)

*(Namespace: `Turquoise.ORM.Transactions`; requires Castle.Core NuGet for interceptors)*

### 10.1 Overview

`IUnitOfWork` wraps a `DataConnection`'s transaction lifecycle behind a clean abstraction, enabling:
- **`With.Transaction`** functional helper
- **`[Transaction]`** attribute-based automatic interception via Castle DynamicProxy
- **Ambient registration** via `TurquoiseServiceLocator`

### 10.2 IUnitOfWork Interface

```csharp
public interface IUnitOfWork : IDisposable
{
    bool InTransaction { get; }
    TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    void Commit();
    void Rollback();
}
```

### 10.3 SqlServerUnitOfWork

```csharp
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);
```

Wraps the underlying `SqlServerConnection` and delegates `BeginTransaction` to it.

### 10.4 With.Transaction

```csharp
// Action overload — commits on success, rolls back on exception:
With.Transaction(uow, () =>
{
    product.Insert();
    orderLine.Insert();
});

// Func<T> overload — returns a value:
int newId = With.Transaction(uow, () =>
{
    product.Insert();
    return (int)product.ID.GetValue();
});

// Specify isolation level:
With.Transaction(uow, () => { ... }, IsolationLevel.Serializable);

// Shorthands:
With.SerializableTransaction(uow, () => { ... });
With.RepeatableReadTransaction(uow, () => { ... });
With.SnapshotTransaction(uow, () => { ... });
```

### 10.5 Nested Transactions

The depth counter is managed by `UnitOfWorkBase`. Inner `With.Transaction` calls reuse the existing ADO.NET transaction:

```csharp
With.Transaction(uow, () =>         // depth 0→1, real tx begins
{
    product.Insert();

    With.Transaction(uow, () =>     // depth 1→2, reuses tx
    {
        orderLine.Insert();
    });                             // depth 2→1

});                                 // depth 1→0, COMMIT
```

**Rollback semantics:** If an inner scope rolls back (exception), `_rollbackOnly` is set. When the outermost scope tries to commit, it rolls back instead.

### 10.6 [Transaction] Attribute

Decorate virtual methods with `[Transaction]` to have them automatically wrapped in a transaction when proxied:

```csharp
public class ProductService
{
    protected readonly SqlServerConnection _conn;
    public ProductService(SqlServerConnection conn) { _conn = conn; }

    [Transaction(IsolationLevel.ReadCommitted)]
    public virtual int CreateProduct(string name, decimal price)
    {
        var p = new Product(_conn);
        p.Name.SetValue(name);
        p.Price.SetValue(price);
        p.InStock.SetValue(true);
        p.CreatedAt.SetValue(DateTime.UtcNow);
        _conn.Insert(p);
        return (int)p.ID.GetValue();
    }

    // No [Transaction] — passes through without starting a transaction.
    public virtual int CountProducts()
        => _conn.QueryCount(new Product(_conn));
}
```

- `[Transaction]` can be placed at **method level** or **class level** (applies to all virtual methods).
- Methods without the attribute are passed through unchanged.
- The intercepted method must be **virtual** (required by Castle DynamicProxy).

### 10.7 Setting Up Castle DynamicProxy Interception

#### For Arbitrary Service Classes

```csharp
using Castle.DynamicProxy;

using IUnitOfWork uow  = new SqlServerUnitOfWork(conn);
var interceptor         = new TransactionInterceptor(uow);
var generator           = new ProxyGenerator();

ProductService real  = new ProductService(conn);
ProductService proxy = (ProductService)generator.CreateClassProxyWithTarget(
    typeof(ProductService), real, interceptor);

int id = proxy.CreateProduct("Widget", 9.99m);  // transaction committed automatically
```

#### For DataConnection Subclasses (C1 Strategy)

```csharp
// Proxy the connection itself so every virtual method on it gets intercepted:
SqlServerConnection proxied =
    DataConnectionProxyFactory.Create<SqlServerConnection>(conn, uow);
```

### 10.8 TurquoiseServiceLocator (Ambient DI)

Register a factory so `With.Transaction()` (no UoW argument) can resolve the UoW:

```csharp
// Register a factory:
TurquoiseServiceLocator.SetUnitOfWorkFactory(() => new SqlServerUnitOfWork(conn));

// Or register a full IServiceProvider:
TurquoiseServiceLocator.SetProvider(serviceProvider);

// Use without explicit UoW argument:
With.Transaction(() =>
{
    product.Insert();
});

// Reset (e.g., in tests):
TurquoiseServiceLocator.Reset();
```

### 10.9 Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Exception inside `With.Transaction` | `Rollback()` called; exception re-thrown |
| Inner scope rolls back | `_rollbackOnly` set; outer scope will roll back even if it tries to commit |
| `Dispose()` with open transaction | Transaction is rolled back automatically |
| `Commit()` when `_rollbackOnly` | Silently converts to `Rollback()` |
| `CreateTransaction()` when already active and same isolation level | Depth incremented; no new ADO.NET transaction |

---

## 11. Action Queue (Batch Operations)

The action queue batches DML without executing immediately:

```csharp
// Queue operations:
product1.QueueForInsert();
product2.QueueForUpdate();
product3.QueueForDelete();

// Execute all queued operations in a single round-trip batch:
conn.ProcessActionQueue();

// Discard without executing:
conn.ClearActionQueue();
```

**Use case:** High-throughput imports, bulk updates, or deferred persistence.

---

## 12. Field Subsets (Partial Fetch / Update)

`FieldSubset` specifies which columns are included in a SELECT or UPDATE.

### 12.1 Creating Subsets

```csharp
var template = new Product(conn);

FieldSubset all     = conn.FieldSubset(template, FieldSubsetInitialState.IncludeAll);
FieldSubset none    = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
FieldSubset dflt    = conn.DefaultFieldSubset(template);

// Single-field subset:
FieldSubset nameOnly = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
nameOnly += template.Name;  // add Name column only
```

### 12.2 Composing Subsets

```csharp
FieldSubset base1 = conn.FieldSubset(template, FieldSubsetInitialState.IncludeAll);
FieldSubset base2 = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
base2 += template.Price;

FieldSubset union        = base1 | base2;  // union
FieldSubset intersection = base1 & base2;  // intersection
FieldSubset removed      = base1 - base2;  // difference
```

### 12.3 Partial Fetch (SELECT)

```csharp
FieldSubset subset = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
subset += template.Name;
subset += template.Price;

// QueryAll with subset — only Name and Price columns are fetched:
var results = conn.QueryAll(template, null, null, 0, subset);
```

### 12.4 Partial Update

```csharp
// UpdateChanged() only updates fields that changed since the initial snapshot:
product.Price.SetValue(14.99m);   // only Price changed
product.UpdateChanged();           // generates: UPDATE Products SET Price=@Price WHERE ID=@ID

// Or queue for partial update with explicit subset:
product.QueueForUpdate(priceSubset);
conn.ProcessActionQueue();
```

### 12.5 InitialState Values

| Value | Meaning |
|-------|---------|
| `IncludeAll` | All columns included |
| `ExcludeAll` | No columns included |
| `Default` | ORM-defined default (usually all non-identity columns) |
| `IncludeAllJoins` | Include all joined/embedded object columns |
| `ExcludeAllJoins` | Exclude all joined/embedded object columns |

---

## 13. Field Encryption

### 13.1 Marking a Field for Encryption

```csharp
[Table("Customers")]
public class Customer : IdentDataObject
{
    [Column("SSN")]
    [Encrypted]                          // transparent encrypt/decrypt
    public TString SSN = new TString();

    public Customer() { }
    public Customer(DataConnection conn) : base(conn) { }
}
```

### 13.2 Providing an Encryption Algorithm

Implement `IEncryptionAlgorithm` (or `EncryptionAlgorithm`) and register it with the connection:

```csharp
public class AesEncryption : EncryptionAlgorithm
{
    private readonly byte[] _key;
    public AesEncryption(byte[] key) { _key = key; }

    public override byte[] Encrypt(byte[] plaintext) { /* AES encrypt */ }
    public override byte[] Decrypt(byte[] ciphertext) { /* AES decrypt */ }
}

// Register:
conn.SetEncryptionAlgorithm(new AesEncryption(myKey));
```

Once registered, all `[Encrypted]` fields are transparently encrypted on write and decrypted on read.

---

## 14. Custom Field Mappers

Implement `IDBFieldMapper` to handle non-standard CLR ↔ DB type conversions:

```csharp
public class MoneyMapper : IDBFieldMapper
{
    // Called when reading from DB:
    public object MapFromDB(object dbValue, TField field)
    {
        if (dbValue is long cents)
            return cents / 100m;
        return dbValue;
    }

    // Called when writing to DB:
    public object MapToDB(object clrValue, TField field)
    {
        if (clrValue is decimal d)
            return (long)(d * 100);
        return clrValue;
    }
}
```

Register on a specific field type or globally via the connection.

---

## 15. Polymorphic Mapping (FactoryBase)

Override `FactoryBase.Create(Type)` to return concrete subtypes based on a discriminator:

```csharp
[Table("Shapes")]
public abstract class Shape : IdentDataObject
{
    [Column("Kind")]   public TString Kind   = new TString();
    [Column("Colour")] public TString Colour = new TString();
    protected Shape() { }
    protected Shape(DataConnection conn) : base(conn) { }
}

[Table("Shapes")]
public class Circle : Shape
{
    [Column("Radius")] public TDecimal Radius = new TDecimal();
    public Circle() { }
    public Circle(DataConnection conn) : base(conn) { }
}

[Table("Shapes")]
public class Rectangle : Shape
{
    [Column("Width")]  public TDecimal Width  = new TDecimal();
    [Column("Height")] public TDecimal Height = new TDecimal();
    public Rectangle() { }
    public Rectangle(DataConnection conn) : base(conn) { }
}

public class ShapeFactory : FactoryBase
{
    public override DataObject Create(Type type, DataObject template)
    {
        if (type == typeof(Shape))
        {
            string kind = (string)((Shape)template).Kind.GetValue();
            return kind switch
            {
                "circle"    => new Circle(),
                "rectangle" => new Rectangle(),
                _           => base.Create(type, template)
            };
        }
        return base.Create(type, template);
    }
}
```

Register at connection time:

```csharp
var conn = new SqlServerConnection(connectionString, new ShapeFactory());
```

---

## 16. Optimistic Locking

`DataObjectLock.UpdateOption` controls what happens when another process has modified the row:

```csharp
// Default — throws ObjectLockException if row was modified elsewhere:
product.Update();

// Ignore lock — always overwrite:
product.Update(DataObjectLock.UpdateOption.IgnoreLock);

// Release lock after update:
product.Update(DataObjectLock.UpdateOption.ReleaseLock);

// Retain lock after update:
product.Update(DataObjectLock.UpdateOption.RetainLock);
```

Handle lock conflicts:

```csharp
try
{
    product.Update();
}
catch (ObjectLockException ex)
{
    Console.WriteLine($"Conflict: {ex.Message}");
    // re-read and retry...
}
```

---

## 17. Lazy Streaming

`LazyQueryAll<T>` returns an `IEnumerable<T>` that streams rows one at a time — no full in-memory buffer:

```csharp
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, true);

foreach (Product p in conn.LazyQueryAll<Product>(template, term, null))
{
    // Processed one row at a time — ideal for large result sets.
    Console.WriteLine(p.Name.GetValue());
}
```

Equivalent via LINQ (without `Take`/`Skip`):

```csharp
foreach (Product p in conn.Query<Product>().Where(p => p.InStock == true))
{
    Console.WriteLine(p.Name.GetValue());
}
```

---

## 18. Raw SQL and Stored Procedures

### 18.1 ExecSQL

```csharp
// Returns DataTable:
DataTable table = conn.ExecSQL(
    "SELECT Name, SUM(Qty) AS Total FROM OrderLines GROUP BY Name",
    null);

foreach (DataRow row in table.Rows)
    Console.WriteLine($"{row["Name"]} — {row["Total"]}");
```

### 18.2 Stored Procedures

```csharp
var parameters = new Dictionary<string, object>
{
    ["@CategoryId"] = 5,
    ["@MaxPrice"]   = 100m
};

DataTable result = conn.ExecStoredProcedure("usp_GetProductsByCategory", parameters);
```

---

## 19. Lookup / Cached Reference Tables

`LookupDataObject` caches its rows after the first load, suitable for small reference tables:

```csharp
[Table("Categories")]
public class Category : LookupDataObject
{
    [Column("Name")] public TString Name = new TString();
    public Category() { }
    public Category(DataConnection conn) : base(conn) { }
}

// First call loads all rows; subsequent calls return cached data:
var categories = conn.QueryAll(new Category(conn), null, null, 0, null);
```

---

## 20. Architecture Deep Dive

### 20.1 ObjectBinding — Reflection Cache

`ObjectBinding` is the ORM's per-type reflection cache. It holds:
- The list of `TField` `FieldInfo` objects decorated with `[Column]`
- The `[Table]` name
- Identity field info

`ObjectCollection : List<DataObject>` is returned by bulk query methods.

### 20.2 OrmQueryable<T> State Machine

The LINQ pipeline accumulates state immutably in `OrmQueryable<T>`:

```
OrmQueryable<T>
├── Connection    : DataConnection
├── Template      : T              (template DataObject instance)
├── WhereTerm     : QueryTerm?     (accumulated AND tree)
├── SortOrder     : SortOrder?     (primary; CombinedSortOrder for multi-column)
├── PageSize      : int?           (Take)
└── SkipCount     : int?           (Skip)
```

Each LINQ operator creates a new `OrmQueryable<T>` with updated state and sets its `Expression` property to the incoming `MethodCallExpression` — enabling correct recursive chain rebuilding.

### 20.3 ExpressionToQueryTermVisitor

Translates a `LambdaExpression` predicate into a `QueryTerm` tree:

1. `BinaryExpression (AndAlso)` → `AndTerm`
2. `BinaryExpression (OrElse)` → `OrTerm`
3. `UnaryExpression (Not)` → `NotTerm`
4. `BinaryExpression (Equal, null)` → `IsNullTerm`
5. `BinaryExpression (Equal, value)` → `EqualTerm`
6. `BinaryExpression (NotEqual, null)` → `!IsNullTerm`
7. `BinaryExpression (NotEqual, value)` → `!EqualTerm`
8. `BinaryExpression (GreaterThan)` → `GreaterThanTerm`
9. `MethodCallExpression (Contains on List<T>)` → `InTerm`

Local variable capture is handled by compiling and invoking the captured sub-expression: `Expression.Lambda(expr).Compile().DynamicInvoke()`.

### 20.4 UnitOfWorkBase Depth Counter

```
State: _depth = 0, _rollbackOnly = false, _currentTransaction = null

CreateTransaction() called:
  if _depth == 0:  BeginTransactionCore(level) → _currentTransaction
  _depth++

Commit() called:
  _depth--
  if _depth == 0:
    if _rollbackOnly: _currentTransaction.Rollback()
    else:             _currentTransaction.Commit()

Rollback() called:
  _rollbackOnly = true
  _depth--
  if _depth == 0: _currentTransaction.Rollback()

Dispose():
  if _depth > 0: Rollback(); _currentTransaction.Dispose()
```

### 20.5 CombinedSortOrder

When multiple `ThenBy`/`ThenByDescending` calls are chained, `CombinedSortOrder` wraps primary and secondary sorts:

```csharp
public class CombinedSortOrder : SortOrder
{
    public override string GetSQL(ObjectBinding binding)
        => _primary.GetSQL(binding) + ", " + _secondary.GetSQL(binding);
}
```

---

## 21. Quick Reference Cheat Sheet

### Entity Definition

```csharp
[Table("TableName")]
public class MyEntity : IdentDataObject
{
    [Column("ColA")] public TString  ColA = new TString();
    [Column("ColB")] public TDecimal ColB = new TDecimal();
    [Column("ColC")] public TBool    ColC = new TBool();

    public MyEntity() { }
    public MyEntity(DataConnection conn) : base(conn) { }
}
```

### CRUD

```csharp
var e = new MyEntity(conn);
e.ColA.SetValue("hello");
e.Insert();                    // INSERT

e.ID.SetValue(1);
e.Read();                      // SELECT by PK

e.ColB.SetValue(9.99m);
e.Update();                    // UPDATE all columns
e.UpdateChanged();             // UPDATE changed columns only

e.Delete();                    // DELETE by PK
```

### QueryTerm

```csharp
var t = new MyEntity(conn);
QueryTerm q = new EqualTerm(t, t.ColA, "hello")
            & new GreaterThanTerm(t, t.ColB, 5m);
var rows = conn.QueryAll(t, q, new OrderAscending(t, t.ColA), 0, null);
```

### LINQ

```csharp
using Turquoise.ORM.Linq;

var rows = conn.Query<MyEntity>()
    .Where(e => e.ColA == "hello" && e.ColB > 5m)
    .OrderBy(e => e.ColA)
    .Skip(0).Take(20)
    .ToList();
```

### Unit of Work

```csharp
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);
With.Transaction(uow, () =>
{
    e.Insert();
    e2.Insert();
});
```

### [Transaction] Attribute

```csharp
[Transaction]
public virtual void DoWork() { ... }

// Wire up with Castle:
var proxy = generator.CreateClassProxyWithTarget(typeof(MyService), real, interceptor);
proxy.DoWork();  // auto-commits
```

### Pagination

```csharp
var page = conn.QueryPage(template, term, sort, startRecord: 0, pageSize: 20);
// page.IsMoreData, page.TotalRowCount
```

### Field Subset

```csharp
FieldSubset subset = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
subset += template.ColA;
var rows = conn.QueryAll(template, null, null, 0, subset);
```

### Lazy Stream

```csharp
foreach (MyEntity e in conn.LazyQueryAll<MyEntity>(template, term, null))
    Console.WriteLine(e.ColA.GetValue());
```

---

*Turquoise.ORM v1.1 — .NET 8 / SQL Server*
