# Turquoise.ORM — Comprehensive Developer Wiki

A complete reference for every concept in Turquoise.ORM: architecture, field types, querying, transactions, unit of work, LINQ support, and advanced features.

---

## Table of Contents

1. [The Active Record Pattern](#1-the-active-record-pattern)
2. [Architecture Overview](#2-architecture-overview)
3. [Entities — DataObject and IdentDataObject](#3-entities--dataobject-and-identdataobject)
4. [Field Types (TField System)](#4-field-types-tfield-system)
5. [DataConnection — The Gateway](#5-dataconnection--the-gateway)
6. [CRUD Operations](#6-crud-operations)
7. [Query Predicates (QueryTerm API)](#7-query-predicates-queryterm-api)
8. [Sorting and Pagination](#8-sorting-and-pagination)
9. [LINQ Query Support](#9-linq-query-support)
10. [Transactions (Manual API)](#10-transactions-manual-api)
11. [Unit of Work, Transactions, and Connection Scope](#11-unit-of-work-iunitofwork)
12. [Action Queue (Batch Operations)](#12-action-queue-batch-operations)
13. [Field Subsets (Partial Fetch / Update)](#13-field-subsets-partial-fetch--update)
14. [Field Encryption](#14-field-encryption)
15. [Custom Field Mappers](#15-custom-field-mappers)
16. [Polymorphic Mapping (FactoryBase)](#16-polymorphic-mapping-factorybase)
17. [Optimistic Locking](#17-optimistic-locking)
18. [Lazy Streaming](#18-lazy-streaming)
19. [Raw SQL and Stored Procedures](#19-raw-sql-and-stored-procedures)
20. [Lookup / Cached Reference Tables](#20-lookup--cached-reference-tables)
21. [Architecture Deep Dive](#21-architecture-deep-dive)
22. [Quick Reference Cheat Sheet](#22-quick-reference-cheat-sheet)
23. [MongoDB Provider](#23-mongodb-provider)
24. [SQLite Provider](#24-sqlite-provider)

---

## 1. The Active Record Pattern

### 1.1 What is Active Record?

Active Record is a design pattern first named by Martin Fowler in *Patterns of Enterprise Application Architecture* (2003). Its defining idea is simple: **an object that represents a database row also knows how to persist itself**. The class carries both the data (fields mapping to columns) and the behaviour that reads, writes, and deletes that data from the database. There is no separate layer sitting between the object and the database — the object *is* the persistence unit.

In Turquoise.ORM every entity class inherits from `DataObject`. A `DataObject` instance holds typed field objects (`TString`, `TDecimal`, `TBool`, …) for each column, and exposes methods like `Insert()`, `Update()`, `Delete()`, and `Read()` that execute the corresponding SQL:

```csharp
// The object carries data AND knows how to save itself.
var product = new Product(conn);
product.Name.SetValue("Widget");
product.Price.SetValue(9.99m);
product.Insert();   // ← persistence behaviour on the object itself

product.Price.SetValue(14.99m);
product.Update();   // ← object tells the DB to update its own row
```

### 1.2 Why Use Active Record?

**Simplicity for CRUD-heavy applications.** When the primary job of your code is creating, reading, updating and deleting records — as it is in the overwhelming majority of business applications — Active Record keeps the pattern count low. You work with one object that represents the row; you do not need to maintain a separate repository class, a separate mapping class, and a separate domain model class for each table.

**Reduced boilerplate.** With Data Mapper (see §1.3) you write a `ProductRepository`, a `ProductMapper`, and a `Product` domain object for every entity. With Active Record you write one `Product` class and you are done. For applications with dozens of tables this is a meaningful reduction in total code.

**Immediate navigability.** Because persistence methods live on the entity, any developer reading the code can see at a glance what a class can do. `product.Insert()` is self-evident in a way that `_unitOfWork.ProductRepository.Add(product)` is not.

**Low cognitive overhead for queries.** The `QueryTerm` predicate tree and the LINQ layer translate directly into typed field references on the entity itself:

```csharp
conn.Query<Product>().Where(p => p.Price > 50m && p.InStock == true).ToList();
```

The field references (`p.Price`, `p.InStock`) are real `TField` objects on the same class you use everywhere else — not magic strings, not a separate query model.

### 1.3 How Active Record Differs from Other Patterns

#### Data Mapper

Data Mapper separates domain objects completely from persistence logic. A plain `Product` POCO knows nothing about databases; a separate `ProductMapper` (or ORM configuration file) handles the translation. Entity Framework Core is the canonical .NET example.

| Concern | Active Record (Turquoise) | Data Mapper (EF Core) |
|---------|--------------------------|----------------------|
| Where does persistence logic live? | On the entity class (`DataObject`) | In the ORM mapping layer, separate from the domain object |
| Domain object awareness | Entity knows its own columns and how to save itself | Entity is a plain class with no ORM dependency |
| Boilerplate per table | One class | Entity + mapping configuration (fluent or attributes) + optional repository |
| Ideal for | CRUD-heavy applications, direct DB → object mapping | Complex domain models where business logic must be insulated from persistence concerns |
| Testability | Test via a stub `DataConnection` | Test domain objects with no DB dependency at all |

**When to prefer Data Mapper:** If your domain objects need to be completely ignorant of the database — for example in a DDD (Domain-Driven Design) project with a rich domain model, complex invariants, and a desire to keep the persistence mechanism fully swappable — Data Mapper gives a cleaner separation. The cost is more infrastructure code.

**When Active Record wins:** When your application's entities largely mirror your database schema, the business logic is not so complex that it demands strict separation, and development speed matters. This covers the majority of line-of-business applications.

#### Repository Pattern

The Repository pattern is often used *on top of* a Data Mapper ORM to add a collection-like interface over the persistence layer:

```csharp
// Repository pattern (not Turquoise):
IProductRepository repo = new SqlProductRepository(dbContext);
Product p = await repo.GetByIdAsync(42);
p.Price = 14.99m;
await repo.SaveAsync(p);
```

With Active Record the entity already *is* its own repository in a sense. You can still add a separate service or repository class around Turquoise entities if you want to centralise query logic or enforce business rules — the ORM does not prevent it — but the pattern is not forced on you.

#### Table Gateway

Table Gateway assigns one object per *table* (not per *row*), and that object contains all the methods for querying that table. This is coarser-grained than Active Record: you call `ProductGateway.FindAll()` and receive raw data rows, rather than calling `product.Read()` and getting a fully hydrated typed object back.

#### Record / DTO (plain classes)

Some teams use plain data transfer objects with a separate command/query handler (CQRS style). This decouples read and write models completely but requires hand-written SQL or a micro-ORM (Dapper) and significant ceremony to wire up. Active Record is a better fit when the read model and the write model are the same thing, which is again the common case in business software.

### 1.4 Active Record in Turquoise.ORM — Key Decisions

**Fields, not properties.** Turquoise represents columns as public `TField` instance fields rather than auto-properties. This lets the ORM discover them by reflection without attributes on every getter, lets them carry null/loaded state independently of their value, and enables the predicate system (a `TField` reference in a `QueryTerm` constructor tells the ORM exactly which column to filter on).

**Shared connection, not embedded connection.** The entity does not open its own database connection. Instead, one `DataConnection` is passed in at construction time and shared across all objects in a unit of work. This keeps connection management explicit and testable, while still letting the object call `Insert()` / `Update()` without the caller needing to think about the connection.

**Delegation, not inheritance from the connection.** `DataObject.Insert()` delegates to `conn.Insert(this)`. The SQL generation, parameter binding, and result hydration live in `DataConnection` (and its SQL Server implementation), not in each entity. Entities therefore stay lean — they contain field declarations and business logic, nothing else.

**Optional Unit of Work on top.** Pure Active Record is sometimes criticised for making it hard to batch multiple saves into a single transaction in a clean way. Turquoise addresses this with the `IUnitOfWork` / `With.Transaction` layer (§11), which can wrap any number of `Insert()` / `Update()` / `Delete()` calls in a managed transaction without changing the entity code at all.

---

## 2. Architecture Overview

Turquoise.ORM is a **lightweight Active Record ORM** for .NET 8. It is split across provider assemblies to keep database-specific concerns separate from the core abstractions.

### 2.1 Assembly Split

| Assembly | Contents |
|----------|----------|
| `Turquoise.ORM` | Core — entities, TField types, QueryTerm predicates, LINQ layer, transactions (abstract), adapters (abstract) |
| `Turquoise.ORM.SqlServer` | SQL Server provider — `SqlServerConnection`, SQL adapters, `SqlServerUnitOfWork` |
| `Turquoise.ORM.PostgreSQL` | PostgreSQL provider — `PostgreSQLConnection`, Npgsql adapters, `PostgreSQLUnitOfWork` |
| `Turquoise.ORM.MongoDB` | MongoDB provider — `MongoDataConnection`, BSON mapping, `MongoUnitOfWork` |
| `Turquoise.ORM.SQLite` | SQLite provider — `SQLiteConnection`, Microsoft.Data.Sqlite adapters, `SQLiteUnitOfWork` |

Entity classes only reference `Turquoise.ORM`. Applications add the appropriate provider package alongside it.

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│  Your Application                                                                        │
│                                                                                          │
│  DataObject subclass ──── CRUD ────► DataConnection (abstract, core)                    │
│  (fields, logic)                         │                                               │
│  QueryTerm / LINQ ─── query calls ───────┤                                               │
│                                          │ implemented by (choose one)                   │
│           ┌──────────────────────────────┼──────────────────────────────┐               │
│  SqlServerConnection  PostgreSQLConnection  MongoDataConnection  SQLiteConnection         │
│  (SqlServer provider) (PostgreSQL provider) (MongoDB provider)  (SQLite provider)         │
│           │                   │                    │                    │               │
│      SQL Server           PostgreSQL            MongoDB              SQLite             │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Core Principles

- **Active Record** — `DataObject` instances know how to persist themselves via a shared `DataConnection`.
- **Type-safe fields** — every column is represented by a `TField` subclass, not a bare property. This tracks null/loaded state and enables predicate construction.
- **Composable predicates** — `QueryTerm` objects compose with C# `&`, `|`, `!` operators to build arbitrary WHERE clauses.
- **Connection-centric** — `DataConnection` is the single point of query execution; entities delegate to it.
- **Provider-agnostic core** — `Turquoise.ORM` has no dependency on `Microsoft.Data.SqlClient` or `Npgsql`. Only the provider packages do.

---

## 3. Entities — DataObject and IdentDataObject

### 3.1 Base Classes

| Class | When to use |
|-------|-------------|
| `DataObject` | Tables without a single integer auto-identity primary key |
| `IdentDataObject` | Tables with an `INT IDENTITY(1,1)` primary key (exposed as `ID: TPrimaryKey`) |

### 3.2 Defining an Entity

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

### 3.3 IdentDataObject.ID

`IdentDataObject` adds:

```csharp
[Column("ID")]
[Identity]
public TPrimaryKey ID = new TPrimaryKey();
```

After `Insert()`, `ID.GetValue()` returns the new auto-generated integer.

### 3.4 Embedded / Joined Objects

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

## 4. Field Types (TField System)

### 4.1 Common API

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

### 4.2 Numeric Types

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

### 4.3 String Types

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

### 4.4 Key Types

| Type | Notes |
|------|-------|
| `TPrimaryKey` | Auto-identity integer PK; read-only after insert |
| `TForeignKey` | Integer FK referencing another table |

```csharp
int id = (int)order.ID.GetValue();          // primary key after insert
orderLine.OrderID.SetValue(id);              // set foreign key
```

### 4.5 Date / Time Types

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

### 4.6 Boolean and Binary

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

### 4.7 Null Handling

```csharp
product.Name.SetNull();              // explicitly set to NULL
if (product.Name.IsNull()) { ... }   // check for NULL
if (!product.Name.IsLoaded()) { ... } // never set at all
```

---

## 5. DataConnection — The Gateway

`DataConnection` is the abstract base; `SqlServerConnection` is the concrete SQL Server implementation.

### 5.1 Creating a Connection

`SqlServerConnection`, `PostgreSQLConnection`, and `MongoDataConnection` all live in the `Turquoise.ORM` namespace (in their respective provider assemblies), so no extra `using` directive is needed once the provider assembly is referenced.

```csharp
// SQL Server (reference Turquoise.ORM + Turquoise.ORM.SqlServer)
using Turquoise.ORM;

var conn = new SqlServerConnection(
    "Server=.;Database=MyDB;Integrated Security=True;TrustServerCertificate=True;",
    new FactoryBase());
conn.Connect();
```

```csharp
// PostgreSQL (reference Turquoise.ORM + Turquoise.ORM.PostgreSQL)
using Turquoise.ORM;

var conn = new PostgreSQLConnection(
    "Host=localhost;Database=mydb;Username=app;Password=secret;",
    new FactoryBase());
conn.Connect();
```

```csharp
// MongoDB (reference Turquoise.ORM + Turquoise.ORM.MongoDB)
using Turquoise.ORM;

var conn = new MongoDataConnection(
    "mongodb://localhost:27017",
    "myDatabase");
conn.Connect();
```

### 5.2 Lifecycle

```csharp
conn.Connect();     // opens the ADO.NET connection
conn.Disconnect();  // closes it
```

### 5.3 Factory Pattern

Pass a `FactoryBase` to control how the ORM instantiates objects. The default `FactoryBase` uses `Activator.CreateInstance`. Override `Create(Type)` for polymorphic mapping (see [§15](#15-polymorphic-mapping-factorybase)).

### 5.4 Provider Dialect Comparison

| Feature | SQL Server | PostgreSQL | MongoDB |
|---------|-----------|------------|---------|
| Parameter mark | `@name` | `@name` | N/A |
| Identifier quoting | `[Name]` | `"name"` | N/A |
| Row limit syntax | `SELECT TOP n …` | `SELECT … LIMIT n` | `.Limit(n)` |
| String concatenation | `+` | `\|\|` | N/A |
| Identity retrieval | `SELECT SCOPE_IDENTITY()` | `SELECT LASTVAL()` | Counter collection |
| Row lock hint | `WITH (UPDLOCK)` | `FOR UPDATE` (end) | N/A |
| Identity insert control | `SET IDENTITY_INSERT … ON/OFF` | Not needed | Not needed |
| Schema introspection | `SYSOBJECTS` / `SYSCOLUMNS` | `information_schema.columns` | `[Table]` attribute |
| Identity storage | Auto-generated int column | Auto-generated int (`serial`) | `_id` field (int) |
| Identifier case | Case-insensitive | Case-sensitive (lower-case) | Exact match |
| Unit of Work class | `SqlServerUnitOfWork` | `PostgreSQLUnitOfWork` | `MongoUnitOfWork` |
| Driver | `Microsoft.Data.SqlClient` | `Npgsql` | `MongoDB.Driver` |
| SQL operations | Full | Full | `NotSupportedException` |
| Transaction scope | RDBMS transaction | RDBMS transaction | Requires replica set |

**PostgreSQL naming note:** PostgreSQL folds unquoted identifiers to lower-case at parse time. All `[Table]` and `[Column]` attribute values should be lower-case unless you created the table with quoted identifiers.

**MongoDB naming note:** `[Table("collectionName")]` maps to the MongoDB collection name. `[Column("fieldName")]` maps to the BSON field name. The `[Identity]` field maps to the special `_id` BSON field and is stored as an integer using a counter document for auto-increment simulation.

---

## 6. CRUD Operations

### 6.1 Insert

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

### 6.2 Read (by Primary Key)

```csharp
var product = new Product(conn);
product.ID.SetValue(42);
product.Read();

Console.WriteLine(product.Name.GetValue()); // "Widget"
```

Alternatively, use `conn.Read(product)`.

### 6.3 Update

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

### 6.4 Delete

```csharp
product.Delete();                    // deletes by PK

// Delete by predicate:
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.Delete(term);               // DELETE WHERE InStock = 0
```

### 6.5 ReadForUpdate (Advisory Lock)

```csharp
product.ID.SetValue(1);
product.ReadForUpdate();             // SELECT ... WITH (UPDLOCK)
product.Price.SetValue(29.99m);
product.Update();
```

---

## 7. Query Predicates (QueryTerm API)

### 7.1 Equality

```csharp
var template = new Product(conn);
var term = new EqualTerm(template, template.Name, "Widget");
// → WHERE Name = @Name
```

### 7.2 Comparisons

```csharp
new GreaterThanTerm(template, template.Price, 50m)     // >
new GreaterOrEqualTerm(template, template.Price, 50m)  // >=
new LessThanTerm(template, template.Price, 10m)        // <
new LessOrEqualTerm(template, template.Price, 10m)     // <=
```

### 7.3 String Matching

```csharp
new LikeTerm(template, template.Name, "%widget%")       // LIKE
new ContainsTerm(template, template.Name, "widget")     // LIKE '%widget%' (built-in)
```

### 7.4 Null Checks

```csharp
new IsNullTerm(template, template.Name)     // IS NULL
!new IsNullTerm(template, template.Name)   // IS NOT NULL (via NotTerm)
```

### 7.5 IN Clause

```csharp
var ids = new List<int> { 1, 2, 3 };
new InTerm(template, template.ID, ids)
// → WHERE ID IN (@IN_ID0, @IN_ID1, @IN_ID2)
```

### 7.6 Full-Text Search

```csharp
new FullTextTerm(template, template.Name, "widget NEAR gadget")
// → WHERE CONTAINS(Name, 'widget NEAR gadget')
```

### 7.7 EXISTS Sub-query

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

### 7.8 Raw SQL Predicate

```csharp
new RawSqlTerm("Price BETWEEN 10 AND 50")
// Injected verbatim — use sparingly; prefer parameterized terms.
```

### 7.9 Logical Composition

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

### 7.10 Executing Queries

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

## 8. Sorting and Pagination

### 8.1 Sort Orders

```csharp
var template = new Product(conn);

SortOrder byName      = new OrderAscending(template, template.Name);
SortOrder byPriceDesc = new OrderDescending(template, template.Price);

// Compose multiple sorts:
// Use CombinedSortOrder (Linq namespace) or pass the first to QueryAll
// and rely on secondary ORDER BY via SQL.
```

### 8.2 QueryPage

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

## 9. LINQ Query Support

*(Requires `using Turquoise.ORM.Linq;`)*

### 9.1 Entry Point

```csharp
IQueryable<Product> q = conn.Query<Product>();
```

Internally creates a template instance and wraps it in `OrmQueryable<T>`. The database is NOT queried until you enumerate.

### 9.2 Where (Predicates)

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

### 9.3 Chained Where (auto-ANDed)

```csharp
conn.Query<Product>()
    .Where(p => p.InStock == true)
    .Where(p => p.Price   >  10m)
    .Where(p => p.Name    != "Discontinued")
    .ToList();
// Equivalent to: WHERE InStock = 1 AND Price > 10 AND Name <> 'Discontinued'
```

### 9.4 Sorting

```csharp
conn.Query<Product>().OrderBy(p => p.Name).ToList();
conn.Query<Product>().OrderByDescending(p => p.Price).ToList();

// Multi-column:
conn.Query<Product>()
    .OrderBy(p => p.Name)
    .ThenByDescending(p => p.Price)
    .ToList();
```

### 9.5 Pagination

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

### 9.6 Full Chain

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

### 9.7 Supported Operators

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

### 9.8 Limitations

| Limitation | Workaround |
|------------|-----------|
| No `GroupBy` | Use raw SQL (`ExecSQL`) |
| No `Join` | Use embedded `DataObject` fields |
| No `Select` projection | Use `FieldSubset` on template |
| No `Count()`, `First()` | Use `conn.QueryCount()`, `conn.QueryFirst()` |
| No async | Async planned for a future release |

### 9.9 Mixing LINQ with QueryTerm

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

## 10. Transactions (Manual API)

### 10.1 Explicit Transaction

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

### 10.2 Isolation Levels

```csharp
using System.Data;

conn.BeginTransaction(IsolationLevel.Serializable);
// ... work ...
conn.CommitTransaction();
```

Available: `ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`, `Serializable`, `Snapshot`.

### 10.3 Nested Transactions

Turquoise uses a **depth counter** internally. Outer `BeginTransaction` starts the real ADO.NET transaction; inner calls increment depth only:

```csharp
conn.BeginTransaction();            // depth: 0→1, real tx starts
    conn.BeginTransaction();        // depth: 1→2, reuses same tx
    conn.CommitTransaction();       // depth: 2→1, no DB commit yet
conn.CommitTransaction();           // depth: 1→0, real COMMIT
```

If any inner scope calls `RollbackTransaction()`, the entire outer transaction will roll back when it unwinds.

### 10.4 TransactionState

```csharp
TransactionState state = conn.TransactionState();
// returns: None, Active, Committed, Aborted
```

---

## 11. Unit of Work, Transactions, and Connection Scope

*(Namespace: `Turquoise.ORM.Transactions`; requires Castle.Core NuGet for interceptors)*

### 11.1 Overview

Turquoise.ORM provides two complementary Castle DynamicProxy interceptors for managing the
connection and transaction lifecycle at the service-method boundary:

| Interceptor | Attribute | What it does |
|-------------|-----------|--------------|
| `ConnectionScopeInterceptor` | `[ConnectionScope]` | Opens `DataConnection` before the method; closes it in `finally` |
| `TransactionInterceptor` | `[Transaction]` | Wraps the method in a `IUnitOfWork` transaction |

`IUnitOfWork` wraps a `DataConnection`'s transaction lifecycle behind a clean abstraction, enabling:
- **`With.Transaction`** functional helper
- **`[Transaction]`** attribute-based automatic interception via Castle DynamicProxy
- **Ambient registration** via `TurquoiseServiceLocator`
- **`[ConnectionScope]`** attribute for automatic connection open/close (§11.10)

### 11.2 IUnitOfWork Interface

```csharp
public interface IUnitOfWork : IDisposable
{
    bool InTransaction { get; }
    TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    void Commit();
    void Rollback();
}
```

### 11.3 SqlServerUnitOfWork

```csharp
using IUnitOfWork uow = new SqlServerUnitOfWork(conn);
```

Wraps the underlying `SqlServerConnection` and delegates `BeginTransaction` to it.

### 11.4 With.Transaction

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

### 11.5 Nested Transactions

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

### 11.6 [Transaction] Attribute

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

### 11.7 Setting Up Castle DynamicProxy Interception

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

### 11.8 TurquoiseServiceLocator (Ambient DI)

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

### 11.9 Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Exception inside `With.Transaction` | `Rollback()` called; exception re-thrown |
| Inner scope rolls back | `_rollbackOnly` set; outer scope will roll back even if it tries to commit |
| `Dispose()` with open transaction | Transaction is rolled back automatically |
| `Commit()` when `_rollbackOnly` | Silently converts to `Rollback()` |
| `CreateTransaction()` when already active and same isolation level | Depth incremented; no new ADO.NET transaction |

### 11.10 [ConnectionScope] — Connection Lifecycle for Service Methods

`[ConnectionScope]` and its Castle DynamicProxy interceptor `ConnectionScopeInterceptor` complement
`[Transaction]` by managing the connection open/close lifecycle at the service-method boundary,
leaving the caller completely free of connection boilerplate.

#### Attribute

```csharp
using Turquoise.ORM.Attributes;

// Method level — only this method gets the scope
[ConnectionScope]
public void DoWork() { ... }

// Class level — every method on the class gets the scope
[ConnectionScope]
public class OrderService : IOrderService, IService { ... }
```

Place the attribute on the **implementation class** (not the interface). When using an interface
proxy (the default for `IService` services), `ConnectionScopeInterceptor` reads the attribute from
`IInvocation.MethodInvocationTarget` — the concrete method — so attributes on the interface are
supported as a fallback only.

#### How it works

```
Call enters proxy
  → ConnectionScopeInterceptor checks IsOpen
      if NOT open → Connect()
      → method body executes
  → finally: if we opened the connection → Disconnect()
```

The interceptor uses `DataConnection.IsOpen` to decide whether to open/close — it never
double-opens. This means:

- A `[ConnectionScope]` method that calls another `[ConnectionScope]` method on the same proxy
  reuses the already-open connection; the connection closes only when the outermost scope returns.
- Entity writes inside a `[ConnectionScope]` method (`p.Insert()`, `p.Delete()`, …) use the
  open connection transparently — `RunWrite` checks `IsOpen` and skips the re-connect.

#### Combining with [Transaction]

```csharp
[ConnectionScope]   // opens connection before, closes after (in finally)
[Transaction]       // begins UoW transaction after connection is open; commits on return
public void Ship(int orderId)
{
    var order = new Order(_conn);
    order.ID.SetValue(orderId);
    _conn.Read(order);
    order.Status.SetValue("Shipped");
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);
    // commit on success; rollback + close on exception
}
```

Interceptor execution order (outermost first):

```
ConnectionScopeInterceptor → Connect()
  TransactionInterceptor    → UoW.CreateTransaction()
    → real method executes
  TransactionInterceptor    → UoW.Commit() (or Rollback on exception)
ConnectionScopeInterceptor → Disconnect() [always, in finally]
```

#### IService auto-registration and DI

Services that implement `IService` are registered by `AddServices()` as Castle **interface
proxies** — no `virtual` keyword required on any method.

```csharp
// Program.cs
builder.Services
    .AddTurquoiseSqlServer("Server=...;...")
    .AddServices(typeof(Program).Assembly);

// Service definition
public interface IOrderService
{
    Order GetById(int id);
    void  Ship(int orderId);
}

public class OrderService : IOrderService, IService
{
    private readonly DataConnection _conn;
    public OrderService(DataConnection conn) { _conn = conn; }

    [ConnectionScope]
    public Order GetById(int id) { ... }

    [ConnectionScope]
    [Transaction]
    public void Ship(int orderId) { ... }
}

// Controller — injects by interface; proxy is transparent
public class CheckoutController : ControllerBase
{
    public CheckoutController(IOrderService orders) { _orders = orders; }
}
```

#### Standalone (no DI)

```csharp
var conn = new SqlServerConnection("Server=...;...");
var uow  = new SqlServerUnitOfWork(conn);
var svc  = TurquoiseServiceFactory.Create<IOrderService>(new OrderService(conn), conn, uow);

svc.Ship(42);   // proxy opens connection, starts transaction, executes, commits, closes
```

#### Connection-level lifecycle without a proxy

For code that doesn't go through a service proxy, assign `UnitOfWork` on the connection once. Every
write operation (`Insert`, `Update`, `Delete`, `ProcessActionQueue`, `ExecStoredProcedure`)
automatically opens the connection, begins a transaction, commits, and closes:

```csharp
var conn = new SqlServerConnection("...");
var uow  = new SqlServerUnitOfWork(conn);
conn.UnitOfWork = uow;   // wire once

var product = new Product(conn);
product.Name.SetValue("Widget");
product.Price.SetValue(9.99m);
product.Insert();   // opens connection → begins transaction → inserts → commits → closes
```

Read operations (`Read`, `QueryAll`, `QueryPage`, …) auto-connect and disconnect but do not start
a transaction regardless of whether `UnitOfWork` is set.

#### Proxy strategies summary

| Scenario | Proxy type | Requirements |
|----------|-----------|----|
| `IService` + `AddServices()` (auto-scan) | `CreateInterfaceProxyWithTarget` | None — no virtual required |
| `AddService<TInterface, TImpl>()` | `CreateInterfaceProxyWithTarget` | None |
| `AddService<TClass>()` (class proxy) | `CreateClassProxyWithTarget` | Non-sealed; intercepted methods `virtual` |
| `TurquoiseServiceFactory.Create<T>()` | Interface or class proxy (auto-detected) | See above |

---

## 12. Action Queue (Batch Operations)

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

## 13. Field Subsets (Partial Fetch / Update)

`FieldSubset` specifies which columns are included in a SELECT or UPDATE.

### 13.1 Creating Subsets

```csharp
var template = new Product(conn);

FieldSubset all     = conn.FieldSubset(template, FieldSubsetInitialState.IncludeAll);
FieldSubset none    = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
FieldSubset dflt    = conn.DefaultFieldSubset(template);

// Single-field subset:
FieldSubset nameOnly = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
nameOnly += template.Name;  // add Name column only
```

### 13.2 Composing Subsets

```csharp
FieldSubset base1 = conn.FieldSubset(template, FieldSubsetInitialState.IncludeAll);
FieldSubset base2 = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
base2 += template.Price;

FieldSubset union        = base1 | base2;  // union
FieldSubset intersection = base1 & base2;  // intersection
FieldSubset removed      = base1 - base2;  // difference
```

### 13.3 Partial Fetch (SELECT)

```csharp
FieldSubset subset = conn.FieldSubset(template, FieldSubsetInitialState.ExcludeAll);
subset += template.Name;
subset += template.Price;

// QueryAll with subset — only Name and Price columns are fetched:
var results = conn.QueryAll(template, null, null, 0, subset);
```

### 13.4 Partial Update

```csharp
// UpdateChanged() only updates fields that changed since the initial snapshot:
product.Price.SetValue(14.99m);   // only Price changed
product.UpdateChanged();           // generates: UPDATE Products SET Price=@Price WHERE ID=@ID

// Or queue for partial update with explicit subset:
product.QueueForUpdate(priceSubset);
conn.ProcessActionQueue();
```

### 13.5 InitialState Values

| Value | Meaning |
|-------|---------|
| `IncludeAll` | All columns included |
| `ExcludeAll` | No columns included |
| `Default` | ORM-defined default (usually all non-identity columns) |
| `IncludeAllJoins` | Include all joined/embedded object columns |
| `ExcludeAllJoins` | Exclude all joined/embedded object columns |

---

## 14. Field Encryption

### 14.1 Marking a Field for Encryption

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

### 14.2 Providing an Encryption Algorithm

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

## 15. Custom Field Mappers

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

## 16. Polymorphic Mapping (FactoryBase)

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

## 17. Optimistic Locking

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

## 18. Lazy Streaming

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

## 19. Raw SQL and Stored Procedures

### 19.1 ExecSQL

```csharp
// Returns DataTable:
DataTable table = conn.ExecSQL(
    "SELECT Name, SUM(Qty) AS Total FROM OrderLines GROUP BY Name",
    null);

foreach (DataRow row in table.Rows)
    Console.WriteLine($"{row["Name"]} — {row["Total"]}");
```

### 19.2 Stored Procedures

```csharp
var parameters = new Dictionary<string, object>
{
    ["@CategoryId"] = 5,
    ["@MaxPrice"]   = 100m
};

DataTable result = conn.ExecStoredProcedure("usp_GetProductsByCategory", parameters);
```

---

## 20. Lookup / Cached Reference Tables

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

## 21. Architecture Deep Dive

### 21.0 Assembly Boundaries

```
Turquoise.ORM (core — no provider dependency)
├── DataObject / IdentDataObject / LookupDataObject
├── DataConnection (abstract) / DBDataConnection (abstract)
├── TField subtypes (25+)
├── QueryTerm tree (EqualTerm, AndTerm, InTerm, …)
├── LINQ layer (OrmQueryable, ExpressionToQueryTermVisitor, …)
├── Adapter abstractions (ConnectionBase, CommandBase, ReaderBase, TransactionBase)
└── Transactions (IUnitOfWork, UnitOfWorkBase, With, TransactionInterceptor, …)

Turquoise.ORM.SqlServer (depends on Turquoise.ORM + Microsoft.Data.SqlClient)
├── SqlServerConnection : DBDataConnection
├── Adapters/SqlAdapterConnection     (wraps SqlConnection)
├── Adapters/SqlAdapterCommand        (wraps SqlCommand)
├── Adapters/SqlAdapterReader         (wraps SqlDataReader)
├── Adapters/SqlAdapterTransaction    (wraps SqlTransaction)
└── Transactions/SqlServerUnitOfWork  : UnitOfWorkBase

Turquoise.ORM.PostgreSQL (depends on Turquoise.ORM + Npgsql)
├── PostgreSQLConnection : DBDataConnection
├── Adapters/NpgsqlAdapterConnection  (wraps NpgsqlConnection)
├── Adapters/NpgsqlAdapterCommand     (wraps NpgsqlCommand)
├── Adapters/NpgsqlAdapterReader      (wraps NpgsqlDataReader)
├── Adapters/NpgsqlAdapterTransaction (wraps NpgsqlTransaction)
└── Transactions/PostgreSQLUnitOfWork : UnitOfWorkBase

Turquoise.ORM.MongoDB (depends on Turquoise.ORM + MongoDB.Driver)
├── MongoDataConnection : DataConnection   ← extends DataConnection directly (not DBDataConnection)
├── Internal/MongoFieldDescriptor          (per-field BSON name cache)
├── Internal/MongoTypeCache                (per-type collection name + field descriptors)
├── Internal/MongoMapper                   (DataObject ↔ BsonDocument serialization)
├── Internal/MongoQueryTranslator          (QueryTerm → FilterDefinition, SortOrder → SortDefinition)
└── Transactions/MongoUnitOfWork : UnitOfWorkBase
```

All provider types use the `Turquoise.ORM` namespace — the same namespace as the core types they extend. This means consuming code only needs `using Turquoise.ORM;`.

**MongoDB vs SQL architecture:** SQL providers extend `DBDataConnection`, which inherits `DataConnection` and adds SQL generation, ADO.NET adapter management, and the `ObjectBinding` cache. `MongoDataConnection` extends `DataConnection` directly because there is no SQL to generate. It builds its own minimal `ObjectBinding` from reflection for QueryTerm lookup, and performs all CRUD via `MongoDB.Driver` API calls.

### 21.1 ObjectBinding — Reflection Cache

`ObjectBinding` is the ORM's per-type reflection cache. It holds:
- The list of `TField` `FieldInfo` objects decorated with `[Column]`
- The `[Table]` name
- Identity field info

`ObjectCollection : List<DataObject>` is returned by bulk query methods.

### 21.2 OrmQueryable<T> State Machine

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

### 21.3 ExpressionToQueryTermVisitor

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

### 21.4 UnitOfWorkBase Depth Counter

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

### 21.5 CombinedSortOrder

When multiple `ThenBy`/`ThenByDescending` calls are chained, `CombinedSortOrder` wraps primary and secondary sorts:

```csharp
public class CombinedSortOrder : SortOrder
{
    public override string GetSQL(ObjectBinding binding)
        => _primary.GetSQL(binding) + ", " + _secondary.GetSQL(binding);
}
```

---

## 22. Quick Reference Cheat Sheet

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

## 23. MongoDB Provider

### 23.1 Overview

`MongoDataConnection` brings the standard Turquoise.ORM CRUD and query API to MongoDB. It extends `DataConnection` directly (not `DBDataConnection`, which is SQL-specific) and uses the `MongoDB.Driver` 2.28.0 library.

Key differences from SQL providers:

| Aspect | SQL Providers | MongoDB |
|--------|--------------|---------|
| Base class | `DBDataConnection` (SQL generation) | `DataConnection` (no SQL) |
| Storage | Relational tables | BSON documents in collections |
| `[Table]` attribute | SQL table name | Collection name |
| `[Column]` attribute | Column name | BSON field name |
| `[Identity]` field | Auto-increment int (DB-generated) | `_id` (int via counter collection) |
| Query translation | SQL `WHERE` clause | `FilterDefinition<BsonDocument>` |
| Sort translation | SQL `ORDER BY` | `SortDefinition<BsonDocument>` |
| Transactions | RDBMS transactions | `IClientSessionHandle` (requires replica set) |
| `ExecSQL` / `ExecStoredProcedure` | Supported | `NotSupportedException` |

### 23.2 Connecting

```csharp
using Turquoise.ORM;

var conn = new MongoDataConnection(
    connectionString: "mongodb://localhost:27017",
    databaseName:     "myDatabase");
conn.Connect();
```

### 23.3 Defining Entities

Entity definitions are provider-agnostic — use the same `[Table]` / `[Column]` / `[Identity]` attributes:

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

The `[Identity]` field (added by `IdentDataObject`) maps to MongoDB's `_id` field stored as `int32`. Auto-increment is simulated via a `__turquoise_counters` collection.

### 23.4 CRUD

```csharp
// INSERT (auto-generates _id)
var p = new Product(conn);
p.Name.SetValue("Widget");
p.Price.SetValue(9.99m);
p.InStock.SetValue(true);
p.Insert();
// p.ID now has the generated _id value

// READ by primary key
var p2 = new Product(conn);
p2.ID.SetValue(1);
bool found = p2.Read();

// UPDATE
p.Price.SetValue(14.99m);
p.Update(DataObjectLock.UpdateOption.IgnoreLock);

// DELETE by primary key
p.Delete();

// DELETE by query
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.Delete(term);
```

### 23.5 Querying

The full `QueryAll`, `QueryFirst`, `QueryCount`, `QueryPage`, and `LazyQueryAll` methods are supported. QueryTerms are translated to `FilterDefinition<BsonDocument>` internally:

```csharp
var template = new Product(conn);

// All in-stock products
var inStock  = new EqualTerm(template, template.InStock, true);
var results  = conn.QueryAll(template, inStock, null, 0, null);

// Price range
var lowPrice  = new GreaterThanTerm(template, template.Price, 5.00m);
var highPrice = new LessThanTerm(template, template.Price, 50.00m);
var range     = conn.QueryAll(template, lowPrice & highPrice, null, 0, null);

// Count
int count = conn.QueryCount(template, inStock);

// Paged results (skip 20, take 10)
var page = conn.QueryPage(template, inStock, null, 20, 10, null);

// Streaming
foreach (Product item in conn.LazyQueryAll<Product>(template, inStock, null, 0, null))
    Console.WriteLine(item.Name.GetValue());
```

### 23.6 Supported QueryTerms

| QueryTerm | MongoDB translation |
|-----------|-------------------|
| `EqualTerm` | `Filter.Eq(field, value)` |
| `GreaterThanTerm` | `Filter.Gt(field, value)` |
| `LessThanTerm` | `Filter.Lt(field, value)` |
| `IsNullTerm` | `Filter.Eq(field, BsonNull.Value)` |
| `InTerm` | `Filter.In(field, values)` |
| `ContainsTerm` | `Filter.Regex(field, /value/i)` |
| `AndTerm` (`&`) | `Filter.And(left, right)` |
| `OrTerm` (`\|`) | `Filter.Or(left, right)` |
| `NotTerm` (`!`) | `Filter.Not(inner)` |

### 23.7 Transactions

MongoDB multi-document transactions require a **replica set** or **sharded cluster**. On a standalone `mongod` they are not supported.

```csharp
using IUnitOfWork uow = new MongoUnitOfWork(conn);

With.Transaction(uow, () =>
{
    product.Status.SetValue("Shipped");
    product.Update(DataObjectLock.UpdateOption.IgnoreLock);
    shipment.Insert();
});
```

For single-document operations no transaction is needed — MongoDB's document model guarantees atomicity per document.

### 23.8 Unsupported Features

The following operations throw `NotSupportedException` on `MongoDataConnection`:

- `ExecSQL(...)` — no SQL dialect
- `ExecStoredProcedure(...)` — MongoDB has no stored procedures
- `GetDynamicObjectBinding(...)` — SQL-specific reader-based binding
- `GetTargetFieldInfo(string, string, string)` — SQL-specific schema introspection
- `ExistsTerm` / `GenerateExistsSQLQuery` — SQL sub-query syntax
- LINQ `ExistsTerm` (translated via SQL correlated subquery)

The LINQ query interface (`conn.Query<T>()`) is not supported because its translation layer generates SQL expressions.

---

## 24. SQLite Provider

### 24.1 Overview

`SQLiteConnection` extends `DBDataConnection` and adds the SQLite dialect on top of the standard SQL generation pipeline provided by the core.  It uses `Microsoft.Data.Sqlite` 8.0.0.

SQLite specifics compared to the other SQL providers:

| Aspect | SQL Server / PostgreSQL | SQLite |
|--------|------------------------|--------|
| Name quoting | `[…]` / `"…"` | `"…"` (double quotes) |
| Row limiting | `SELECT TOP N …` / `LIMIT N` | `SELECT … LIMIT N` (appended) |
| Identity after INSERT | `SCOPE_IDENTITY()` / `LASTVAL()` | `last_insert_rowid()` |
| Schema introspection | `SYSOBJECTS` / `information_schema` | `PRAGMA table_info(table)` |
| IDENTITY_INSERT toggle | `SET IDENTITY_INSERT … ON/OFF` | Not required (empty string) |
| Update lock hint | `WITH (UPDLOCK)` / `FOR UPDATE` | Not applicable (empty string) |
| String concatenation | `+` / `\|\|` | `\|\|` |
| Stored procedures | Supported | `NotSupportedException` |
| In-memory databases | Not supported | `Data Source=:memory:` |

### 24.2 Connecting

```csharp
using Turquoise.ORM;

// File-based database
var conn = new SQLiteConnection("Data Source=app.db");
conn.Connect();

// In-memory database (connection must stay open; destroyed when closed)
var conn = new SQLiteConnection("Data Source=:memory:");
conn.Connect();

// Named shared-cache in-memory (can be reopened)
var conn = new SQLiteConnection("Data Source=mydb;Mode=Memory;Cache=Shared");
conn.Connect();
```

### 24.3 Schema Setup

SQLite does not auto-create tables.  Create the schema before inserting data:

```csharp
conn.ExecSQL(
    "CREATE TABLE IF NOT EXISTS products (" +
    "  id       INTEGER PRIMARY KEY AUTOINCREMENT," +
    "  name     TEXT    NOT NULL," +
    "  price    NUMERIC NOT NULL DEFAULT 0," +
    "  in_stock INTEGER NOT NULL DEFAULT 1)");
```

`INTEGER PRIMARY KEY` in SQLite is an alias for the internal rowid — it auto-increments without `AUTOINCREMENT`.  `INTEGER PRIMARY KEY AUTOINCREMENT` prevents rowid reuse after deletes.

### 24.4 Defining Entities

Entity definitions are provider-agnostic — the same class works with any provider:

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

> **Naming convention:** SQLite is case-insensitive for identifiers by default, but it
> stores names as given.  Using lower-case `[Table]` and `[Column]` values is safest
> for portability.

### 24.5 CRUD

```csharp
// INSERT — ID populated via last_insert_rowid()
var p = new Product(conn);
p.Name.SetValue("Widget");
p.Price.SetValue(9.99m);
p.InStock.SetValue(true);
p.Insert();
// p.ID now holds the generated rowid

// READ by primary key
var p2 = new Product(conn);
p2.ID.SetValue(1);
bool found = p2.Read();

// UPDATE
p.Price.SetValue(14.99m);
p.Update(DataObjectLock.UpdateOption.IgnoreLock);

// DELETE by primary key
p.Delete();

// DELETE by query
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.Delete(term);
```

### 24.6 Transactions

SQLite supports `ReadCommitted` and `Serializable` isolation levels natively. Other levels are mapped to the nearest supported equivalent by `SQLiteAdapterConnection`:

| Requested level | Mapped to |
|----------------|-----------|
| `ReadUncommitted` | `ReadCommitted` |
| `RepeatableRead` | `Serializable` |
| `Snapshot` | `Serializable` |
| `ReadCommitted` | `ReadCommitted` (no mapping) |
| `Serializable` | `Serializable` (no mapping) |

```csharp
// Manual transaction
conn.BeginTransaction();
try
{
    order.Status.SetValue("Shipped");
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);
    shipment.Insert();
    conn.CommitTransaction();
}
catch
{
    conn.RollbackTransaction();
    throw;
}

// With.Transaction helper
var uow = new SQLiteUnitOfWork(conn);
With.Transaction(uow, () =>
{
    order.Status.SetValue("Shipped");
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);
    shipment.Insert();
});

// Automatic via conn.UnitOfWork
conn.UnitOfWork = new SQLiteUnitOfWork(conn);
product.Insert();  // auto: connect → begin tx → insert → commit → disconnect
```

### 24.7 DI Registration

```csharp
// Program.cs
builder.Services
    .AddTurquoiseSQLite("Data Source=app.db")
    .AddServices(typeof(Program).Assembly);
```

Or with an in-memory database for testing:

```csharp
builder.Services
    .AddTurquoiseSQLite("Data Source=testdb;Mode=Memory;Cache=Shared");
```

### 24.8 Type Affinity Mapping

SQLite uses type affinity rather than strict types. `SQLiteConnection.MapNativeType` applies the following mapping rules (first match wins):

| Declared type contains | Mapped CLR type |
|------------------------|-----------------|
| `INT` | `long` |
| `REAL`, `FLOA`, `DOUB` | `double` |
| `NUM`, `DEC`, `MONEY` | `decimal` |
| `BOOL` | `bool` |
| `DATE`, `TIME` | `DateTime` |
| `GUID`, `UUID` | `Guid` |
| `BLOB` or empty | `byte[]` |
| Anything else (TEXT, VARCHAR, …) | `string` |

### 24.9 Limitations

- **Stored procedures** — SQLite has no stored procedure support. Calling `ExecStoredProcedure` throws `NotSupportedException`.
- **`GetUpdateLock()`** — SQLite uses file-level locking, not row-level locks. The method returns an empty string (no hint appended to SELECT).
- **`IDENTITY_INSERT` toggle** — not needed; `PreInsertIdentityCommand`/`PostInsertIdentityCommand` return empty strings.
- **Isolation level mapping** — levels not supported by SQLite are silently promoted (see §24.6).
- **In-memory lifetime** — a `Data Source=:memory:` connection is destroyed when it closes.  Use a named shared-cache string (`Mode=Memory;Cache=Shared`) when the connection must be reopened or shared.

---

*Turquoise.ORM — .NET 8 / SQL Server / PostgreSQL / MongoDB / SQLite*
