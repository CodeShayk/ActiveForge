# Field Types

All database columns are represented by a `TField` subclass.
Every `TField` starts as **null** and tracks null state independently of the CLR value.

## Common API (all TField subtypes)

```csharp
field.IsNull()            // true if no value has been set
field.IsLoaded()          // true if loaded from DB (set by ORM after a Read/Query)
field.SetValue(object v)  // set a value (clears null)
field.SetNull(bool isNull)// force null state
field.SetLoaded(bool b)   // mark as loaded (used internally)
field.GetValue()          // returns boxed CLR value
field.GetRawValue()       // returns raw object (may differ from GetValue for mapped types)
field.CopyFrom(TField src)// copy value + null state from another field
field.IsValid()           // false if SetValidationFailure(true) was called
field.ConversionErrorOccurred() // true if SetValue received an unconvertible value
```

## Numeric Types

| Type | CLR Type | Notes |
|------|----------|-------|
| `TInt` | `int` | 32-bit signed integer; ++/-- operators |
| `TInt16` | `short` | 16-bit integer |
| `TInt64` / `TLong` | `long` | 64-bit integer |
| `TByte` | `byte` | 8-bit unsigned |
| `TSByte` | `sbyte` | 8-bit signed |
| `TUInt` | `uint` | 32-bit unsigned |
| `TUInt16` | `ushort` | 16-bit unsigned |
| `TUInt64` | `ulong` | 64-bit unsigned |
| `TFloat` | `float` | Single precision |
| `TDouble` | `double` | Double precision |
| `TDecimal` | `decimal` | Monetary / high-precision; comparison operators |

All numeric types support:
- Implicit conversion to/from their CLR type
- `==`, `!=`, `>`, `<`, `>=`, `<=` operators
- `GetHashCode()` returning 0 when null

## String Types

| Type | CLR Type | Notes |
|------|----------|-------|
| `TString` | `string` | Empty string ≠ null; `Length`, `Contains`, `ToUpper`, `ToLower`, `Trim`, `Clone` |
| `TChar` | `char` | Single character |
| `THtmlString` | `string` | Like TString; HTML-aware subclass |
| `TIpAddress` | `string` | IP address stored as string |

`TString` does **not** convert empty string to null. Use `SetNull(true)` explicitly.

```csharp
var s = new TString("hello");
string raw = s;         // implicit → "hello"
TString ts = "world";  // implicit ← "world"
bool eq = s == "hello"; // mixed equality
```

## Key Types

| Type | CLR Type | Notes |
|------|----------|-------|
| `TPrimaryKey` | `int` | Auto-generated PK; implicit from `int` / `long`; converts to `TForeignKey` |
| `TForeignKey` | `int` | References another table's PK; implicit from `int` / `long` |

```csharp
TPrimaryKey pk = 42;        // implicit from int
TForeignKey fk = pk;        // TPrimaryKey → TForeignKey (creates new instance)
bool equal = pk == (TForeignKey)pk; // safe cross-type comparison via explicit cast
```

> **Note:** The operators `pk == fk` and `pk != fk` may throw at runtime due to an unsafe
> internal cast. Always convert explicitly: `(TForeignKey)pk == fk`.

## Date/Time Types

| Type | CLR Type | Notes |
|------|----------|-------|
| `TDateTime` | `DateTime` | Local or unspecified datetime |
| `TUtcDateTime` | `DateTime` | Always UTC |
| `TLocalDateTime` | `DateTime` | Always local time |
| `TDate` | `DateTime` | Date only (time truncated) |
| `TUtcDate` | `DateTime` | UTC date only |
| `TLocalDate` | `DateTime` | Local date only |
| `TTime` | `TimeSpan` | Time of day |

## Boolean

| Type | CLR Type | Notes |
|------|----------|-------|
| `TBool` | `bool` | Stores true/false; implicit from `bool` |

## Binary / GUID

| Type | CLR Type | Notes |
|------|----------|-------|
| `TByteArray` | `byte[]` | Binary data |
| `TGuid` | `Guid` | UUID / uniqueidentifier |

## Factory

Create a `TField` instance dynamically:

```csharp
// By type
TField f = TField.Create(typeof(TString), null);

// By fully-qualified name
TField g = TField.Create("ActiveForge.TDecimal");
```

All factory-created instances start as null (`IsNull() == true`).
