# Advanced Topics

## Field Encryption

Mark a field with `[Encrypted]` to have values automatically encrypted before
INSERT/UPDATE and decrypted after SELECT.

```csharp
public class SecureData : IdentDataObject
{
    [Column("SSN")]
    [Encrypted(typeof(AesEncryption), EncryptionMethodType.AllDataEncrypted)]
    public TString SSN = new TString();
}
```

Implement `EncryptionAlgorithm`:

```csharp
public class AesEncryption : EncryptionAlgorithm
{
    public override int GetMaxFieldLength() => 256;

    public override object Encrypt(object plaintext)
    {
        // ... encrypt plaintext string, return Base64 string ...
    }

    public override object Decrypt(object ciphertext)
    {
        // ... decrypt Base64 string, return plaintext ...
    }

    public override EncryptionMethodType GetEncryptionMethodType()
        => EncryptionMethodType.AllDataEncrypted;
}
```

> `AllDataEncrypted` means the entire field value is encrypted (safe to query with `EqualTerm`
> if you encrypt the search value too).
> `PartialEncryption` means only part of the value is encrypted — this type cannot be used
> in query terms (the ORM will throw a `PersistenceException`).

## Custom Field Mappers

Implement `IDBFieldMapper` to handle non-standard CLR ↔ DB type conversions:

```csharp
public class JsonMapper : IDBFieldMapper
{
    private DataObject _container;

    public void SetContainingDataObject(DataObject obj) => _container = obj;

    public object ConvertToDBValue(object value)
        => value is MyStruct s ? System.Text.Json.JsonSerializer.Serialize(s) : null;

    public object ConvertFromDBValue(object value)
        => value is string json ? System.Text.Json.JsonSerializer.Deserialize<MyStruct>(json) : default;
}

// Usage on a field:
[Column("Metadata")]
[FieldMapping(typeof(JsonMapper))]
public TString Metadata = new TString();
```

## Polymorphic Type Mapping

When you have an abstract base entity and multiple concrete subtypes (table-per-hierarchy
or table-per-type), register the mapping in your `FactoryBase`:

```csharp
public class AppFactory : FactoryBase
{
    protected override void CreateTypeMap()
    {
        // "When the ORM needs to create a PaymentMethod, use CreditCardPayment instead"
        AddTypeMapping(typeof(PaymentMethod), typeof(CreditCardPayment));
    }
}
```

Pass `AppFactory` to the connection constructor.

## Lookup Objects

`LookupDataObject` is a base class for reference / code-table entities whose rows are
cached in memory after the first load.

```csharp
[Table("Statuses")]
public class Status : LookupDataObject
{
    [Column("Code")]  public TString Code  = new TString();
    [Column("Label")] public TString Label = new TString();

    public override void PrimeAndQueryCache(QueryTerm term, SortOrder sort, int version)
    {
        // Load all rows once into a static dictionary here
    }
}
```

When the ORM resolves a foreign-key relationship to a `LookupDataObject`, it reads from
the in-memory cache rather than issuing a JOIN, reducing round trips for high-read
reference tables.

## Action Queue

Accumulate operations and flush them in a single database round-trip:

```csharp
foreach (var item in itemsToInsert)
{
    var p = new Product(conn);
    p.Name.SetValue(item.Name);
    p.Price.SetValue(item.Price);
    p.QueueForInsert();           // deferred
}

conn.ProcessActionQueue();        // all inserts in one transaction
conn.ClearActionQueue();          // remove any leftover pending operations
```

Similarly: `QueueForUpdate()`, `QueueForDelete()`.

## Stored Procedures

```csharp
var result = conn.ExecStoredProcedure(
    template, "usp_GetProductsByCategory",
    0 /*start*/, 0 /*count (0=all)*/,
    new DataObject.SPInputParameter("@CategoryID", 2));
```

## Raw SQL

```csharp
// Returns an ObjectCollection populated from the query
var results = conn.ExecSQL(template,
    "SELECT * FROM Products WHERE CreatedAt > @since",
    new Dictionary<string, object> { { "@since", DateTime.Today.AddDays(-7) } });

// Returns a ReaderBase for low-level access
using var reader = conn.ExecSQL("SELECT COUNT(*) FROM Products");
reader.Read();
int count = (int)reader.GetValue(0);
```

## Object Templating

When `IsObjectTemplatingEnabled` is `true` on the connection, objects passed to `Create()`
are pre-populated from a cached template rather than being default-constructed.
Override in a custom connection subclass to enable:

```csharp
public override bool IsObjectTemplatingEnabled => true;
```

## Schema Discovery

The ORM inspects the database schema on first use of each table:

- Reads column names, types, nullability, precision, and scale from `INFORMATION_SCHEMA`
- Caches the schema per connection string
- Uses `sp_pkeys` to discover primary key columns

You can inspect the discovered metadata:

```csharp
List<TargetFieldInfo> columns = conn.GetTargetFieldInfo("Products");
foreach (var col in columns)
    Console.WriteLine($"{col.TargetName}: {col.TargetType.Name}, PK={col.IsInPK}");
```
