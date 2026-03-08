# Transactions and Action Queue

## Explicit Transactions

Wrap multiple operations in a single atomic unit using `BeginTransaction` /
`CommitTransaction` / `RollbackTransaction`.

```csharp
TransactionBase tx = conn.BeginTransaction();
try
{
    var order = new Order(conn);
    order.CustomerName.SetValue("Alice Smith");
    order.OrderDate.SetValue(DateTime.UtcNow);
    order.TotalAmount.SetValue(0m);
    order.Insert();

    var line = new OrderLine(conn);
    line.OrderID.SetValue((int)order.ID.GetValue());
    line.ProductID.SetValue(7);
    line.Quantity.SetValue(2);
    line.UnitPrice.SetValue(29.99m);
    line.Insert();

    // Update the order total
    order.TotalAmount.SetValue(59.98m);
    order.Update(DataObjectLock.UpdateOption.IgnoreLock);

    conn.CommitTransaction(tx);
}
catch
{
    conn.RollbackTransaction(tx);
    throw;
}
```

All `DataObject` instances bound to the same `DataConnection` automatically participate
in that connection's active transaction — there is no need to pass the transaction handle
to each entity.

### Isolation levels

```csharp
using System.Data;

var tx = conn.BeginTransaction(IsolationLevel.RepeatableRead);
// ... operations ...
conn.CommitTransaction(tx);
```

Supported levels are whatever `Microsoft.Data.SqlClient` exposes:
`ReadUncommitted`, `ReadCommitted` (default), `RepeatableRead`, `Serializable`, `Snapshot`.

### Checking transaction state

```csharp
TransactionStates state = conn.TransactionState(tx);
// state is one of: None, Active, Committed, RolledBack
```

### Nested transactions

`DBDataConnection` tracks a transaction depth counter.  Calling `BeginTransaction`
inside an already-active transaction increments the counter rather than opening a
new database transaction.  Only the outermost `CommitTransaction` or
`RollbackTransaction` actually issues `COMMIT` / `ROLLBACK` to the database.

```csharp
var outer = conn.BeginTransaction();      // depth: 1 — real BEGIN TRANSACTION

var inner = conn.BeginTransaction();      // depth: 2 — logical only
// ... inner work ...
conn.CommitTransaction(inner);            // depth: 1 — no DB commit yet

// ... outer work ...
conn.CommitTransaction(outer);            // depth: 0 — real COMMIT
```

> If any level rolls back, the entire transaction rolls back.

---

## Optimistic Locking

`Update` accepts a `DataObjectLock.UpdateOption` that controls how the ORM
handles concurrent writes:

| Option | Behaviour |
|--------|-----------|
| `IgnoreLock` | No locking check — simplest option for non-concurrent tables |
| `ReleaseLock` | Checks an optimistic lock column and releases it after update |
| `RetainLock` | Checks an optimistic lock column and keeps the lock for further updates |

```csharp
// Most common — no concurrent locking needed
product.Price.SetValue(14.99m);
product.Update(DataObjectLock.UpdateOption.IgnoreLock);

// Optimistic lock — throws ObjectLockException if another writer changed the row
product.Update(DataObjectLock.UpdateOption.ReleaseLock);
```

Catch `ObjectLockException` to handle a lost update gracefully:

```csharp
try
{
    product.Update(DataObjectLock.UpdateOption.ReleaseLock);
}
catch (ObjectLockException)
{
    // Another process updated this row — re-read and retry
    product.Read();
    product.Price.SetValue(14.99m);
    product.Update(DataObjectLock.UpdateOption.ReleaseLock);
}
```

---

## Action Queue

The action queue lets you accumulate INSERT / UPDATE / DELETE operations and flush
them all in a single database round-trip, wrapped in an automatic transaction.

### Queuing operations

```csharp
// QueueForInsert — deferred INSERT (object is NOT yet in the DB)
var p1 = new Product(conn);
p1.Name.SetValue("Widget A");
p1.Price.SetValue(9.99m);
p1.QueueForInsert();

var p2 = new Product(conn);
p2.Name.SetValue("Widget B");
p2.Price.SetValue(14.99m);
p2.QueueForInsert();

// QueueForUpdate — deferred UPDATE
p1.Price.SetValue(7.99m);   // change something first
p1.QueueForUpdate();

// QueueForDelete — deferred DELETE
var old = new Product(conn);
old.ID.SetValue(99);
old.QueueForDelete();
```

### Flushing the queue

```csharp
conn.ProcessActionQueue();   // executes all queued operations atomically
```

All operations in the queue are executed in the order they were queued.
If any operation fails, the entire batch is rolled back.

### Clearing the queue

```csharp
conn.ClearActionQueue();     // discard all pending operations without executing
```

Use this in error-handling paths to ensure a partially-built queue does not
accidentally persist on the next `ProcessActionQueue` call.

### Typical batch-insert pattern

```csharp
var items = GetItemsFromSomewhere();   // e.g. parsed from CSV

foreach (var item in items)
{
    var p = new Product(conn);
    p.Name.SetValue(item.Name);
    p.Price.SetValue(item.Price);
    p.InStock.SetValue(true);
    p.QueueForInsert();
}

try
{
    conn.ProcessActionQueue();
    Console.WriteLine($"Inserted {items.Count} products.");
}
catch (Exception ex)
{
    conn.ClearActionQueue();
    Console.WriteLine($"Batch failed: {ex.Message}");
}
```

### Deleting by query via queue

```csharp
var template = new Product(conn);
var term = new EqualTerm(template, template.InStock, false);
template.QueueForDelete(term);   // DELETE FROM Products WHERE InStock = 0

conn.ProcessActionQueue();
```

---

## Read-for-update (advisory lock)

`ReadForUpdate` acquires a row-level update lock on SQL Server
(`SELECT ... WITH (UPDLOCK)`) so that no other session can update the row
between your read and your subsequent write:

```csharp
var product = new Product(conn);
product.ID.SetValue(42);

// Open a transaction first — the lock is held until it completes
var tx = conn.BeginTransaction();
conn.ReadForUpdate(product, null);   // SELECT ... WITH (UPDLOCK)

product.Price.SetValue(product.Price + 5m);
product.Update(DataObjectLock.UpdateOption.IgnoreLock);

conn.CommitTransaction(tx);          // lock released
```

---

## Summary

| Scenario | Recommended approach |
|----------|----------------------|
| Single entity save | `Insert()` / `Update()` / `Delete()` directly |
| Multiple entities atomically | `BeginTransaction` → operations → `CommitTransaction` |
| Bulk inserts / updates | `QueueForInsert` / `QueueForUpdate` → `ProcessActionQueue` |
| Preventing lost updates | `ReadForUpdate` inside a transaction |
| Optimistic concurrency | `Update(DataObjectLock.UpdateOption.ReleaseLock)` + catch `ObjectLockException` |
