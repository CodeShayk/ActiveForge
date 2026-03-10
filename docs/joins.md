# Joins

This document covers how ActiveForge ORM handles SQL JOINs — both the automatic, convention-based mechanism and the LINQ-level query-time overrides introduced in v1.2.

---

## Overview

ActiveForge ORM derives JOIN SQL from the **structure of your entity class**, not from a query builder.
When you embed a `DataObject` field inside another `DataObject`, the ORM automatically emits a JOIN between the two tables when you query the outer type.

There are three layers of JOIN control:

| Layer | Mechanism | When to use |
|-------|-----------|-------------|
| **Convention** | Naming rule: `XID` FK + embedded `X` field | Simple 1:1 FK joins, zero boilerplate |
| **Attribute** | `[JoinSpec]` on the class | Override join type; non-standard column names |
| **Query-time** | `.InnerJoin<T>()` / `.LeftOuterJoin<T>()` in LINQ | Per-query override without changing the entity class |

---

## 1. Convention-Based INNER JOIN

If your entity class has:
- a `TForeignKey` field named `XID` (e.g. `CategoryID`)
- a `DataObject` field named `X` (e.g. `Category`) whose class name ends in `X`

the ORM automatically produces:

```sql
INNER JOIN Categories ON Products.CategoryID = Categories.ID
```

No attribute required.

```csharp
[Table("Products")]
public class ProductWithCategory : IdentDataObject
{
    [Column("Name")]        public TString     Name       = new TString();
    [Column("Price")]       public TDecimal    Price      = new TDecimal();
    [Column("CategoryID")]  public TForeignKey CategoryID = new TForeignKey();

    // Embedding this triggers the auto-join.
    // After a query, Category.Name etc. are populated from the joined row.
    public Category Category = new Category();

    public ProductWithCategory() { }
    public ProductWithCategory(DataConnection conn) : base(conn) { }
}
```

```csharp
var template = new ProductWithCategory(conn);
var results  = conn.QueryAll(template, null, null, 0, null);
// SELECT ... FROM Products INNER JOIN Categories ON Products.CategoryID = Categories.ID
```

---

## 2. Attribute-Based Join Override (`[JoinSpec]`)

Use `[JoinSpec]` to override the join type or specify non-standard column names.

```csharp
[Table("Products")]
[JoinSpec("CategoryID", "Category", "ID", JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
public class ProductWithOptionalCategory : IdentDataObject
{
    [Column("Name")]        public TString     Name       = new TString();
    [Column("Price")]       public TDecimal    Price      = new TDecimal();
    [Column("CategoryID")]  public TForeignKey CategoryID = new TForeignKey();

    public Category Category = new Category();

    public ProductWithOptionalCategory() { }
    public ProductWithOptionalCategory(DataConnection conn) : base(conn) { }
}
```

```csharp
// Returns every product row; Category fields are null/empty where there is no match.
var results = conn.QueryAll(new ProductWithOptionalCategory(conn), null, null, 0, null);
// SELECT ... FROM Products LEFT OUTER JOIN Categories ON Products.CategoryID = Categories.ID
```

### `[JoinSpec]` parameters

```csharp
[JoinSpec(
    sourceField:   "CategoryID",   // FK column on the outer table
    targetField:   "Category",     // embedded DataObject field name
    joinField:     "ID",           // PK column on the inner table
    joinType:      JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
```

---

## 3. Multiple FK Joins

Embed multiple `DataObject` fields to produce multiple JOINs in one query.

```csharp
[Table("OrderLines")]
public class OrderLineWithDetails : IdentDataObject
{
    [Column("OrderID")]   public TForeignKey OrderID   = new TForeignKey();
    [Column("ProductID")] public TForeignKey ProductID = new TForeignKey();
    [Column("Quantity")]  public TInt        Quantity  = new TInt();
    [Column("UnitPrice")] public TDecimal    UnitPrice = new TDecimal();

    public Order   Order   = new Order();    // → INNER JOIN Orders
    public Product Product = new Product();  // → INNER JOIN Products

    public OrderLineWithDetails() { }
    public OrderLineWithDetails(DataConnection conn) : base(conn) { }
}
```

```csharp
var lines = conn.QueryAll(new OrderLineWithDetails(conn), null, null, 0, null);
// SELECT ... FROM OrderLines
//   INNER JOIN Orders   ON OrderLines.OrderID   = Orders.ID
//   INNER JOIN Products ON OrderLines.ProductID = Products.ID
```

---

## 4. Filtering on Joined Columns (QueryTerm API)

Pass the **embedded DataObject** (not the root template) as the `target` argument when building a `QueryTerm` against a joined column:

```csharp
var template = new ProductWithCategory(conn);

// Filter on the joined table's column — use template.Category as the target
var term    = new EqualTerm(template.Category, template.Category.Name, "Books");
var results = conn.QueryAll(template, term, null, 0, null);
// WHERE Categories.Name = 'Books'

// Combine with an own-column filter using &
var inStock = new EqualTerm(template, template.InStock, true);
var both    = conn.QueryAll(template, term & inStock, null, 0, null);
// WHERE Categories.Name = 'Books' AND Products.InStock = 1
```

> **Note**: The first `EqualTerm` argument must be the _containing_ `DataObject` instance
> (the embedded field's owner), not the root query template.
> For top-level fields, pass the root template; for joined fields, pass the embedded object.

---

## 5. LINQ Joins (v1.2+)

When using the LINQ query layer (`conn.Query<T>()`), you can:

- Access joined columns directly in `Where` predicates and `OrderBy` selectors
- Override the join type per-query using `.InnerJoin<T>()` / `.LeftOuterJoin<T>()`

### 5.1 Cross-Join `Where` Predicates

Navigate to the embedded DataObject field in your lambda — the ORM resolves the correct table column automatically:

```csharp
using ActiveForge.Linq;

// Filter on a joined column — x.Category.Name navigates the embedded DataObject
var books = conn.Query<ProductWithCategory>()
    .Where(x => x.Category.Name == "Books")
    .ToList();
// WHERE Categories.Name = @p0

// Combine joined and direct fields in one predicate
var cheapBooks = conn.Query<ProductWithCategory>()
    .Where(x => x.Category.Name == "Books" && x.Price < 20m)
    .ToList();
// WHERE Categories.Name = @p0 AND Products.Price < @p1

// Null checks on joined columns
var noCategory = conn.Query<ProductWithOptionalCategory>()
    .Where(x => x.Category.Name == (TString)null)
    .ToList();
// WHERE Categories.Name IS NULL
```

### 5.2 Cross-Join `OrderBy` Selectors

```csharp
// Sort by a joined column
var byCategory = conn.Query<ProductWithCategory>()
    .OrderBy(x => x.Category.Name)
    .ThenBy(x => x.Price)
    .ToList();
// ORDER BY Categories.Name ASC, Products.Price ASC

// Descending on a joined column
var byRatingDesc = conn.Query<ProductWithCategory>()
    .OrderByDescending(x => x.Category.Name)
    .ToList();
```

### 5.3 Query-Time Join-Type Override

Use `.InnerJoin<TJoined>()` or `.LeftOuterJoin<TJoined>()` to override the join type **for a single query** without touching the entity class.

```csharp
// Entity class uses INNER JOIN by convention, but override to LEFT OUTER for this query
var allProducts = conn.Query<ProductWithCategory>()
    .LeftOuterJoin<Category>()
    .ToList();
// LEFT OUTER JOIN Categories ON Products.CategoryID = Categories.ID

// Chain join overrides before or after Where / OrderBy
var booksOrUncat = conn.Query<ProductWithCategory>()
    .LeftOuterJoin<Category>()
    .Where(x => x.Category.Name == "Books" || x.Category.Name == (TString)null)
    .OrderBy(x => x.Name)
    .ToList();

// Restore a LEFT OUTER JOIN class-level attribute to INNER for this query
var strictJoin = conn.Query<ProductWithOptionalCategory>()
    .InnerJoin<Category>()
    .Where(x => x.Price > 10m)
    .ToList();
```

#### Chaining rules

- Call `.InnerJoin<T>()` / `.LeftOuterJoin<T>()` **before or after** `.Where()`, `.OrderBy()`, `.Take()`, `.Skip()`.
- Calling the same override twice replaces the earlier one (last write wins).
- Multiple different embedded types can each have their own override:

```csharp
var lines = conn.Query<OrderLineWithDetails>()
    .InnerJoin<Order>()
    .LeftOuterJoin<Product>()    // products may have been deleted
    .Where(x => x.Quantity > 1)
    .OrderBy(x => x.Order.OrderDate)
    .ToList();
```

### 5.4 Template Connection

When calling `conn.Query<T>()`, the ORM creates a template instance via `conn.Create(typeof(T))`.
If your entity type requires a non-default factory or constructor argument (e.g. the connection itself), use the explicit-template overload:

```csharp
// Safe — template is created with the connection
var q = conn.Query<ProductWithCategory>(new ProductWithCategory(conn));
```

---

## 6. EXISTS Sub-Query (Semi-Join)

Use `ExistsTerm<TInner>` to produce `WHERE EXISTS (SELECT 1 FROM ... WHERE ...)`:

```csharp
var orderTemplate = new Order(conn);
var lineObj       = new OrderLine(conn);

// Sub-query: find order lines with qty > 1
var bigQty   = new GreaterThanTerm(lineObj, lineObj.Quantity, 1);
var subQuery = new Query<OrderLine>(lineObj, conn).Where(bigQty);

// Correlate outer Orders.ID ↔ inner OrderLines.OrderID
var existsTerm = new ExistsTerm<OrderLine>(orderTemplate, lineObj, lineObj.OrderID, subQuery);
var orders     = conn.QueryAll(orderTemplate, existsTerm, null, 0, null);
// WHERE EXISTS (SELECT 1 FROM OrderLines WHERE Quantity > 1 AND OrderLines.OrderID = Orders.ID)
```

---

## 7. Limitations

| Limitation | Notes |
|------------|-------|
| No `JOIN` between unrelated tables | Joins must be expressed via embedded `DataObject` fields |
| No self-joins | A type cannot embed itself |
| No many-to-many navigation | Bridge-table entities with two FKs can model this |
| LINQ `Where` LHS must be a `TField` | `x.Category.SomeField` — works; arbitrary expressions — not supported |
| No LINQ `GroupJoin` / `SelectMany` | Use raw `ExecSQL` for complex joins |
| Query-time overrides bypass class-level `[JoinSpec]` | The override replaces, not merges with, the class attribute |

---

## See Also

- [query-builder.md](query-builder.md) — `QueryTerm` API, `EqualTerm`, `ExistsTerm`, etc.
- [linq-querying.md](linq-querying.md) — Full LINQ operator reference
- [field-types.md](field-types.md) — `TField` hierarchy, `TForeignKey`, etc.
