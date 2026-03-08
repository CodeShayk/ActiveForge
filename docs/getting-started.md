# Getting Started with Turquoise.ORM

## Installation

Turquoise.ORM consists of a core library and one or more provider packages. Reference the core plus the provider for your database:

**SQL Server**

```xml
<ItemGroup>
  <ProjectReference Include="..\Turquoise.ORM\Turquoise.ORM.csproj" />
  <ProjectReference Include="..\Turquoise.ORM.SqlServer\Turquoise.ORM.SqlServer.csproj" />
</ItemGroup>
```

**PostgreSQL**

```xml
<ItemGroup>
  <ProjectReference Include="..\Turquoise.ORM\Turquoise.ORM.csproj" />
  <ProjectReference Include="..\Turquoise.ORM.PostgreSQL\Turquoise.ORM.PostgreSQL.csproj" />
</ItemGroup>
```

If consuming published NuGet packages:

```xml
<!-- SQL Server -->
<PackageReference Include="Turquoise.ORM" Version="1.0.0" />
<PackageReference Include="Turquoise.ORM.SqlServer" Version="1.0.0" />

<!-- PostgreSQL -->
<PackageReference Include="Turquoise.ORM" Version="1.0.0" />
<PackageReference Include="Turquoise.ORM.PostgreSQL" Version="1.0.0" />
```

Both `SqlServerConnection` and `PostgreSQLConnection` live in the `Turquoise.ORM` namespace, so no extra `using` directive is required beyond `using Turquoise.ORM;`.

## Concepts

### DataObject — the Active Record base

Every database entity extends `DataObject` (or `IdentDataObject` for auto-increment PK tables).
Fields are declared as **public fields** of a `TField` subtype — not properties.

```csharp
[Table("Customers")]
public class Customer : IdentDataObject
{
    [Column("FirstName")] public TString  FirstName = new TString();
    [Column("LastName")]  public TString  LastName  = new TString();
    [Column("Email")]     public TString  Email     = new TString();
    [Column("Balance")]   public TDecimal Balance   = new TDecimal();
    [Column("Active")]    public TBool    Active    = new TBool();

    public Customer() { }
    public Customer(DataConnection conn) : base(conn) { }
}
```

The `[Table]` attribute names the SQL table.
The `[Column]` attribute names the SQL column.
`IdentDataObject` adds an `ID` field (`TPrimaryKey`) with `[Identity]` — auto-populated after INSERT.

### TField — null-aware typed wrapper

Every field starts as **null** (`IsNull() == true`). Setting a value clears the null state.

```csharp
var c = new Customer();
c.FirstName.IsNull();        // true  — never assigned
c.FirstName.SetValue("Alice");
c.FirstName.IsNull();        // false — has a value
string name = c.FirstName;   // implicit conversion to string → "Alice"
```

Fields support implicit conversion to and from their underlying CLR type:

```csharp
TString s = "hello";          // string → TString
string  t = new TString("x"); // TString → string
TInt    n = 42;               // int → TInt
int     m = new TInt(7);      // TInt → int
```

### DataConnection — the connection gateway

`SqlServerConnection` is the SQL Server implementation:

```csharp
using Turquoise.ORM;

var conn = new SqlServerConnection(connectionString, new FactoryBase());
conn.Connect();
// ... use the connection ...
conn.Disconnect();
```

Pass the connection to each entity at construction time, or set it via `Target` later.
Every CRUD/query call on the entity is delegated to the bound connection.

## CRUD Operations

### INSERT

```csharp
var c = new Customer(conn);
c.FirstName.SetValue("Alice");
c.LastName.SetValue("Smith");
c.Email.SetValue("alice@example.com");
c.Balance.SetValue(100m);
c.Active.SetValue(true);

bool ok = c.Insert();
// c.ID is now populated with the generated primary key
Console.WriteLine($"New customer ID: {(int)c.ID.GetValue()}");
```

### READ (by primary key)

```csharp
var c = new Customer(conn);
c.ID.SetValue(42);
bool found = c.Read();
if (found)
    Console.WriteLine($"Found: {c.FirstName} {c.LastName}");
```

### UPDATE

```csharp
c.Balance.SetValue(250m);
c.Update(DataObjectLock.UpdateOption.IgnoreLock);

// To update all fields unconditionally:
c.UpdateAll();

// To update only fields that changed since last read:
c.UpdateChanged();
```

`DataObjectLock.UpdateOption` values:
- `ReleaseLock` — release the optimistic lock after update
- `RetainLock` — keep the lock (for subsequent updates in same transaction)
- `IgnoreLock` — skip all locking (simplest, use for non-concurrent tables)

### DELETE

```csharp
// Delete by primary key (object must have ID set)
c.Delete();

// Delete by query (removes multiple rows)
var template = new Customer(conn);
var term = new EqualTerm(template, template.Active, false);
template.Delete(term);
```

## Querying

See [query-builder.md](query-builder.md) for the full query API.

```csharp
var template = new Customer(conn);

// All active customers
var activeFilter = new EqualTerm(template, template.Active, true);
var results = conn.QueryAll(template, activeFilter, null, 0, null);

// Count
int count = conn.QueryCount(template, activeFilter);

// First match
bool found = conn.QueryFirst(template, activeFilter, null, null);

// Page of 20, starting at record 40
var page = conn.QueryPage(template, activeFilter, null, 40, 20, null);
```

## Connecting the dots

A typical repository-style usage:

```csharp
public class CustomerRepository
{
    private readonly DataConnection _conn;

    public CustomerRepository(DataConnection conn) => _conn = conn;

    public Customer GetById(int id)
    {
        var c = new Customer(_conn);
        c.ID.SetValue(id);
        return c.Read() ? c : null;
    }

    public ObjectCollection GetActive()
    {
        var template = new Customer(_conn);
        var term = new EqualTerm(template, template.Active, true);
        return _conn.QueryAll(template, term, null, 0, null);
    }

    public void Save(Customer c)
    {
        if (c.ID.IsNull()) c.Insert();
        else               c.Update(DataObjectLock.UpdateOption.IgnoreLock);
    }

    public void Delete(Customer c) => c.Delete();
}
```
