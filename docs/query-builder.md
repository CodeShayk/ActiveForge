# Query Builder

ActiveForge uses a predicate tree for WHERE clauses. Each `QueryTerm` is a composable
object that knows how to emit parameterised SQL and bind its parameters.

## Term Types

### Comparison terms

```csharp
// The first argument is always the "template" Record that owns the field.
var template = new Product(conn);

new EqualTerm         (template, template.Name,  "Widget")   // Name = @Name1
new GreaterThanTerm   (template, template.Price, 10m)        // Price > @Price1
new GreaterOrEqualTerm(template, template.Price, 10m)        // Price >= @Price1
new LessThanTerm      (template, template.Price, 50m)        // Price < @Price1
new LessOrEqualTerm   (template, template.Price, 50m)        // Price <= @Price1
```

### NULL check

```csharp
new IsNullTerm(template, template.Description)               // Description IS NULL
```

### String matching

```csharp
// LikeTerm — caller controls the % wildcards
new LikeTerm(template, template.Name, "Widget%")             // Name LIKE 'Widget%'
new LikeTerm(template, template.Name, "%Widget%")            // Name LIKE '%Widget%'

// ContainsTerm — wraps the value in % automatically
new ContainsTerm(template, template.Name, "widget")          // Name LIKE '%widget%'
```

### IN list

```csharp
var ids = new List<object> { 1, 2, 3 };
new InTerm(template, template.CategoryID, ids)               // CategoryID IN (@p1,@p2,@p3)
```

### Full-text search (SQL Server)

```csharp
new FullTextTerm(template, template.Description, "natural language query")
// → FREETEXT(Description, @value)
```

### EXISTS sub-query

```csharp
// "Products that have at least one OrderLine"
var orderLine = new OrderLine(conn);
var subQuery  = new Query<OrderLine>(orderLine, conn)
                    .Where(new EqualTerm(orderLine, orderLine.ProductID, /* outer link */));

new ExistsTerm<OrderLine>(template, template.ID, orderLine, orderLine.ProductID, subQuery)
// → EXISTS (SELECT 1 FROM OrderLines WHERE ProductID = outer.ID)
```

### Raw SQL

```csharp
// Embed a literal SQL predicate — use sparingly (no parameter binding, injection risk)
new RawSqlTerm("YEAR(CreatedAt) = 2024")
```

## Logical Composition

Terms compose with C# operators:

```csharp
QueryTerm a = new EqualTerm(template, template.InStock, true);
QueryTerm b = new GreaterThanTerm(template, template.Price, 10m);
QueryTerm c = new ContainsTerm(template, template.Name, "widget");

// AND
QueryTerm both  = a & b;              // InStock=true AND Price>10

// OR
QueryTerm either = b | c;             // Price>10 OR Name LIKE '%widget%'

// NOT
QueryTerm notA   = !a;                // NOT (InStock=true)

// Nesting
QueryTerm complex = (a & b) | !c;    // (InStock AND Price>10) OR NOT(name contains widget)

// Null-safe: null & term → term (not an AndTerm)
QueryTerm safe = null & a;            // → a
```

Compound terms (`AndTerm`, `OrTerm`, `NotTerm`) are created automatically by the operators.

## Executing Queries

```csharp
var template = new Product(conn);
QueryTerm term = new EqualTerm(template, template.InStock, true);
SortOrder sort = new OrderAscending(template, template.Price);

// All matching rows
RecordCollection all = conn.QueryAll(template, term, sort, 0 /*pageSize*/, null);

// Just count
int count = conn.QueryCount(template, term);

// First match only
bool found = conn.QueryFirst(template, term, sort, null);

// Paginated: skip 20, take 10
RecordCollection page = conn.QueryPage(template, term, sort, 20 /*start*/, 10 /*count*/, null);
Console.WriteLine($"Page has {page.Count} rows; IsMoreData={page.IsMoreData}");

// Stream without loading all into memory
foreach (Product p in conn.LazyQueryAll(template, term, sort, 0, null))
    Console.WriteLine(p.Name);
```

## Sorting

```csharp
var sort = new OrderAscending(template, template.Name);    // ORDER BY Name ASC
var desc  = new OrderDescending(template, template.Price); // ORDER BY Price DESC
```

Only one `SortOrder` object can be passed directly; for multi-column sorting, combine
sort orders using the methods on the concrete `SortOrder` subclass or pass a `RawSqlTerm`
with a raw ORDER BY clause.

## Fluent Query Builder

`Query<T>` wraps a Record and provides a fluent API used primarily for EXISTS sub-queries:

```csharp
var q = new Query<OrderLine>(orderLine, conn)
            .Where(new EqualTerm(...))
            .OrderBy(new OrderAscending(...))
            .Skip(0)
            .Take(10)
            .Select(fieldSubset);

RecordCollection results = q.QueryAll();
int count = q.QueryCount();
```

## Pagination Details

`QueryPage` returns an `RecordCollection` with extra properties:

| Property | Description |
|----------|-------------|
| `StartRecord` | Index of first record in this page (0-based) |
| `PageSize` | Requested page size |
| `IsMoreData` | `true` if there are rows after this page |
| `TotalRowCount` | Total number of matching rows (if `TotalRowCountValid`) |
| `TotalRowCountValid` | `true` if `TotalRowCount` was computed |
