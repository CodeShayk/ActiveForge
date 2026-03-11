# Field Subsets — Partial Fetches and Partial Updates

A `FieldSubset` controls which columns are included in a `SELECT` or `UPDATE` statement.
Use it to:

- Reduce bandwidth by fetching only the columns you need from a wide table.
- Exclude large `TEXT` / `NVARCHAR(MAX)` / `VARBINARY` columns from list queries.
- Update only specific columns without touching others (avoids overwriting concurrent changes).

---

## Creating a FieldSubset

The connection provides factory methods:

```csharp
var template = new Product(conn);

// All fields included (default ORM behaviour)
FieldSubset all = conn.FieldSubset(template, FieldSubset.InitialState.IncludeAll);

// No fields included — add specific fields yourself
FieldSubset none = conn.FieldSubset(template, FieldSubset.InitialState.ExcludeAll);

// ORM default (includes non-lazy fields, excludes blob columns by default)
FieldSubset def = conn.DefaultFieldSubset(template);

// A subset containing exactly one field
FieldSubset nameOnly = conn.FieldSubset(template, template, template.Name);
```

The three-argument overload `FieldSubset(rootObject, enclosingObject, field)` is the
most common way to target a single field. For a flat (non-embedded) entity the
`enclosingObject` is the same as `rootObject`.

---

## Composing Subsets

`FieldSubset` supports set operators:

| Operator | Meaning |
|----------|---------|
| `a + b` | Union — includes any field in either `a` or `b` |
| `a & b` | Intersection — includes only fields in both `a` and `b` |
| `a \| b` | Join union — like `+` but also merges join inclusions |
| `a - b` | Exclusion — includes fields in `a` that are not in `b` |

```csharp
// Fetch only Name and Price
FieldSubset name  = conn.FieldSubset(template, template, template.Name);
FieldSubset price = conn.FieldSubset(template, template, template.Price);
FieldSubset nameAndPrice = name + price;

// Fetch everything except the large Description column
FieldSubset allButDesc = conn.DefaultFieldSubset(template)
                       - conn.FieldSubset(template, template, template.Description);
```

---

## Partial Fetch (SELECT)

Pass a `FieldSubset` to any read or query operation:

```csharp
// Read a single record — only Name and Price columns fetched
var p = new Product(conn);
p.ID.SetValue(42);
conn.Read(p, nameAndPrice);

Console.WriteLine(p.Name);          // populated
Console.WriteLine(p.Price);         // populated
Console.WriteLine(p.Description);   // IsNull() == true — was not fetched

// QueryAll with subset
RecordCollection results = conn.QueryAll(template, term, sort, 0, nameAndPrice);

// QueryFirst with subset
conn.QueryFirst(template, term, sort, nameAndPrice);

// QueryPage with subset
var page = conn.QueryPage(template, term, sort, 0, 20, nameAndPrice);
```

---

## Partial Update (UPDATE)

`UpdateChanged()` automatically updates only fields whose value has been
explicitly set since the last read — no `FieldSubset` needed:

```csharp
var p = new Product(conn);
p.ID.SetValue(42);
p.Read();

p.Price.SetValue(99.99m);  // only this field changed
p.UpdateChanged();          // UPDATE Products SET Price=@Price WHERE ID=@ID
                            // Name, Description, etc. are NOT in the UPDATE
```

For explicit subset control, use `Update` with the overload that accepts a subset
(if exposed by your concrete `DataConnection` subclass), or use the queue:

```csharp
p.Price.SetValue(99.99m);
p.QueueForUpdate();
conn.ProcessActionQueue();
```

---

## Field Subsets on Embedded (Joined) Objects

When a `Record` embeds another `Record` as a field (joined table), you can
include or exclude the entire embedded object's fields:

```csharp
// Include all fields of the embedded Category object
FieldSubset withCategory = conn.FieldSubset(
    rootObject:      product,
    enclosing:       product,
    enclosed:        product.Category   // a nested Record
);

// Or exclude it to skip the JOIN entirely
FieldSubset withoutCategory = conn.FieldSubset(
    rootObject:  product,
    enclosing:   product,
    enclosed:    product.Category,
    state:       FieldSubset.InitialState.ExcludeAll
);
```

---

## InitialState Reference

| Value | Meaning |
|-------|---------|
| `IncludeAll` | All scalar fields and all joins included |
| `ExcludeAll` | Nothing included; add fields explicitly with the `+` operator |
| `Default` | ORM default — excludes `[NoPreload]` fields and resolves `[EagerLoad]` annotations |
| `IncludeAllJoins` | All joins included but scalar-field inclusion unchanged |
| `ExcludeAllJoins` | All joins excluded — fetches only the root table's columns |

---

## Performance Tips

| Scenario | Recommended subset |
|----------|--------------------|
| List view (many rows, few columns) | `ExcludeAll` + add only display columns |
| Detail view (single row, all data) | `IncludeAll` or `DefaultFieldSubset` |
| Update a single column | `UpdateChanged()` — no subset needed |
| Exclude large blobs from lists | `DefaultFieldSubset(obj) - blobField` |
| JOIN is slow and data not needed | `ExcludeAllJoins` |
