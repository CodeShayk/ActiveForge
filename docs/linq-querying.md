# LINQ Query Support

This document covers the `ActiveForge.Linq` namespace.

---

## Overview

ActiveForge ORM supports LINQ-style query composition via `conn.Query<T>()`.
LINQ operators are translated into native `QueryTerm` / `SortOrder` objects at translation time, then executed as a single optimised ORM query when you iterate the result.

---

## Quick Start

```csharp
using ActiveForge.Linq;

// Basic predicate + sort + pagination
List<Product> products = conn.Query<Product>()
    .Where(p => p.IsActive == true && p.Price > 10m)
    .OrderBy(p => p.Name)
    .Skip(0)
    .Take(20)
    .ToList();

// Single comparison
var expensive = conn.Query<Product>()
    .Where(p => p.Price >= 100m)
    .ToList();

// IN clause
var featured = new List<string> { "Widget", "Gadget", "Gizmo" };
var items = conn.Query<Product>()
    .Where(p => featured.Contains(p.Name))
    .ToList();
```

---

## Entry Point: `conn.Query<T>()`

```csharp
// DataConnectionExtensions (ActiveForge.Linq namespace)
public static OrmQueryable<T> Query<T>(this DataConnection connection)
    where T : DataObject;

// Overload with pre-constructed template (useful with custom factories or join queries)
public static OrmQueryable<T> Query<T>(this DataConnection connection, T template)
    where T : DataObject;
```

Internally, `Query<T>()` creates a template instance (`conn.Create(typeof(T))`), wraps it in an `OrmQueryable<T>`, and returns it.

> **Tip for join queries**: use the template overload with an explicitly-connected template:
> `conn.Query(new ProductWithCategory(conn))`. This ensures `QueryTerm` initialisation
> has a live connection to resolve column metadata.

---

## Supported LINQ Operators

| LINQ operator | ActiveForge equivalent | Notes |
|---------------|---------------------|-------|
| `Where(x => x.Field == value)` | `EqualTerm` | |
| `Where(x => x.Field != value)` | `!EqualTerm` | |
| `Where(x => x.Field > value)` | `GreaterThanTerm` | |
| `Where(x => x.Field >= value)` | `GreaterOrEqualTerm` | |
| `Where(x => x.Field < value)` | `LessThanTerm` | |
| `Where(x => x.Field <= value)` | `LessOrEqualTerm` | |
| `Where(x => x.Field == null)` | `IsNullTerm` | |
| `Where(x => x.Field != null)` | `!IsNullTerm` | |
| `Where(x => cond1 && cond2)` | `AndTerm` | |
| `Where(x => cond1 \|\| cond2)` | `OrTerm` | |
| `Where(x => !cond)` | `NotTerm` | |
| `Where(x => list.Contains(x.Field))` | `InTerm` | |
| `OrderBy(x => x.Field)` | `OrderAscending` | |
| `OrderByDescending(x => x.Field)` | `OrderDescending` | |
| `ThenBy(x => x.Field)` | Appended `OrderAscending` | Composites with primary sort |
| `ThenByDescending(x => x.Field)` | Appended `OrderDescending` | |
| `Take(n)` | `pageSize` parameter | |
| `Skip(n)` | `start` parameter in `QueryPage` | |

---

## Where Predicates

### Comparisons

```csharp
conn.Query<Product>().Where(p => p.Name    == "Widget")
conn.Query<Product>().Where(p => p.Price   >  50m)
conn.Query<Product>().Where(p => p.Qty     >= 1)
conn.Query<Product>().Where(p => p.Qty     <  100)
conn.Query<Product>().Where(p => p.Qty     <= 99)
conn.Query<Product>().Where(p => p.Name    != "Discontinued")
```

### Null checks

```csharp
conn.Query<Product>().Where(p => p.Name == (TString)null)   // IS NULL
conn.Query<Product>().Where(p => p.Name != (TString)null)   // IS NOT NULL
```

### Logical AND / OR / NOT

```csharp
// AND
conn.Query<Product>().Where(p => p.IsActive == true && p.Price > 10m)

// OR
conn.Query<Product>().Where(p => p.Category == "A" || p.Category == "B")

// NOT
conn.Query<Product>().Where(p => !(p.Name == "Discontinued"))

// Chained Where calls are ANDed automatically
conn.Query<Product>()
    .Where(p => p.IsActive == true)
    .Where(p => p.Price > 10m)     // AND p.Price > 10
```

### IN clause (Contains)

```csharp
var names = new List<string> { "Widget", "Gadget" };
conn.Query<Product>().Where(p => names.Contains(p.Name))
// → WHERE Name IN (@IN_Name0, @IN_Name1)
```

### Captured local variables

```csharp
string targetName = "Widget";
decimal minPrice  = 10m;

conn.Query<Product>()
    .Where(p => p.Name == targetName && p.Price >= minPrice)
    .ToList();
// Variables are evaluated at translation time, not lazily.
```

---

## Sorting

```csharp
// Single sort
conn.Query<Product>().OrderBy(p => p.Name)
conn.Query<Product>().OrderByDescending(p => p.Price)

// Multi-column sort
conn.Query<Product>()
    .OrderBy(p => p.Category)
    .ThenBy(p => p.Name)
    .ThenByDescending(p => p.Price)
```

---

## Pagination

```csharp
// Page 2, 20 items per page (zero-based start)
conn.Query<Product>()
    .Where(p => p.IsActive == true)
    .OrderBy(p => p.Name)
    .Skip(20)
    .Take(20)
    .ToList();
```

When `Skip` or `Take` is set, execution uses `conn.QueryPage(start, count, ...)`.
Without `Skip`/`Take`, execution uses `conn.LazyQueryAll(...)` for memory-efficient streaming.

---

## Lazy Enumeration

`IQueryable<T>` is lazy — the database is not queried until you start iterating:

```csharp
// No DB call yet:
IQueryable<Product> q = conn.Query<Product>().Where(p => p.Price > 10m);

// DB call happens here:
foreach (Product p in q) { ... }

// Or materialise:
List<Product> list = q.ToList();
```

---

## Full Chain Example

```csharp
using ActiveForge.Linq;

var conn     = new SqlServerConnection(connectionString);
conn.Connect();

string category = "Electronics";
decimal maxPrice = 999.99m;

var products = conn.Query<Product>()
    .Where(p => p.Category  == category)
    .Where(p => p.Price     <= maxPrice)
    .Where(p => p.IsActive  == true)
    .OrderBy(p => p.Price)
    .ThenBy(p => p.Name)
    .Skip(0)
    .Take(50)
    .ToList();

foreach (var product in products)
    Console.WriteLine($"{product.Name.Value} — ${product.Price.Value:F2}");
```

---

## Querying Across Joins

When your entity embeds another `DataObject` (triggering a JOIN), LINQ predicates and sort selectors can navigate into the joined type directly.

### Cross-Join `Where` Predicates

```csharp
// x.Category.Name navigates the embedded DataObject → maps to Categories.Name
conn.Query(new ProductWithCategory(conn))
    .Where(x => x.Category.Name == "Books")
    .ToList();

// Null check on a joined column
conn.Query(new ProductWithOptionalCategory(conn))
    .Where(x => x.Category.Name == (TString)null)   // IS NULL
    .ToList();

// Mixed: own column AND joined column
conn.Query(new ProductWithCategory(conn))
    .Where(x => x.Price < 20m && x.Category.Name == "Books")
    .ToList();
```

### Cross-Join `OrderBy` Selectors

```csharp
conn.Query(new ProductWithCategory(conn))
    .OrderBy(x => x.Category.Name)
    .ThenByDescending(x => x.Price)
    .ToList();
```

### Query-Time Join-Type Override

Use `.InnerJoin<T>()` or `.LeftOuterJoin<T>()` to override the join type for a single query without modifying the entity class:

```csharp
// Entity class uses INNER JOIN by convention — override to LEFT OUTER for this query
conn.Query(new ProductWithCategory(conn))
    .LeftOuterJoin<Category>()
    .Where(x => x.Price > 5m)
    .OrderBy(x => x.Category.Name)
    .ToList();

// Entity class has [JoinSpec(LeftOuterJoin)] — restore INNER JOIN for this query
conn.Query(new ProductWithOptionalCategory(conn))
    .InnerJoin<Category>()
    .ToList();
```

Override calls can appear anywhere in the chain; calling the same type twice replaces the earlier override.

See [joins.md](joins.md) for the full joins reference including convention-based joins, `[JoinSpec]` attributes, multi-FK joins, and EXISTS sub-queries.

---

## Combining with QueryTerm API

You can mix LINQ and the existing `QueryTerm` API:

```csharp
// Build the WHERE predicate using LINQ:
IQueryable<Product> q = conn.Query<Product>()
    .Where(p => p.IsActive == true);

// Retrieve the accumulated term and combine with additional QueryTerms:
OrmQueryable<Product> orm = (OrmQueryable<Product>)q;
QueryTerm combined = orm.WhereTerm & new EqualTerm(product, product.Category, "Electronics");

// Execute using the classic API:
ObjectCollection results = conn.QueryAll(product, combined, null, 0, null);
```

---

## How It Works

1. `conn.Query<T>()` creates an `OrmQueryable<T>` wrapping a template `DataObject` instance.
2. Each LINQ operator (`.Where(...)`, `.OrderBy(...)`, etc.) calls `IQueryProvider.CreateQuery()` on `OrmQueryProvider<T>`.
3. `OrmQueryProvider<T>` recursively walks the expression tree, translating each operator into ORM state (WhereTerm, SortOrder, PageSize, SkipCount).
4. When you enumerate (`ToList()`, `foreach`, etc.), `OrmQueryable<T>.GetEnumerator()` calls `conn.LazyQueryAll<T>(...)` or `conn.QueryPage(...)` with the accumulated state.

The expression tree is traversed **at execution time**, so local variables are captured correctly.

---

## Limitations

| Limitation | Notes |
|------------|-------|
| No `GroupBy` | Not supported; use raw SQL or `ExecSQL`. |
| No `Join` clause | Cross-join predicates and sorts work via embedded `DataObject` fields. See [joins.md](joins.md). |
| No `Select` projection | Returns full typed `DataObject` instances; field subsets can be applied at the `conn.Query<T>(template)` level. |
| No `Count()`, `First()`, etc. | Call the standard ORM methods (`conn.QueryCount(...)`, `conn.QueryFirst(...)`) directly. |
| No async support | Use the synchronous API; async is planned for a future release. |

---

## Architecture: Key Classes

| Class | Role |
|-------|------|
| `OrmQueryable<T>` | `IQueryable<T>` / `IOrderedQueryable<T>` implementation; holds accumulated state |
| `OrmQueryProvider<T>` | `IQueryProvider`; translates method calls into state changes |
| `ExpressionToQueryTermVisitor` | `ExpressionVisitor` subclass; translates predicate expressions to `QueryTerm` |
| `ExpressionToSortVisitor` | Translates key-selector lambdas to `SortOrder` |
| `CombinedSortOrder` | Composes multiple `SortOrder` instances for multi-column ORDER BY |
| `DataConnectionExtensions` | Adds `.Query<T>()` extension method to `DataConnection` |
| `JoinOverride` | Struct holding a `(Type, JoinTypeEnum)` pair for query-time join type overrides |
