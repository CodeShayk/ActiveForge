# Appendix — ActiveForge.Core: Provider Extension Reference

A complete type-level reference for authors implementing a new ActiveForge database provider.
A provider is a NuGet package (e.g. `ActiveForge.SqlServer`) that bridges the ORM engine to a specific database by implementing three adapter types, one connection class, one unit-of-work class, and one DI extension method.

---

## Table of Contents

1. [Overview — What a Provider Must Supply](#1-overview--what-a-provider-must-supply)
2. [Adapter Layer](#2-adapter-layer)
   - 2.1 [ConnectionBase](#21-connectionbase)
   - 2.2 [TransactionBase](#22-transactionbase)
   - 2.3 [CommandBase](#23-commandbase)
   - 2.4 [ReaderBase](#24-readerbase)
3. [Connection Class](#3-connection-class)
   - 3.1 [DBDataConnection (SQL providers)](#31-dbdataconnection--sql-providers)
   - 3.2 [DataConnection (non-SQL / document providers)](#32-dataconnection--non-sql--document-providers)
4. [Unit of Work](#4-unit-of-work)
   - 4.1 [IUnitOfWork](#41-iunitofwork)
   - 4.2 [UnitOfWorkBase](#42-unitofworkbase)
5. [DI Registration](#5-di-registration)
6. [Core Supporting Types](#6-core-supporting-types)
   - 6.1 [TField Hierarchy](#61-tfield-hierarchy)
   - 6.2 [TargetFieldInfo](#62-targetfieldinfo)
   - 6.3 [JoinSpecification / JoinOverride](#63-joinspecification--joinoverride)
   - 6.4 [FieldSubset](#64-fieldsubset)
   - 6.5 [PersistenceException](#65-persistenceexception)
7. [Dialect Contracts](#7-dialect-contracts)
8. [Reference Implementation Checklist](#8-reference-implementation-checklist)

---

## 1. Overview — What a Provider Must Supply

A provider consists of exactly these pieces:

| Piece | Base type | Required |
|---|---|---|
| Connection adapter | `ConnectionBase` | Yes |
| Transaction adapter | `TransactionBase` | Yes |
| Command adapter | `CommandBase` | Yes |
| Reader adapter | `ReaderBase` | Yes |
| ORM connection | `DBDataConnection` (SQL) or `DataConnection` (document) | Yes |
| Unit of work | `UnitOfWorkBase` | Yes |
| DI extension | `IServiceCollection` extension returning `IActiveForgeBuilder` | Yes |

The four adapter types form an abstraction layer over the native ADO.NET (or driver) objects. The ORM engine only ever calls the abstract adapter API — it never references the native types directly.

### Assembly layout

```
ActiveForge.MyProvider/
├── Adapters/
│   ├── MyAdapterConnection.cs    ← ConnectionBase subclass
│   ├── MyAdapterTransaction.cs   ← TransactionBase subclass
│   ├── MyAdapterCommand.cs       ← CommandBase subclass
│   └── MyAdapterReader.cs        ← ReaderBase subclass
├── Transactions/
│   └── MyUnitOfWork.cs           ← UnitOfWorkBase subclass
├── Extensions/
│   └── MyServiceCollectionExtensions.cs
└── MyConnection.cs               ← DBDataConnection (or DataConnection) subclass
```

---

## 2. Adapter Layer

### 2.1 `ConnectionBase`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/Adapters/ConnectionBase.cs`

Abstract wrapper around the native database connection object.

```csharp
public abstract class ConnectionBase
{
    // ── Lifecycle ────────────────────────────────────────────────────────────────

    /// <summary>Opens the underlying physical connection.</summary>
    public abstract void Open();

    /// <summary>Closes the connection and returns it to the pool (if pooling is enabled).</summary>
    public abstract void Close();

    // ── Commands ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new provider-specific CommandBase for the given SQL text.
    /// The command inherits the timeout from GetTimeout().
    /// </summary>
    public abstract CommandBase CreateCommand(string sql);

    // ── Transactions ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new transaction at the given isolation level and returns a
    /// TransactionBase wrapping the native transaction object.
    /// </summary>
    public abstract TransactionBase BeginTransaction(IsolationLevel level);

    /// <summary>
    /// Inspects the state of the supplied TransactionBase.
    /// Implementations query the database (e.g. SELECT XACT_STATE() for SQL Server,
    /// txid_current_if_assigned() for PostgreSQL) or inspect driver state.
    /// Return NoTransaction when the argument is not a recognised provider type.
    /// </summary>
    public abstract TransactionStates TransactionState(TransactionBase transaction);

    // ── State ────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when the native connection is in the Open state.</summary>
    public abstract bool IsConnected();

    /// <summary>Returns the name of the currently selected database/catalogue.</summary>
    public abstract string DatabaseName();

    // ── Timeout (concrete — no override required) ────────────────────────────────

    public int  GetTimeout()            // returns the current command timeout in seconds
    public void SetTimeout(int seconds) // sets the command timeout in seconds (0 = no timeout)
}
```

#### `TransactionStates` enum (nested in `ConnectionBase`)

| Value | Meaning |
|---|---|
| `NoTransaction` | No active transaction on this connection. |
| `CommittableTransaction` | Transaction is active and in a healthy state; Commit will succeed. |
| `NonCommittableTransaction` | Transaction is active but doomed (e.g. a prior statement failed on PostgreSQL/SQL Server). Only Rollback is valid. |

#### Implementation pattern

```csharp
public class MyAdapterConnection : ConnectionBase
{
    private readonly MyNativeConnection _conn;

    public MyAdapterConnection(string connectionString)
        => _conn = new MyNativeConnection(connectionString);

    public override void Open()  => _conn.Open();
    public override void Close() => _conn.Close();
    public override bool IsConnected() => _conn.State == ConnectionState.Open;
    public override string DatabaseName() => _conn.Database;

    public override TransactionBase BeginTransaction(IsolationLevel level)
        => new MyAdapterTransaction(_conn.BeginTransaction(level));

    public override CommandBase CreateCommand(string sql)
        => new MyAdapterCommand(sql, this);

    // Expose native connection for MyAdapterCommand to bind to:
    public MyNativeConnection GetNativeConnection() => _conn;

    public override TransactionStates TransactionState(TransactionBase tx)
    {
        if (tx is MyAdapterTransaction mat)
        {
            // Query db state — e.g. SELECT XACT_STATE() for SQL Server
            // or return CommittableTransaction when tx object is non-null for SQLite
            return TransactionStates.CommittableTransaction;
        }
        return TransactionStates.NoTransaction;
    }
}
```

---

### 2.2 `TransactionBase`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/Adapters/TransactionBase.cs`

Abstract wrapper around the native transaction object. Instances are created by
`ConnectionBase.BeginTransaction` and associated with commands via `CommandBase.SetTransaction`.

```csharp
public abstract class TransactionBase : IDisposable
{
    /// <summary>
    /// Commits all changes. After a successful commit the object must not be reused.
    /// </summary>
    public abstract void Commit();

    /// <summary>
    /// Rolls back all changes. After rollback the object must not be reused.
    /// </summary>
    public abstract void Rollback();

    /// <summary>
    /// Releases native resources. If neither Commit nor Rollback has been called,
    /// most providers implicitly roll back on Dispose.
    /// </summary>
    public abstract void Dispose();
}
```

#### Implementation pattern

```csharp
public class MyAdapterTransaction : TransactionBase
{
    private readonly MyNativeTransaction _tx;

    public MyAdapterTransaction(MyNativeTransaction tx) => _tx = tx;

    // Expose the native tx so MyAdapterCommand can attach it:
    public MyNativeTransaction GetNativeTransaction() => _tx;

    public override void Commit()   => _tx.Commit();
    public override void Rollback() => _tx.Rollback();
    public override void Dispose()  => _tx.Dispose();
}
```

---

### 2.3 `CommandBase`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/Adapters/CommandBase.cs`

Abstract wrapper around the native command object. The ORM engine calls `AddParameter` to bind
values and then `ExecuteNonQuery`, `ExecuteReader`, `ExecuteSequentialReader`, or `ExecuteScalar`
to run the statement.

```csharp
public abstract class CommandBase : IDisposable
{
    // ── Execution ────────────────────────────────────────────────────────────────

    /// <summary>Executes a non-row-returning statement. Returns rows-affected count.</summary>
    public abstract int ExecuteNonQuery();

    /// <summary>Executes and returns a ReaderBase in default (random-access) mode.</summary>
    public abstract ReaderBase ExecuteReader();

    /// <summary>Executes and returns a ReaderBase in sequential-access mode (lower memory).</summary>
    public abstract ReaderBase ExecuteSequentialReader();

    /// <summary>Executes and returns the value of the first column of the first row, or null.</summary>
    public abstract object ExecuteScalar();

    // ── Parameters ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Provider-specific: adds a named parameter to the underlying native command.
    /// IMPORTANT: If the provider rejects non-primitive types (e.g. Microsoft.Data.Sqlite),
    /// unwrap any TField wrapper before passing to the native API:
    ///
    ///   if (value is TField tf) value = tf.GetValue() ?? DBNull.Value;
    ///
    /// The base AddParameter(name, value, info) method already converts null → DBNull.Value
    /// before calling AddNativeParameter, so you only need to handle TField unwrapping.
    /// </summary>
    protected abstract void AddNativeParameter(string name, object value, TargetFieldInfo info);

    // ── Stored procedures ─────────────────────────────────────────────────────────

    /// <summary>
    /// Switches the command to stored-procedure mode.
    /// Providers that do not support stored procedures should throw NotSupportedException.
    /// </summary>
    public abstract void SetToStoredProcedure();

    // ── Other ────────────────────────────────────────────────────────────────────

    /// <summary>Attempts to cancel the in-flight command. May be a no-op for some providers.</summary>
    public abstract void Cancel();

    /// <summary>Releases the underlying native command.</summary>
    public abstract void Dispose();

    // ── Concrete helpers (no override needed) ────────────────────────────────────

    /// <summary>Associates a transaction with this command.</summary>
    public void SetTransaction(TransactionBase tx);

    /// <summary>Adds a parameter with name + value only (no field metadata).</summary>
    public virtual void AddParameter(string name, object value);

    /// <summary>Adds a parameter with field metadata for type/size resolution.</summary>
    public virtual void AddParameter(string name, object value, TargetFieldInfo info);

    /// <summary>Maps a CLR type to DbType. Override to extend the default mapping.</summary>
    protected virtual DbType MapDbType(Type clrType);
}
```

#### `Parameter` inner class (in `CommandBase`)

```csharp
public class Parameter
{
    public string Name   { get; set; }   // e.g. "@ProductID"
    public object Value  { get; set; }   // DBNull.Value when logically null
    public DbType DbType { get; set; }   // inferred from CLR type
    public int    Size   { get; set; }   // max length for string/binary types
}
```

#### Implementation pattern

```csharp
public class MyAdapterCommand : CommandBase
{
    private readonly MyNativeCommand _cmd;

    public MyAdapterCommand(string sql, MyAdapterConnection conn)
        : base(sql, conn)
    {
        _cmd = new MyNativeCommand(sql, conn.GetNativeConnection());
        _cmd.CommandTimeout = conn.GetTimeout();
    }

    public override int        ExecuteNonQuery()         => _cmd.ExecuteNonQuery();
    public override object     ExecuteScalar()           => _cmd.ExecuteScalar();
    public override ReaderBase ExecuteReader()           => new MyAdapterReader(_cmd.ExecuteReader());
    public override ReaderBase ExecuteSequentialReader() => new MyAdapterReader(
        _cmd.ExecuteReader(CommandBehavior.SequentialAccess));

    public override void SetToStoredProcedure()
        => _cmd.CommandType = CommandType.StoredProcedure;

    public override void Cancel()  => _cmd.Cancel();
    public override void Dispose() => _cmd.Dispose();

    protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
    {
        // Unwrap TField wrappers — required by strict drivers (e.g. Microsoft.Data.Sqlite)
        if (value is TField tf) value = tf.GetValue() ?? DBNull.Value;

        var p = _cmd.Parameters.Add(new MyNativeParameter(name, value));

        // Optionally constrain size for string parameters:
        if (info?.MaxLength > 0 && value is string)
            p.Size = info.MaxLength;

        // Attach the current transaction if one is active:
        if (Transaction is MyAdapterTransaction mat)
            _cmd.Transaction = mat.GetNativeTransaction();
    }
}
```

---

### 2.4 `ReaderBase`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/Adapters/ReaderBase.cs`

Abstract wrapper around a forward-only data reader. The ORM engine calls `Read()` in a loop,
then retrieves column values via `ColumnValue(int)` or `ColumnValue(string)`.

```csharp
public abstract class ReaderBase : IDisposable
{
    // ── Row navigation ────────────────────────────────────────────────────────────

    /// <summary>Advances to the next row. Returns false when all rows have been read.</summary>
    public abstract bool Read();

    /// <summary>Closes the reader and frees server-side cursor resources.</summary>
    public abstract void Close();

    /// <summary>Releases all resources, including closing the underlying native reader.</summary>
    public abstract void Dispose();

    // ── Column access ─────────────────────────────────────────────────────────────

    /// <summary>Returns the raw value at the specified zero-based ordinal.</summary>
    public abstract object GetValue(int ordinal);

    /// <summary>Returns true when the value at the given ordinal is a database NULL.</summary>
    public abstract bool IsDBNull(int ordinal);

    /// <summary>Returns the zero-based ordinal for the named column.</summary>
    public abstract int GetOrdinal(string columnName);

    /// <summary>Gets the number of columns in the current result set.</summary>
    public abstract int FieldCount { get; }

    /// <summary>Returns the column name at the given zero-based ordinal.</summary>
    public abstract string GetName(int ordinal);

    /// <summary>
    /// Exposes the underlying IDataRecord for callers that need typed accessors
    /// (GetInt32, GetString, etc.) directly on the native reader.
    /// </summary>
    public abstract IDataRecord Record { get; }

    // ── Convenience helpers (concrete — no override needed) ───────────────────────

    public object ColumnValue(int ordinal)      // null-safe GetValue by ordinal
    public object ColumnValue(string name)      // null-safe GetValue by column name
    public int    ColumnOrdinal(string name)    // alias for GetOrdinal
    public int    ColumnCount()                 // alias for FieldCount
    public string ColumnName(int ordinal)       // alias for GetName
}
```

#### Implementation pattern

```csharp
public class MyAdapterReader : ReaderBase
{
    private readonly MyNativeReader _reader;

    public MyAdapterReader(MyNativeReader reader) => _reader = reader;

    public override bool   Read()                    => _reader.Read();
    public override void   Close()                   => _reader.Close();
    public override void   Dispose()                 => _reader.Dispose();
    public override object GetValue(int ordinal)     => _reader.GetValue(ordinal);
    public override bool   IsDBNull(int ordinal)     => _reader.IsDBNull(ordinal);
    public override int    GetOrdinal(string name)   => _reader.GetOrdinal(name);
    public override int    FieldCount                => _reader.FieldCount;
    public override string GetName(int ordinal)      => _reader.GetName(ordinal);
    public override IDataRecord Record               => _reader;
}
```

---

## 3. Connection Class

### 3.1 `DBDataConnection` — SQL providers

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/DBDataConnection.cs`

All relational (SQL) providers extend `DBDataConnection`, which handles SQL generation,
parameterised binding, result hydration, JOIN construction, caching, and transaction depth.
The provider subclass only needs to supply dialect-specific overrides.

```csharp
public abstract class DBDataConnection : DataConnection, IDisposable
{
    // ── MUST override ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and returns a new ConnectionBase wrapping the native connection for
    /// the given connection string. Called once per Connect() call.
    /// </summary>
    protected abstract ConnectionBase CreateConnection(string connectString);

    /// <summary>
    /// Returns SQL that limits the result set to <paramref name="count"/> rows.
    /// SQL Server: "SELECT TOP {count} {fieldsAndFrom}"
    /// PostgreSQL/SQLite: return fieldsAndFrom unchanged (suffix added via GetPageSuffix)
    /// </summary>
    public abstract string LimitRowCount(int count, string fieldsAndFrom);

    // ── MUST override (from DataConnection) ──────────────────────────────────────

    public abstract string GetParameterMark();            // SQL Server: "@",  PG: "@",  SQLite: "@"
    public abstract string GetLeftNameQuote();            // SQL Server: "[",  PG: "\"", SQLite: "\""
    public abstract string GetRightNameQuote();           // SQL Server: "]",  PG: "\"", SQLite: "\""
    public abstract string GetSourceNameSeparator();      // SQL Server: ".",  PG: ".",  SQLite: "."
    public abstract string GetUpdateLock();               // SQL Server: "WITH (UPDLOCK)", PG: "FOR UPDATE", SQLite: ""
    public abstract bool   IsAutoIdentity();              // true for IDENTITY/SERIAL columns
    public abstract string GetStringConnectionOperator(); // SQL Server: "+",  PG: "||", SQLite: "||"
    public abstract string GetGeneratorOperator(TargetFieldInfo info); // sequence/generator SQL or ""
    public abstract string CreateConcatenateOperator(params string[] parts);

    // ── SHOULD override (identity retrieval) ─────────────────────────────────────

    /// <summary>
    /// Called after the first INSERT in a multi-table hierarchy to retrieve and
    /// assign the database-generated identity value to the entity's identity field.
    /// Default returns "" (no identity retrieval).
    ///
    /// SQL Server: SELECT @@IDENTITY  (not SCOPE_IDENTITY — see note in MEMORY.md)
    /// PostgreSQL: RETURNING id  (implemented via RETURNING clause in INSERT SQL)
    /// SQLite:     SELECT last_insert_rowid()
    /// MongoDB:    n/a (uses auto-increment counter collection)
    /// </summary>
    protected virtual string PopulateIdentity(Record obj, RecordBinding binding, CommandBase command);

    /// <summary>
    /// When true, identity field values are included in INSERT statements.
    /// Default: false. Override to return true when the provider requires explicit
    /// identity insertion (e.g. when SET IDENTITY_INSERT ON is used).
    /// </summary>
    protected virtual bool InsertIdentityFields => false;

    // ── SHOULD override (pagination) ─────────────────────────────────────────────

    /// <summary>
    /// Returns the SQL suffix that limits pagination for providers that use a
    /// LIMIT/OFFSET clause rather than a SELECT TOP prefix.
    ///
    /// PostgreSQL / SQLite: "LIMIT {count} OFFSET {start}"
    /// SQL Server: "" (uses LimitRowCount prefix instead)
    ///
    /// When this method returns a non-empty string, QueryNode appends it to the
    /// SELECT and passes firstSignificant=0 to PerformFetch.
    /// </summary>
    protected virtual string GetPageSuffix(int start, int count) => "";

    // ── MAY override (identity insert commands) ───────────────────────────────────

    /// <summary>SQL executed before an INSERT when identity insertion is enabled.</summary>
    public abstract string PreInsertIdentityCommand(string sourceName);

    /// <summary>SQL executed after an INSERT when identity insertion is enabled.</summary>
    public abstract string PostInsertIdentityCommand(string sourceName);

    // ── MAY override (schema introspection) ──────────────────────────────────────

    /// <summary>
    /// Populates the schema cache for the given source (table/view name) by
    /// querying the database's information schema or system catalog.
    /// Called the first time a binding is built for a new table.
    /// Override to query the specific system catalog for the target database.
    ///
    /// SQL Server: queries SYSOBJECTS / SYSCOLUMNS / SYSTYPES
    /// PostgreSQL: queries information_schema.columns
    /// SQLite:     executes PRAGMA table_info(tableName)
    /// </summary>
    protected virtual void PopulateSQLFieldCache(string sourceName) { }

    // ── MAY override (name resolution) ───────────────────────────────────────────

    /// <summary>
    /// Resolves a source name (table/view name from [Table] attribute) to a
    /// fully-qualified name. Default: QuoteName(sourceName).
    /// Override for providers that require schema qualification (e.g. "dbo"."Products").
    /// </summary>
    public virtual string ResolveFullyQualifiedName(string sourceName, bool isFunction);

    // ── Cache management (concrete — no override needed) ─────────────────────────

    public virtual void FlushSchema();    // clears the schema field cache
    public virtual void FlushBindings();  // clears the binding cache only

    // ── UoW sync hooks (override to fix transaction depth counters) ───────────────

    /// <summary>
    /// Called by RunWrite after a UnitOfWork-managed transaction is committed.
    /// Override to decrement internal _transactionDepth counters.
    /// Required for providers (like SQLite via Microsoft.Data.Sqlite) that fail
    /// if a committed transaction object is reused.
    /// </summary>
    protected override void OnUoWCommitted();
    protected override void OnUoWRolledBack();
}
```

#### Constructors

```csharp
// All DBDataConnection subclasses call one of these base constructors:
protected DBDataConnection(string connectString)
protected DBDataConnection(string connectString, FactoryBase factory)
```

#### Minimum viable SQL provider

```csharp
public class MyConnection : DBDataConnection
{
    public MyConnection(string connectString) : base(connectString) { }
    public MyConnection(string connectString, FactoryBase factory) : base(connectString, factory) { }

    // Adapter factory
    protected override ConnectionBase CreateConnection(string connectString)
        => new MyAdapterConnection(connectString);

    // Dialect
    public override string GetParameterMark()            => "@";
    public override string GetLeftNameQuote()            => "\"";
    public override string GetRightNameQuote()           => "\"";
    public override string GetSourceNameSeparator()      => ".";
    public override string GetUpdateLock()               => "FOR UPDATE";
    public override bool   IsAutoIdentity()              => true;
    public override string GetStringConnectionOperator() => "||";
    public override string GetGeneratorOperator(TargetFieldInfo info) => "";
    public override string CreateConcatenateOperator(params string[] parts)
        => string.Join("||", parts);

    // Row limiting (LIMIT suffix approach)
    public override string LimitRowCount(int count, string fieldsAndFrom) => fieldsAndFrom;
    protected override string GetPageSuffix(int start, int count)
        => $"LIMIT {count} OFFSET {start}";

    // Identity retrieval
    protected override string PopulateIdentity(Record obj, RecordBinding binding, CommandBase _)
    {
        using var cmd = CreateCommand("SELECT last_insert_rowid()");
        object raw = cmd.ExecuteScalar();
        // assign raw to the identity TField on obj via binding.UpdateFields
        return "";
    }

    // Identity insert wrappers (if not supported, return empty strings)
    public override string PreInsertIdentityCommand(string sourceName)  => "";
    public override string PostInsertIdentityCommand(string sourceName) => "";
}
```

---

### 3.2 `DataConnection` — non-SQL / document providers

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/DataConnection.cs`

Document-oriented providers (e.g. MongoDB) extend `DataConnection` directly and implement
the full CRUD and query surface area without using `DBDataConnection`'s SQL generation.

```csharp
public abstract class DataConnection
{
    // ── Lifecycle ─────────────────────────────────────────────────────────────────
    public abstract bool Connect();
    public abstract bool Disconnect();
    public virtual  bool IsOpen => false;

    // ── UoW integration (concrete — used as-is) ───────────────────────────────────
    public IUnitOfWork UnitOfWork { get; set; }
    protected virtual T RunWrite<T>(Func<T> operation);
    protected virtual void OnUoWCommitted();
    protected virtual void OnUoWRolledBack();

    // ── CRUD (must implement all) ─────────────────────────────────────────────────
    public abstract bool Insert(Record obj);
    public abstract bool Delete(Record obj);
    public abstract bool Delete(Record obj, QueryTerm term);
    // + internal Update overloads

    // ── Reads (must implement all) ────────────────────────────────────────────────
    public abstract bool Read(Record obj);
    public abstract bool QueryFirst(Record obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset);
    public abstract int  QueryCount(Record obj, QueryTerm term);

    // ── QueryAll / QueryPage / LazyQueryAll (must implement all overloads) ────────
    public abstract RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder,
        int pageSize, FieldSubset fieldSubset);
    public abstract RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder,
        int start, int count, FieldSubset fieldSubset);
    public abstract IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder,
        int pageSize, FieldSubset fieldSubset) where T : Record;

    // ── JoinOverride overloads (virtual — default ignores overrides) ──────────────
    public virtual RecordCollection QueryAll(..., IReadOnlyList<JoinOverride> joinOverrides);
    public virtual IEnumerable<T>   LazyQueryAll<T>(..., IReadOnlyList<JoinOverride> joinOverrides);
    public virtual RecordCollection QueryPage(..., IReadOnlyList<JoinOverride> joinOverrides);

    // ── Raw SQL (throw NotSupportedException for non-SQL providers) ───────────────
    public abstract RecordCollection ExecSQL(Record obj, string sql);
    public abstract ReaderBase       ExecSQL(string sql);

    // ── Transactions ──────────────────────────────────────────────────────────────
    public abstract TransactionBase   BeginTransaction();
    public abstract TransactionBase   BeginTransaction(IsolationLevel level);
    public abstract void              CommitTransaction(TransactionBase tx);
    public abstract void              RollbackTransaction(TransactionBase tx);
    public abstract TransactionStates TransactionState(TransactionBase tx);

    // ── Dialect helpers ───────────────────────────────────────────────────────────
    public abstract string GetParameterMark();
    public abstract string GetLeftNameQuote();
    public abstract string GetRightNameQuote();
    // ...etc
}
```

---

## 4. Unit of Work

### 4.1 `IUnitOfWork`

**Namespace:** `ActiveForge.Transactions`
**File:** `src/ActiveForge/Transactions/IUnitOfWork.cs`

The public contract consumed by the ORM engine, `With.Transaction`, and the interceptors.

```csharp
public interface IUnitOfWork : IDisposable
{
    /// <summary>True when a transaction is currently active (depth > 0).</summary>
    bool InTransaction { get; }

    /// <summary>
    /// Begins (or re-enters) a transaction. Depth-tracked: only the outermost
    /// call opens an ADO.NET transaction; inner calls increment depth only.
    /// Returns the active TransactionBase (shared for all depth levels).
    /// </summary>
    TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);

    /// <summary>
    /// Decrements depth. When depth reaches 0, commits the ADO.NET transaction
    /// (unless the rollback-only flag is set, in which case it rolls back instead).
    /// </summary>
    void Commit();

    /// <summary>
    /// Sets the rollback-only flag and decrements depth. When depth reaches 0,
    /// rolls back the ADO.NET transaction.
    /// </summary>
    void Rollback();
}
```

---

### 4.2 `UnitOfWorkBase`

**Namespace:** `ActiveForge.Transactions`
**File:** `src/ActiveForge/Transactions/UnitOfWorkBase.cs`

Concrete base that implements all depth-tracking and rollback-only logic.
Provider implementations only need to supply `BeginTransactionCore`.

```csharp
public abstract class UnitOfWorkBase : IUnitOfWork
{
    protected UnitOfWorkBase(ILogger logger = null);

    // ── IUnitOfWork (fully implemented — no override needed) ──────────────────────
    public bool InTransaction { get; }
    public TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    public void Commit();
    public void Rollback();
    public void Dispose();

    // ── MUST override ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a new ADO.NET transaction on the underlying connection at the given
    /// isolation level. Called only when the depth transitions from 0 → 1.
    /// Return the TransactionBase wrapping the new native transaction.
    /// </summary>
    protected abstract TransactionBase BeginTransactionCore(IsolationLevel level);

    // ── MAY override ──────────────────────────────────────────────────────────────

    /// <summary>Called by Dispose() after transaction cleanup. Override to release resources.</summary>
    protected virtual void DisposeCore() { }
}
```

#### Implementation pattern

```csharp
public sealed class MyUnitOfWork : UnitOfWorkBase
{
    private readonly MyConnection _connection;

    public MyUnitOfWork(MyConnection connection, ILogger<MyUnitOfWork> logger = null)
        : base(logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public MyConnection Connection => _connection;

    protected override TransactionBase BeginTransactionCore(IsolationLevel level)
        => _connection.BeginTransaction(level);
}
```

---

## 5. DI Registration

Every provider package exposes a single `IServiceCollection` extension method that:
1. Registers the provider-specific connection as **scoped**
2. Registers `DataConnection` as scoped, forwarding to the concrete connection
3. Registers `IUnitOfWork` as scoped
4. Returns `new ActiveForgeBuilder(services)` so the caller can chain `.AddServices(...)`

```csharp
// Pattern — copy and adapt for your provider
public static class MyServiceCollectionExtensions
{
    /// <summary>
    /// Registers ActiveForge ORM with the MyDatabase provider.
    /// Returns an IActiveForgeBuilder for chaining .AddServices() calls.
    ///
    /// Registrations:
    ///   Scoped MyConnection     — one instance per DI scope (request)
    ///   Scoped DataConnection   — resolves to the same MyConnection instance
    ///   Scoped IUnitOfWork      — MyUnitOfWork wrapping the scoped MyConnection
    /// </summary>
    public static IActiveForgeBuilder AddActiveForgeMyDb(
        this IServiceCollection services,
        string connectionString,
        FactoryBase factory = null)
    {
        if (services         == null) throw new ArgumentNullException(nameof(services));
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

        services.AddScoped<MyConnection>(_ =>
            factory != null
                ? new MyConnection(connectionString, factory)
                : new MyConnection(connectionString));

        services.AddScoped<DataConnection>(sp => sp.GetRequiredService<MyConnection>());

        services.AddScoped<IUnitOfWork>(sp =>
        {
            var conn   = sp.GetRequiredService<MyConnection>();
            var logger = sp.GetService<ILogger<MyUnitOfWork>>();
            return new MyUnitOfWork(conn, logger);
        });

        return new ActiveForgeBuilder(services);
    }
}
```

### Method naming convention

| Provider | Method name |
|---|---|
| SQL Server | `AddActiveForgeSqlServer` |
| PostgreSQL | `AddActiveForgePostgreSQL` |
| MongoDB | `AddActiveForgeMongoDB` |
| SQLite | `AddActiveForgeSQLite` |
| Your provider | `AddActiveForge{Name}` |

### Using the builder

```csharp
builder.Services
    .AddActiveForgeMyDb("Data Source=mydb.db")
    .AddServices(typeof(Program).Assembly);   // auto-scans for IService implementations
```

---

## 6. Core Supporting Types

### 6.1 `TField` Hierarchy

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/Fields/TField.cs` (and subclass files)

`TField` is the abstract base for all typed column fields. Every concrete subclass tracks:
- **null state** — `IsNull()` / `SetNull(bool)`
- **load state** — `IsLoaded()` / `SetLoaded(bool)`
- **value** — `GetValue()` / `SetValue(object)` / implicit cast operators on each subclass

Provider code that reads values from a `TargetFieldInfo` should call `targetFieldInfo.SetValue(obj, dbValue)`, which handles null, mapper, and encryption automatically.

#### Complete `TField` type catalogue

| Type | CLR equivalent | Notes |
|---|---|---|
| `TPrimaryKey` | `int` | Auto-identity primary key |
| `TForeignKey` | `int` | Foreign key (int) |
| `TString` | `string` | `.Value` is **protected** — use `.SetValue()` / `.GetValue()` / implicit cast |
| `THtmlString` | `string` | HTML-safe string; subclass of `TString` |
| `TInt` | `int` | 32-bit signed integer |
| `TInt16` | `short` | 16-bit signed integer |
| `TInt64` | `long` | 64-bit signed integer |
| `TLong` | `long` | Alias for `TInt64` |
| `TByte` | `byte` | 8-bit unsigned integer |
| `TSByte` | `sbyte` | 8-bit signed integer |
| `TUInt` | `uint` | 32-bit unsigned integer |
| `TUInt16` | `ushort` | 16-bit unsigned integer |
| `TUInt64` | `ulong` | 64-bit unsigned integer |
| `TDecimal` | `decimal` | Fixed-point decimal |
| `TDouble` | `double` | 64-bit float |
| `TFloat` | `float` | 32-bit float |
| `TBool` | `bool` | Boolean |
| `TGuid` | `Guid` | Stored as string in most providers |
| `TChar` | `char` | Single character |
| `TByteArray` | `byte[]` | Binary blob |
| `TIpAddress` | `string` | IP address string |
| `TDateTime` | `DateTime` | Local date+time |
| `TDate` | `DateTime` | Date only (time component ignored) |
| `TTime` | `TimeSpan` | Time only |
| `TLocalDate` | `DateTime` | Provider-local date |
| `TLocalDateTime` | `DateTime` | Provider-local date+time |
| `TUtcDate` | `DateTime` | UTC date only |
| `TUtcDateTime` | `DateTime` | UTC date+time |
| `TKey` | `int` | Generic key field |

#### Key `TField` methods used by providers

```csharp
// Read a value from a field (provider → DB):
object GetValue()                   // returns the raw CLR value, or default when null
bool   IsNull()                     // true when no value has been assigned

// Write a DB value into a field (DB → provider):
void   SetValue(object value)       // handles null, DBNull, empty-string → null conversion
void   SetLoaded(bool loaded)       // marks the field as populated from DB
void   SetNull(bool isNull)         // explicitly sets the null state

// Factory methods (used when creating new field instances):
static TField Create(Type type, Record container)
static TField Create(string typeName)

// Get underlying CLR type:
Type   GetUnderlyingType()          // e.g. typeof(int) for TInt
string GetTypeDescription()         // e.g. "int", "string", "bool"
```

> **Warning:** `TString.Value` is **protected**. External code (including providers) must use
> `.SetValue(string)`, `.GetValue()`, or the implicit `TString → string` cast. Never access
> `.Value` directly.

---

### 6.2 `TargetFieldInfo`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/TargetFieldInfo.cs`

Metadata about a single mapped field. Populated by `DBDataConnection.PopulateSQLFieldCache`
(or an equivalent mechanism) during binding construction, then cached for reuse.

```csharp
public class TargetFieldInfo
{
    public FieldInfo FieldInfo;         // Reflection handle to the TField field on the Record
    public string    FieldName;         // C# field name (e.g. "Name")
    public string    TargetName;        // DB column name (e.g. "ProductName")
    public string    SourceName;        // DB table alias / source name (e.g. "Products")
    public Type      TargetType;        // CLR type wrapped by the TField (e.g. typeof(string))

    public string    NativeTargetType;  // Optional provider-specific type hint (e.g. "varbinary")

    public bool      IsIdentity;        // True for auto-generated primary key columns
    public bool      IsInPK;            // True for all primary key columns
    public bool      IsIndexed;
    public bool      IsNullable;        // Default: true
    public bool      IsLarge;
    public bool      IsReadOnly;
    public bool      IsAutoGenerated;
    public string    GeneratorName;     // Name of the sequence/generator, if any
    public int       Index;             // Column ordinal as reported by the DB catalog
    public int       Length;            // Raw length from the catalog
    public int       MaxLength;         // Effective max length (accounts for encryption)
    public int       Scale;
    public int       Precision;

    public EncryptionAlgorithm  Encryption;   // Encryption algorithm, or null
    public IDBFieldMapper       FieldMapper;  // Custom value mapper, or null

    // Convenience methods — handle mapper + encryption automatically:
    public object GetValue(object obj);          // read from the TField, apply mapper/encryption
    public void   SetValue(object obj, object dbValue); // write to the TField, apply decryption/mapper
}
```

Provider implementations of schema introspection (`PopulateSQLFieldCache`) should populate a
`TargetFieldInfo` instance per column and add it to the connection's schema cache by calling
`AddTargetFieldInfoToCache(sourceName, targetFieldName, info)`.

---

### 6.3 `JoinSpecification` / `JoinOverride`

**Namespace:** `ActiveForge`
**Files:** `src/ActiveForge/JoinSpecification.cs`, `src/ActiveForge/JoinOverride.cs`

#### `JoinSpecification`

Describes a single SQL JOIN resolved by the binding layer.

```csharp
public class JoinSpecification
{
    public enum JoinTypeEnum { InnerJoin = 1, LeftOuterJoin = 2, RightOuterJoin = 3 }

    public string        SourceAlias;       // alias for the outer (source) table
    public string        JoinSource;        // outer table name
    public string        JoinSourceField;   // FK column on the outer table
    public string        TargetAlias;       // alias for the inner (target) table
    public string        JoinTarget;        // inner table name
    public string        JoinTargetField;   // PK column on the inner table
    public JoinTypeEnum  JoinType;
    public bool          Function;          // true when the source is a SQL function
    public Type          JoinTargetClass;   // CLR type mapped to the inner table
    public string        TempTableName;     // used when the source is materialised as a temp table

    public bool InList(List<JoinSpecification> specs);
    public bool ValueCompare(JoinSpecification other);

    public static JoinTypeEnum MapJoinType(JoinAttribute.JoinTypeEnum value);
    public static JoinTypeEnum MapJoinType(JoinSpecAttribute.JoinTypeEnum value);
}

public class TranslationJoinSpecification : JoinSpecification { }
```

#### `JoinOverride`

A query-time override that replaces the join type for one embedded `Record` subtype.
Created by `OrmQueryable<T>.InnerJoin<TJoined>()` / `.LeftOuterJoin<TJoined>()`.

```csharp
public readonly struct JoinOverride
{
    public readonly Type                          TargetType; // which embedded Record type to override
    public readonly JoinSpecification.JoinTypeEnum JoinType;  // the replacement join type

    public JoinOverride(Type targetType, JoinSpecification.JoinTypeEnum joinType);
}
```

SQL providers that override `QueryAll(..., IReadOnlyList<JoinOverride>)` should call
`QueryNode.SetJoinOverrides(overrides)` before building the SQL, which then calls
`GetJoinSQL(binding, fieldSubset, false, overrides)` to apply the overrides.

---

### 6.4 `FieldSubset`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/FieldSubset.cs`

Controls which fields are included in a SELECT or UPDATE. Pass `null` to include all fields.

```csharp
public class FieldSubset
{
    public enum InitialState
    {
        IncludeAll      = 1,  // include every field and join
        ExcludeAll      = 2,  // exclude everything (caller adds what is needed)
        Default         = 3,  // honour [EagerLoad] attributes; include by default
        IncludeAllJoins = 4,  // include all joins but honour [EagerLoad] for scalar fields
        ExcludeAllJoins = 5,  // exclude all joins; honour [EagerLoad] for scalar fields
    }

    // Constructors
    public FieldSubset(RecordBase rootObject, FactoryBase factory)
        // starts in ExcludeAll state; caller adds required fields
    public FieldSubset(RecordBase rootObject, InitialState state, FactoryBase factory)

    // Set operators (create new FieldSubset instances — non-mutating)
    public static FieldSubset operator +(FieldSubset a, FieldSubset b)   // union
    public static FieldSubset operator &(FieldSubset a, FieldSubset b)   // intersection
    public static FieldSubset operator |(FieldSubset a, FieldSubset b)   // OR union
    public static FieldSubset operator -(FieldSubset a, FieldSubset b)   // subtraction

    // Queries
    public bool IncludesField(string fieldName);
    public bool IncludesField(FieldBinding fieldBinding);
    public bool IncludesJoin(string objectBaseName);
    public bool IncludesField(RecordBase root, RecordBase enclosing, TField field);
    public bool IncludesEmbeddedObject(RecordBase root, RecordBase enclosing, RecordBase embedded);

    // Bulk operations
    public void IncludeAll(bool include);
    public bool AllIncluded();
    public int  CountIncludedFields();

    // Individual field/join control
    public void Include(RecordBase root, RecordBase enclosing, TField field);
    public void Exclude(RecordBase root, RecordBase enclosing, TField field);
    public void Include(RecordBase root, RecordBase enclosing, RecordBase joinTarget);
    public void Exclude(RecordBase root, RecordBase enclosing, RecordBase joinTarget);

    // Primary key guarantee
    public void EnsurePrimaryKeysIncluded();
}
```

---

### 6.5 `PersistenceException`

**Namespace:** `ActiveForge`
**File:** `src/ActiveForge/PersistenceException.cs`

The single exception type thrown by the ORM engine for all persistence failures.
Provider implementations should wrap native driver exceptions in `PersistenceException`.

```csharp
[Serializable]
public class PersistenceException : ApplicationException
{
    public PersistenceException(string message);
    public PersistenceException(string message, Exception inner);
}
```

---

## 7. Dialect Contracts

The table below summarises the dialect values used by the four existing providers. Use this as
a reference when implementing a new provider.

| Method | SQL Server | PostgreSQL | SQLite | Notes |
|---|---|---|---|---|
| `GetParameterMark()` | `@` | `@` | `@` | Prefix for named parameters in SQL |
| `GetLeftNameQuote()` | `[` | `"` | `"` | Left identifier quote character |
| `GetRightNameQuote()` | `]` | `"` | `"` | Right identifier quote character |
| `GetSourceNameSeparator()` | `.` | `.` | `.` | Schema.Table separator |
| `GetUpdateLock()` | `WITH (UPDLOCK)` | `FOR UPDATE` | *(empty)* | Appended after table name in SELECT … FOR UPDATE |
| `IsAutoIdentity()` | `true` | `true` | `true` | `false` if using explicit sequence generators |
| `GetStringConnectionOperator()` | `+` | `\|\|` | `\|\|` | String concatenation operator |
| `LimitRowCount(n, sql)` | `SELECT TOP {n} …` | returns `sql` unchanged | returns `sql` unchanged | SQL Server uses prefix; PG/SQLite use `GetPageSuffix` |
| `GetPageSuffix(start, count)` | *(empty)* | `LIMIT {n} OFFSET {s}` | `LIMIT {n} OFFSET {s}` | Used when `LimitRowCount` returns unchanged SQL |
| `PreInsertIdentityCommand` | `SET IDENTITY_INSERT {t} ON` | *(empty)* | *(empty)* | Executed before identity-field INSERT |
| `PostInsertIdentityCommand` | `SET IDENTITY_INSERT {t} OFF` | *(empty)* | *(empty)* | Executed after identity-field INSERT |
| Identity retrieval | `SELECT @@IDENTITY` | `RETURNING id` | `SELECT last_insert_rowid()` | See `PopulateIdentity` |
| Schema introspection | `SYSCOLUMNS` / `SYSTYPES` | `information_schema.columns` | `PRAGMA table_info` | See `PopulateSQLFieldCache` |

### Identity retrieval — important notes

- **SQL Server:** Use `SELECT @@IDENTITY` (connection-scoped), **not** `SCOPE_IDENTITY()`. ADO.NET
  parameterised queries are executed internally via `sp_executesql`, which creates a new scope;
  `SCOPE_IDENTITY()` returns `NULL` across scope boundaries.
- **PostgreSQL:** Use a `RETURNING id` clause appended to the INSERT SQL — no separate query required.
- **SQLite:** Use `SELECT last_insert_rowid()` executed on the same connection immediately after the INSERT.
- **MongoDB:** No SQL; identity is managed via a `__activeforge_counters` auto-increment collection.

---

## 8. Reference Implementation Checklist

Use this checklist when building a new provider.

### Adapter layer
- [ ] `MyAdapterConnection : ConnectionBase` — `Open`, `Close`, `IsConnected`, `DatabaseName`, `BeginTransaction`, `CreateCommand`, `TransactionState`
- [ ] `MyAdapterTransaction : TransactionBase` — `Commit`, `Rollback`, `Dispose`; expose `GetNativeTransaction()` for command binding
- [ ] `MyAdapterCommand : CommandBase` — `ExecuteNonQuery`, `ExecuteReader`, `ExecuteSequentialReader`, `ExecuteScalar`, `AddNativeParameter`, `SetToStoredProcedure`, `Cancel`, `Dispose`; unwrap `TField` objects in `AddNativeParameter`
- [ ] `MyAdapterReader : ReaderBase` — `Read`, `Close`, `Dispose`, `GetValue`, `IsDBNull`, `GetOrdinal`, `FieldCount`, `GetName`, `Record`

### Connection class
- [ ] `MyConnection : DBDataConnection` (SQL) or `MyConnection : DataConnection` (document)
- [ ] `CreateConnection` — return `new MyAdapterConnection(connectString)`
- [ ] All dialect methods — `GetParameterMark`, `GetLeftNameQuote`, `GetRightNameQuote`, `GetSourceNameSeparator`, `GetUpdateLock`, `IsAutoIdentity`, `GetStringConnectionOperator`, `GetGeneratorOperator`, `CreateConcatenateOperator`
- [ ] `LimitRowCount` — either prefix-based (SQL Server style) or return unchanged + override `GetPageSuffix`
- [ ] `PopulateIdentity` — query the last-inserted identity and assign to the identity `TField`
- [ ] `PreInsertIdentityCommand` / `PostInsertIdentityCommand` — return `""` if not applicable
- [ ] `PopulateSQLFieldCache` — query the DB schema catalog and populate `TargetFieldInfo` entries
- [ ] `OnUoWCommitted` / `OnUoWRolledBack` — sync `_transactionDepth` if needed

### Unit of Work
- [ ] `MyUnitOfWork : UnitOfWorkBase` — constructor accepts `MyConnection` + optional `ILogger`; override `BeginTransactionCore` to call `_connection.BeginTransaction(level)`

### DI extension
- [ ] `AddActiveForge{Name}(IServiceCollection, string, FactoryBase?)` — register scoped `MyConnection`, scoped `DataConnection` alias, scoped `IUnitOfWork`; return `new ActiveForgeBuilder(services)`

### Testing
- [ ] CRUD integration tests — insert, read, update, delete a simple entity
- [ ] Identity assignment — verify the identity field is populated after INSERT
- [ ] Query terms — `EqualTerm`, `LikeTerm`, `GreaterThanTerm`, `ContainsTerm` (IN clause)
- [ ] Pagination — `QueryPage` with non-zero `start`
- [ ] JOIN queries — inner join, left outer join via `QueryAll` with embedded `Record`
- [ ] Transactions — commit path, rollback path, nested UoW depth
- [ ] `IUnitOfWork` depth counter — outer commit only commits at depth 0
- [ ] LINQ layer — `conn.Query<T>().Where(...).OrderBy(...).Take(...).Skip(...).ToList()`

---

*See also:*
- [`docs/di-service-proxies.md`](di-service-proxies.md) — DI registration and service proxy patterns
- [`docs/unit-of-work.md`](unit-of-work.md) — UoW API, With.Transaction, isolation levels
- [`docs/joins.md`](joins.md) — JOIN conventions, `[JoinSpec]`, LINQ join overrides
- [`docs/linq-querying.md`](linq-querying.md) — LINQ operator reference
- [`docs/field-types.md`](field-types.md) — complete TField type reference
