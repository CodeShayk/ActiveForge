using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Turquoise.ORM.Attributes;
using Turquoise.ORM.Query;

namespace Turquoise.ORM
{
    /// <summary>
    /// Abstract base for all relational DB connections.
    /// Provides SQL generation (SELECT/INSERT/UPDATE/DELETE), parameterised binding,
    /// row fetching, transaction depth tracking, and a per-connection object-binding cache.
    /// Concrete subclasses supply the dialect-specific primitives.
    /// </summary>
    public abstract class DBDataConnection : DataConnection, IDisposable
    {
        // ══════════════════════════════════════════════════════════════════════════════
        // Inner types
        // ══════════════════════════════════════════════════════════════════════════════

        /// <summary>Per-connection cache of bindings, field-info, and descriptions.</summary>
        protected class InstanceCache
        {
            public Dictionary<string, ObjectBinding>   Bindings       = new Dictionary<string, ObjectBinding>();
            public Dictionary<string, TargetFieldInfo> TargetInfoCache = new Dictionary<string, TargetFieldInfo>();

            // Thread-safe description caches
            private readonly ConcurrentDictionary<string, string> _fieldDescriptions      = new ConcurrentDictionary<string, string>();
            private readonly ConcurrentDictionary<string, string> _objectDescriptions     = new ConcurrentDictionary<string, string>();
            private readonly ConcurrentDictionary<string, string> _validationMessages     = new ConcurrentDictionary<string, string>();

            public string GetFieldDescription(string key)               => _fieldDescriptions.TryGetValue(key, out var v) ? v : null;
            public void   SetFieldDescription(string key, string value) => _fieldDescriptions[key] = value;
            public string GetObjectDescription(string key)              => _objectDescriptions.TryGetValue(key, out var v) ? v : null;
            public void   SetObjectDescription(string key, string value)=> _objectDescriptions[key] = value;
            public string GetValidationMessage(string key)              => _validationMessages.TryGetValue(key, out var v) ? v : null;
            public void   SetValidationMessage(string key, string value)=> _validationMessages[key] = value;

            public void Flush()
            {
                Bindings        = new Dictionary<string, ObjectBinding>();
                TargetInfoCache = new Dictionary<string, TargetFieldInfo>();
                _fieldDescriptions.Clear();
                _objectDescriptions.Clear();
                _validationMessages.Clear();
            }

            public void FlushBindings() => Bindings = new Dictionary<string, ObjectBinding>();
        }

        /// <summary>Cached UPDATE SQL for one table in the class hierarchy.</summary>
        internal class UpdateSQLInfo
        {
            public string BaseSQL = "";
        }

        // ── Static caches ─────────────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<string, InstanceCache>  _connectionCache      = new ConcurrentDictionary<string, InstanceCache>();
        private static readonly ConcurrentDictionary<Type,   FieldSubset>    _defaultFieldSubsetCache = new ConcurrentDictionary<Type, FieldSubset>();
        private static readonly ConcurrentDictionary<Type,   string>         _columnTypeCache      = new ConcurrentDictionary<Type, string>();

        public static void ResetStaticCaches()
        {
            _connectionCache.Clear();
            _defaultFieldSubsetCache.Clear();
            _columnTypeCache.Clear();
        }

        // ── Instance state ────────────────────────────────────────────────────────
        protected readonly object       _syncRoot   = new object();
        protected string                _connectString;
        protected ConnectionBase        _connection;
        protected TransactionBase       _transaction;
        protected int                   _transactionDepth  = 0;
        protected bool                  _transactionFailed = false;
        protected FactoryBase           _factory;
        protected int                   _timeout    = 30;
        protected bool                  _databaseDatesInUTC = false;

        private readonly AliasGenerator _aliasGenerator = new AliasGenerator();
        public  AliasGenerator AliasGenerator => _aliasGenerator;

        protected bool IsConnectCreated;
        protected bool IsConnected => IsConnectCreated && _connection != null && _connection.IsConnected();
        public override bool IsOpen => IsConnected;

        // ── Constructors ──────────────────────────────────────────────────────────

        protected DBDataConnection(string connectString) : this(connectString, null) { }

        protected DBDataConnection(string connectString, FactoryBase factory)
        {
            _connectString = connectString;
            _factory       = factory;
            EnsureInstanceCache();
        }

        // ── Abstract dialect methods (implemented in SqlServerConnection) ──────────

        protected abstract ConnectionBase CreateConnection(string connectString);

        /// <summary>Wraps a SELECT into a row-limiting form (e.g. TOP N / LIMIT N).</summary>
        public abstract string LimitRowCount(int count, string fieldsAndFrom);

        /// <summary>Format the table/view name, schema-qualifying if needed.</summary>
        public virtual string ResolveFullyQualifiedName(string sourceName, bool isFunction) => QuoteName(sourceName);

        // ── Instance cache helpers ────────────────────────────────────────────────

        protected string InstanceCacheKey() => _connectString;

        private void EnsureInstanceCache()
            => _connectionCache.GetOrAdd(InstanceCacheKey(), _ => new InstanceCache());

        protected InstanceCache GetInstanceCache()
            => _connectionCache.TryGetValue(InstanceCacheKey(), out var c) ? c : null;

        public virtual void FlushSchema()
        {
            GetInstanceCache()?.Flush();
        }

        public virtual void FlushBindings()
        {
            GetInstanceCache()?.FlushBindings();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        public override bool Connect()
        {
            lock (_syncRoot)
            {
                if (!IsConnectCreated)
                {
                    _connection      = CreateConnection(_connectString);
                    IsConnectCreated = true;
                }
                if (!_connection.IsConnected())
                {
                    _connection.Open();
                    _connection.SetTimeout(_timeout);
                }
                return true;
            }
        }

        public override bool Disconnect()
        {
            lock (_syncRoot)
            {
                if (IsConnectCreated)
                {
                    if (IsConnected)
                    {
                        while (_transactionDepth > 0)
                            RollbackInternal();
                        _connection.Close();
                    }
                    IsConnectCreated = false;
                    _connection      = null;
                }
                return true;
            }
        }

        public void Close() => Disconnect();

        public void Dispose()
        {
            try { Disconnect(); } catch { }
        }

        public void SetTimeout(int timeout)
        {
            _timeout = timeout;
            if (IsConnected) _connection.SetTimeout(timeout);
        }

        public override int GetTimeout() => _timeout;

        public void SetDatabaseDatesInUTC(bool utc) => _databaseDatesInUTC = utc;
        public bool GetDatabaseDatesInUTC() => _databaseDatesInUTC;

        // ── Transactions (internal depth-tracking) ────────────────────────────────

        protected void BeginTransactionInternal()
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();
                if (_transactionDepth == 0)
                {
                    _transactionFailed = false;
                    _transaction = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
                }
                _transactionDepth++;
            }
        }

        protected void CommitInternal()
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0)
                {
                    _transactionDepth--;
                    if (_transactionDepth == 0)
                    {
                        try
                        {
                            if (!_transactionFailed)
                                _transaction.Commit();
                            else
                                _transaction.Rollback();
                        }
                        finally
                        {
                            _transaction.Dispose();
                            _transactionFailed = false;
                        }
                    }
                }
            }
        }

        protected void RollbackInternal()
        {
            lock (_syncRoot)
            {
                _transactionFailed = true;
                if (_transactionDepth > 0)
                {
                    _transactionDepth--;
                    if (_transactionDepth == 0)
                    {
                        try
                        {
                            _transaction.Rollback();
                        }
                        finally
                        {
                            _transaction.Dispose();
                        }
                    }
                }
            }
        }

        public int GetTransactionDepth() => _transactionDepth;
        protected TransactionBase GetInnerTransaction() => _transaction;

        /// <summary>
        /// Decrements <see cref="_transactionDepth"/> and clears the internal transaction
        /// reference after a UoW-managed transaction commits or rolls back via
        /// <see cref="DataConnection.RunWrite{T}"/>.  This is necessary because
        /// <see cref="UnitOfWorkBase"/> commits the underlying <see cref="TransactionBase"/>
        /// directly rather than going through <see cref="CommitTransaction"/>, so the
        /// provider's depth counter must be synced here.
        /// </summary>
        protected override void OnUoWCommitted()
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0)
                {
                    _transactionDepth--;
                    if (_transactionDepth == 0)
                    {
                        _transaction?.Dispose();
                        _transaction       = null;
                        _transactionFailed = false;
                    }
                }
            }
        }

        protected override void OnUoWRolledBack()
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0)
                {
                    _transactionDepth--;
                    if (_transactionDepth == 0)
                    {
                        _transaction       = null;
                        _transactionFailed = false;
                    }
                }
            }
        }

        // DataConnection abstract transaction interface

        public override TransactionBase BeginTransaction()
            => BeginTransaction(IsolationLevel.ReadCommitted);

        public override TransactionBase BeginTransaction(IsolationLevel level)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();
                if (_transactionDepth == 0)
                {
                    _transactionFailed = false;
                    _transaction = _connection.BeginTransaction(level);
                }
                _transactionDepth++;
                return _transaction;
            }
        }

        public override void CommitTransaction(TransactionBase transaction)
            => CommitInternal();

        public override void RollbackTransaction(TransactionBase transaction)
            => RollbackInternal();

        public override TransactionStates TransactionState(TransactionBase transaction)
        {
            if (_transactionDepth == 0) return TransactionStates.None;
            if (_transactionFailed)     return TransactionStates.RolledBack;
            return TransactionStates.Active;
        }

        // ── Utility ───────────────────────────────────────────────────────────────

        public CommandBase CreateCommand(string sql) => _connection.CreateCommand(sql);

        protected CommandBase CreateCommand(string sql, bool useTransaction)
        {
            var cmd = CreateCommand(sql);
            if (useTransaction && _transactionDepth > 0)
                cmd.SetTransaction(_transaction);
            return cmd;
        }

        // ── Type mapping ──────────────────────────────────────────────────────────

        public override Type MapType(Type generalization)
        {
            return _factory != null ? _factory.MapType(generalization) : generalization;
        }

        // ── Object creation ───────────────────────────────────────────────────────

        public override DataObject Create(Type type)
        {
            if (_factory != null) type = _factory.MapType(type);
            return (DataObject)Activator.CreateInstance(type);
        }

        public override DataObject Create(Type type, DataObject owner, bool isTemplate = false)
        {
            DataObject obj = Create(type);
            return obj;
        }

        public T CreateObject<T>() where T : DataObject => (T)Create(typeof(T));

        // ── FieldSubset factories ─────────────────────────────────────────────────

        public override FieldSubset DefaultFieldSubset(DataObject rootObject)
        {
            return _defaultFieldSubsetCache.GetOrAdd(rootObject.GetType(),
                _ => new global::Turquoise.ORM.FieldSubset(rootObject, global::Turquoise.ORM.FieldSubset.InitialState.Default, _factory));
        }

        public override FieldSubset FieldSubset(DataObject rootObject, FieldSubset.InitialState state)
            => new FieldSubset(rootObject, state, _factory);

        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, TField enclosed)
        {
            var fs = new FieldSubset(rootObject, _factory);
            fs.Include(rootObject, enclosing, enclosed);
            return fs;
        }

        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed)
            => FieldSubset(rootObject, enclosing, enclosed, Turquoise.ORM.FieldSubset.InitialState.Default);

        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed, FieldSubset.InitialState state)
        {
            var fs = new FieldSubset(rootObject, _factory);
            fs.Include(rootObject, enclosing, enclosed, state);
            return fs;
        }

        // ── Factory ───────────────────────────────────────────────────────────────

        public void AssignFactory(FactoryBase factory) => _factory = factory;

        // ── INSERT ────────────────────────────────────────────────────────────────

        public override bool Insert(DataObject obj) => RunWrite(() => InsertCore(obj));

        private bool InsertCore(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                ObjectBinding binding = obj.GetBinding(this, true, true);
                if (binding.InsertSQL.Count == 0)
                    binding.InsertSQL = GetInsertSQL(binding, obj);

                int insertCount = binding.InsertSQL.Count;
                bool createdTx  = false;
                if (_transactionDepth == 0 && insertCount > 1)
                {
                    BeginTransactionInternal();
                    createdTx = true;
                }

                bool success = true;
                Exception inner = null;
                try
                {
                    for (int i = 0; i < insertCount && success; i++)
                    {
                        var info = binding.InsertSQL[i];
                        if (info.SQL.Length == 0) continue;

                        var cmd = CreateCommand(info.SQL);
                        if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                        BindInsertParameters(obj, binding, cmd, i > 0 || InsertIdentityFields);
                        try
                        {
                            int result = cmd.ExecuteNonQuery();
                            success = result > 0;
                        }
                        catch (Exception e)
                        {
                            string dbg = "{ " + GetInsertParameterDebugString(obj, binding, i > 0 || InsertIdentityFields) + " }";
                            throw new PersistenceException($"Insert failed for {info.SQL}{dbg}", e);
                        }

                        if (i == 0 && !InsertIdentityFields)
                            PopulateIdentity(obj, binding, cmd);
                    }

                    if (createdTx) CommitInternal();
                }
                catch (Exception e)
                {
                    success = false;
                    inner   = e;
                }
                finally
                {
                    if (!success)
                    {
                        if (createdTx) RollbackInternal();
                        if (inner != null) throw new PersistenceException($"Insert failed", inner);
                    }
                }
                return success;
            }
        }

        // ── DELETE ────────────────────────────────────────────────────────────────

        public override bool Delete(DataObject obj) => RunWrite(() => DeleteCore(obj));

        private bool DeleteCore(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                ObjectBinding binding = obj.GetBinding(this, true, true);
                if (binding.DeleteSQL.Count == 0)
                    binding.DeleteSQL = GetDeleteSQL(binding);

                int deleteCount = binding.DeleteSQL.Count;
                bool createdTx  = false;
                if (_transactionDepth == 0 && deleteCount > 1)
                {
                    BeginTransactionInternal();
                    createdTx = true;
                }

                bool success = true;
                Exception inner = null;
                try
                {
                    for (int i = 0; i < deleteCount && success; i++)
                    {
                        var info = binding.DeleteSQL[i];
                        var cmd  = CreateCommand(info.SQL);
                        if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                        BindDeleteParameters(obj, binding, cmd);
                        try
                        {
                            int result = cmd.ExecuteNonQuery();
                            success = result > 0;
                        }
                        catch (Exception e)
                        {
                            string dbg = " { " + GetDeleteParameterDebugString(obj, binding) + " } ";
                            throw new PersistenceException($"Delete failed on SQL {info.SQL}{dbg}", e);
                        }
                    }

                    if (createdTx) CommitInternal();
                }
                catch (Exception e)
                {
                    success = false;
                    inner   = e;
                }
                finally
                {
                    if (!success)
                    {
                        if (createdTx) RollbackInternal();
                        if (inner != null) throw new PersistenceException("Delete failed", inner);
                    }
                }
                return success;
            }
        }

        public override bool Delete(DataObject obj, QueryTerm term) => RunWrite(() => DeleteTermCore(obj, term));

        private bool DeleteTermCore(DataObject obj, QueryTerm term)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                // Multi-table inheritance needs individual deletes
                ObjectBinding binding = obj.GetBinding(this, true, true, null, false);
                if (binding.IsDBDerived())
                {
                    var col = obj.QueryAll(term, null, 0) as ObjectCollection;
                    int n = col?.Count ?? 0;
                    bool createdTx = _transactionDepth == 0 && n > 1;
                    if (createdTx) BeginTransactionInternal();
                    try
                    {
                        for (int i = 0; i < n; i++)
                            col[i].Delete(this);
                        if (createdTx) CommitInternal();
                        return true;
                    }
                    catch (Exception e)
                    {
                        if (createdTx) RollbackInternal();
                        throw new PersistenceException("Delete(term) failed on derived object", e);
                    }
                }

                if (binding.DeleteSQLStub.Count == 0)
                    binding.DeleteSQLStub = GetDeleteSQLStub(binding);

                int stubCount = binding.DeleteSQLStub.Count;
                bool needsTx  = _transactionDepth == 0 && stubCount > 1;
                if (needsTx) BeginTransactionInternal();
                bool success  = true;
                Exception inner = null;
                string lastSQL  = "";
                try
                {
                    for (int i = 0; i < stubCount && success; i++)
                    {
                        int termNumber = 1;
                        var info = binding.DeleteSQLStub[i];
                        lastSQL  = info.SQL;
                        if (term != null)
                            lastSQL += " WHERE " + term.GetDeleteSQL(binding, ref termNumber);

                        var cmd = CreateCommand(lastSQL);
                        if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                        BindQueryParameters(obj, binding, cmd, term);
                        cmd.ExecuteNonQuery();
                        success = true;
                    }
                    if (needsTx) CommitInternal();
                }
                catch (Exception e)
                {
                    success = false;
                    inner   = e;
                }
                finally
                {
                    if (!success)
                    {
                        if (needsTx) RollbackInternal();
                        if (inner != null)
                        {
                            string dbg = " { " + GetQueryParameterDebugString(obj, binding, term) + " } ";
                            throw new PersistenceException($"Delete(term) failed on SQL {lastSQL}{dbg}", inner);
                        }
                    }
                }
                return success;
            }
        }

        internal override void Delete(DataObject obj, QueryTerm term, Type[] concreteTypes)
        {
            lock (_syncRoot)
            {
                var col = obj.QueryAll(term, null, 0, concreteTypes) as ObjectCollection;
                int n = col?.Count ?? 0;
                bool createdTx = _transactionDepth == 0 && n > 1;
                if (createdTx) BeginTransactionInternal();
                try
                {
                    for (int i = 0; i < n; i++)
                        col[i].Delete(this);
                    if (createdTx) CommitInternal();
                }
                catch (Exception e)
                {
                    if (createdTx) RollbackInternal();
                    throw new PersistenceException("Delete(term, concreteTypes) failed", e);
                }
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────────

        internal override FieldSubset Update(DataObject obj, DataObjectLock.UpdateOption option)
            => UpdateAll(obj);

        internal override FieldSubset UpdateAll(DataObject obj) => RunWrite(() => UpdateAllCore(obj));

        private FieldSubset UpdateAllCore(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                ObjectBinding binding = obj.GetBinding(this, true, true);
                var updateSQL = GetUpdateSQL(binding, obj);

                bool createdTx = _transactionDepth == 0 && updateSQL.Count > 1;
                if (createdTx) BeginTransactionInternal();

                bool success = true;
                Exception inner = null;
                try
                {
                    foreach (var info in updateSQL)
                    {
                        if (info.BaseSQL.Length == 0) continue;
                        var cmd = CreateCommand(info.BaseSQL);
                        if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                        BindUpdateParameters(obj, binding, cmd);
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {
                            string dbg = " { " + GetUpdateParameterDebugString(obj, binding) + " } ";
                            throw new PersistenceException($"Update failed on SQL {info.BaseSQL}{dbg}", e);
                        }
                    }
                    if (createdTx) CommitInternal();
                }
                catch (Exception e)
                {
                    success = false;
                    inner   = e;
                }
                finally
                {
                    if (!success)
                    {
                        if (createdTx) RollbackInternal();
                        if (inner != null) throw new PersistenceException("UpdateAll failed", inner);
                    }
                }
                return null;
            }
        }

        internal override FieldSubset UpdateChanged(DataObject obj)
            => UpdateAll(obj);

        // ── READ ─────────────────────────────────────────────────────────────────

        public override bool Read(DataObject obj)
            => Read(obj, null);

        public override bool Read(DataObject obj, FieldSubset fieldSubset)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                ObjectBinding binding = obj.GetBinding(this, true, true);
                bool defaultInUse = fieldSubset == null;
                if (fieldSubset == null)
                    fieldSubset = obj.DefaultFieldSubset();
                fieldSubset.EnsurePrimaryKeysIncluded();

                var fieldBindingSubset = binding.FieldBindingSubset(fieldSubset, binding.Fields);

                string sql = defaultInUse && binding.ReadSQL.Length > 0
                    ? binding.ReadSQL
                    : GetReadSQL(obj, binding, fieldBindingSubset, fieldSubset);

                if (defaultInUse && binding.ReadSQL.Length == 0)
                    binding.ReadSQL = sql;

                var cmd = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                BindReadParameters(obj, binding, cmd);

                bool res = false;
                try
                {
                    bool hasLarge = binding.GetLargeColumnCount() > 0;
                    using var reader = hasLarge ? cmd.ExecuteSequentialReader() : cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        FetchRow(obj, reader, binding, fieldBindingSubset, true);
                        SetObjectPKLoaded(obj, binding);
                        obj.SetLoaded();
                        res = true;
                    }
                    else
                    {
                        throw new PersistenceException("Read failed: " + obj.GetType().FullName);
                    }
                }
                catch (Exception e)
                {
                    string dbg = " { " + GetReadParameterDebugString(obj, binding) + " } ";
                    throw new PersistenceException($"Read failed {obj.GetType().FullName} SQL: {sql}{dbg}", e);
                }

                if (res) obj.PostFetch();
                return res;
            }
        }

        public override bool ReadForUpdate(DataObject obj, FieldSubset fieldSubset)
        {
            lock (_syncRoot)
            {
                if (_transactionDepth == 0)
                    throw new PersistenceException("ReadForUpdate only valid within a transaction");
                if (!IsConnected) Connect();

                ObjectBinding binding = obj.GetBinding(this, true, true);
                bool defaultInUse = fieldSubset == null;
                if (fieldSubset == null)
                    fieldSubset = obj.DefaultFieldSubset();
                fieldSubset.EnsurePrimaryKeysIncluded();

                var fieldBindingSubset = binding.FieldBindingSubset(fieldSubset, binding.UpdateFields);

                string sql = defaultInUse && binding.ReadForUpdateSQL.Length > 0
                    ? binding.ReadForUpdateSQL
                    : GetReadForUpdateSQL(obj, binding, fieldBindingSubset, fieldSubset);

                if (defaultInUse && binding.ReadForUpdateSQL.Length == 0)
                    binding.ReadForUpdateSQL = sql;

                var cmd = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                BindReadParameters(obj, binding, cmd);

                bool res = false;
                try
                {
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        FetchRow(obj, reader, binding, fieldBindingSubset, true, true);
                        res = true;
                    }
                    else
                    {
                        throw new PersistenceException("ReadForUpdate failed: " + obj.GetType().FullName);
                    }
                }
                catch (Exception e)
                {
                    string dbg = " { " + GetReadParameterDebugString(obj, binding) + " } ";
                    throw new PersistenceException($"ReadForUpdate failed {obj.GetType().FullName} SQL: {sql}{dbg}", e);
                }
                return res;
            }
        }

        // ── QUERY FIRST ───────────────────────────────────────────────────────────

        public override bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset)
            => QueryFirst(obj, term, sortOrder, fieldSubset, null);

        public override bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset, ObjectParameterCollectionBase objectParameters)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                bool includeLookups = obj.ShouldIncludeLookupDataObjectsInBinding(term, sortOrder);
                ObjectBinding binding = obj.GetBinding(this, true, true, null, includeLookups);

                bool defaultInUse = fieldSubset == null;
                if (fieldSubset == null) fieldSubset = obj.DefaultFieldSubset();
                fieldSubset.EnsurePrimaryKeysIncluded();

                var fieldBindingSubset = binding.FieldBindingSubset(fieldSubset, binding.Fields);

                string querySQL = defaultInUse && binding.QuerySQL.Length > 0
                    ? binding.QuerySQL
                    : GetQuerySQLStub(binding, fieldSubset, fieldBindingSubset, 0, null, objectParameters, null);

                if (defaultInUse && binding.QuerySQL.Length == 0)
                    binding.QuerySQL = querySQL;

                int termNumber = 1;
                // Strip "SELECT " from start and add limit
                string innerSQL = querySQL.Substring(7);
                if (term != null)
                    innerSQL += " WHERE " + term.GetSQL(binding, ref termNumber).SQL;
                if (sortOrder != null)
                    innerSQL += " ORDER BY " + sortOrder.GetSQL(binding);

                string sql = LimitRowCount(1, innerSQL);
                var cmd = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                BindQueryParameters(obj, binding, cmd, term);
                BindFunctionParameters(obj, binding, cmd, objectParameters, null);

                bool res = false;
                try
                {
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        FetchRow(obj, reader, binding, fieldBindingSubset, false);
                        res = true;
                    }
                }
                catch (Exception e)
                {
                    string dbg = FormatDebugInfo(obj, binding, term);
                    throw new PersistenceException($"QueryFirst failed SQL: {sql}{dbg}", e);
                }

                if (res) obj.PostFetch();
                return res;
            }
        }

        // ── QUERY COUNT ───────────────────────────────────────────────────────────

        public override int QueryCount(DataObject obj)
            => QueryCount(obj, null);

        public override int QueryCount(DataObject obj, QueryTerm term)
            => QueryCount(obj, term, null);

        public override int QueryCount(DataObject obj, QueryTerm term, Type[] expectedTypes)
            => QueryCount(obj, term, expectedTypes, null);

        public override int QueryCount(DataObject obj, QueryTerm term, Type[] expectedTypes, FieldSubset subsetIn)
            => QueryCount(obj, term, expectedTypes, subsetIn, null);

        protected int QueryCount(DataObject obj, QueryTerm term, Type[] expectedTypes, FieldSubset subsetIn, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();

                bool includeLookups = obj.ShouldIncludeLookupDataObjectsInBinding(term, null);
                bool useCache = expectedTypes == null;
                ObjectBinding binding = obj.GetBinding(this, true, useCache, expectedTypes, includeLookups);

                FieldSubset fieldSubset = subsetIn;
                List<FieldBinding> fieldBindingSubset;
                string sql;

                if (expectedTypes != null)
                {
                    if (expectedTypeFieldSubsets == null)
                        expectedTypeFieldSubsets = new Dictionary<Type, FieldSubset>();
                    foreach (var et in expectedTypes)
                    {
                        if (!expectedTypeFieldSubsets.ContainsKey(et))
                        {
                            var eo = (DataObject)DataObject.CreateDataObject(et, this);
                            expectedTypeFieldSubsets[et] = eo.DefaultFieldSubset();
                        }
                    }
                    if (fieldSubset == null) fieldSubset = obj.DefaultFieldSubset();
                    fieldSubset.EnsurePrimaryKeysIncluded();
                    EnsureConcreteTypeFieldSubsetsIncludePrimaryKeys(expectedTypeFieldSubsets);
                    fieldBindingSubset = binding.FieldBindingSubset(expectedTypeFieldSubsets, binding.Fields);
                    sql = GetQuerySQLStub(binding, fieldSubset, fieldBindingSubset, 0, expectedTypeFieldSubsets, null, null);
                }
                else
                {
                    if (fieldSubset == null)
                        fieldSubset = obj.FieldSubset(Turquoise.ORM.FieldSubset.InitialState.IncludeAll);
                    fieldBindingSubset = binding.FieldBindingSubset(fieldSubset, binding.Fields);
                    sql = GetQuerySQLStub(binding, fieldSubset, fieldBindingSubset, 0, null, null, null);
                }

                int termNumber = 1;
                if (term != null)
                    sql += " WHERE " + term.GetSQL(binding, ref termNumber).SQL;

                // Wrap in COUNT(*)
                sql = "SELECT COUNT(*) AAAA FROM (" + sql + ") Expr";

                var cmd = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                if (term != null) BindQueryParameters(obj, binding, cmd, term);

                try
                {
                    using var reader = cmd.ExecuteReader();
                    return reader.Read() ? Convert.ToInt32(reader.ColumnValue(0)) : 0;
                }
                catch (Exception e)
                {
                    string dbg = FormatDebugInfo(obj, binding, term);
                    throw new PersistenceException($"QueryCount failed SQL: {sql}{dbg}", e);
                }
            }
        }

        // ── QUERY ALL / LAZY QUERY ALL ────────────────────────────────────────────

        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset)
            => QueryAll(obj, term, sortOrder, pageSize, null, fieldSubset, null);

        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset)
            => QueryAll(obj, term, sortOrder, pageSize, expectedTypes, fieldSubset, null);

        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            var node = new QueryNode(this, obj);
            node.SetTerm(term);
            node.SetSortOrder(sortOrder);
            node.SetCount(pageSize);
            node.SetConcreteTypes(expectedTypes);
            node.SetConcreteTypeFieldSubsets(expectedTypeFieldSubsets);
            node.SetFieldSubset(fieldSubset);
            return node.QueryAll();
        }

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset)
            => LazyQueryAll<T>(obj, term, sortOrder, pageSize, null, fieldSubset);

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset)
        {
            var results = QueryAll(obj, term, sortOrder, pageSize, expectedTypes, fieldSubset);
            foreach (DataObject item in results)
                yield return (T)item;
        }

        // ── QUERY ALL / LAZY QUERY ALL — JOIN OVERRIDES ───────────────────────────

        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            if (joinOverrides == null || joinOverrides.Count == 0)
                return QueryAll(obj, term, sortOrder, pageSize, fieldSubset);
            var node = new QueryNode(this, obj);
            node.SetTerm(term);
            node.SetSortOrder(sortOrder);
            node.SetCount(pageSize);
            node.SetFieldSubset(fieldSubset);
            node.SetJoinOverrides(joinOverrides);
            return node.QueryAll();
        }

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            if (joinOverrides == null || joinOverrides.Count == 0)
                return LazyQueryAll<T>(obj, term, sortOrder, pageSize, fieldSubset);
            var results = QueryAll(obj, term, sortOrder, pageSize, fieldSubset, joinOverrides);
            return System.Linq.Enumerable.Cast<T>(results);
        }

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            if (joinOverrides == null || joinOverrides.Count == 0)
                return QueryPage(obj, term, sortOrder, start, count, fieldSubset);
            var node = new QueryNode(this, obj);
            node.SetTerm(term);
            node.SetSortOrder(sortOrder);
            node.SetStart(start);
            node.SetCount(count);
            node.SetFieldSubset(fieldSubset);
            node.SetJoinOverrides(joinOverrides);
            return node.QueryPage();
        }

        // ── QUERY PAGE ────────────────────────────────────────────────────────────

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, null, null, false);

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, expectedTypes, null, false);

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, expectedTypes, expectedTypeFieldSubsets, false);

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets, bool returnCountInfo)
        {
            var node = new QueryNode(this, obj);
            node.SetTerm(term);
            node.SetSortOrder(sortOrder);
            node.SetStart(start);
            node.SetCount(count);
            node.SetConcreteTypes(expectedTypes);
            node.SetConcreteTypeFieldSubsets(expectedTypeFieldSubsets);
            node.SetFieldSubset(fieldSubset);
            node.SetReturnCount(returnCountInfo);
            return node.QueryPage();
        }

        // ── EXEC SQL ─────────────────────────────────────────────────────────────

        public override ObjectCollection ExecSQL(DataObject obj, string sql)
            => ExecSQL(obj, sql, 0, 0, (Dictionary<string, object>)null);

        public override ObjectCollection ExecSQL(DataObject obj, string sqlFormat, params object[] values)
            => ExecSQL(obj, sqlFormat, 0, 0, values);

        public override ObjectCollection ExecSQL(DataObject obj, string sql, Dictionary<string, object> parameters)
            => ExecSQL(obj, sql, 0, 0, parameters);

        public override ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count)
            => ExecSQL(obj, sql, start, count, (Dictionary<string, object>)null);

        public override ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count, Dictionary<string, object> parameters)
        {
            lock (_syncRoot)
            {
                var results = new ObjectCollection { StartRecord = start, PageSize = count };
                if (!IsConnected) Connect();

                ObjectBinding binding = null;
                if (obj != null)
                    binding = obj.GetBinding(this, true, false);

                var cmd = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                    {
                        object val = kvp.Value is TField tf ? tf.GetValue() : kvp.Value;
                        cmd.AddParameter(kvp.Key, val ?? DBNull.Value);
                    }
                }

                try
                {
                    using var reader = cmd.ExecuteReader();
                    int scan = 0;
                    int added = 0;
                    while (reader.Read())
                    {
                        if (scan >= start)
                        {
                            var newObj = Create(obj.GetType(), null, true);
                            newObj.SetTarget(this);
                            FetchRow(newObj, reader, binding, binding.Fields, false);
                            results.AddTail(newObj);
                            added++;
                        }
                        scan++;
                        if (count > 0 && added >= count) break;
                    }
                }
                catch (Exception e)
                {
                    throw new PersistenceException($"ExecSQL failed: {sql}", e);
                }
                return results;
            }
        }

        public override ReaderBase ExecSQL(string sql)
        {
            if (!IsConnected) Connect();
            var cmd = CreateCommand(sql);
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
            return cmd.ExecuteReader();
        }

        public override ReaderBase ExecSQL(string sql, Dictionary<string, CommandBase.Parameter> parameters)
        {
            if (!IsConnected) Connect();
            var cmd = CreateCommand(sql);
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                    cmd.AddParameter(kvp.Value.Name, kvp.Value.Value ?? DBNull.Value);
            }
            return cmd.ExecuteReader();
        }

        // ── STORED PROCEDURES ─────────────────────────────────────────────────────

        public override ObjectCollection ExecStoredProcedure(DataObject obj, string spName, int start, int count, params DataObject.SPParameter[] spParameters)
            => RunWrite(() => ExecStoredProcedureCore(obj, spName, start, count, spParameters));

        private ObjectCollection ExecStoredProcedureCore(DataObject obj, string spName, int start, int count, DataObject.SPParameter[] spParameters)
        {
            lock (_syncRoot)
            {
                if (!IsConnected) Connect();
                ObjectBinding binding = obj.GetBinding(this, true, false);
                var cmd = CreateCommand(spName);
                cmd.SetToStoredProcedure();
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
                if (spParameters != null)
                {
                    foreach (var p in spParameters)
                        cmd.AddParameter(GetParameterMark() + p.Name, p.Value ?? DBNull.Value);
                }
                var results = new ObjectCollection { StartRecord = start, PageSize = count };
                try
                {
                    using var reader = cmd.ExecuteReader();
                    int scan = 0, added = 0;
                    while (reader.Read())
                    {
                        if (scan >= start)
                        {
                            var newObj = Create(obj.GetType(), null, true);
                            newObj.SetTarget(this);
                            FetchRow(newObj, reader, binding, binding.Fields, false);
                            results.AddTail(newObj);
                            added++;
                        }
                        scan++;
                        if (count > 0 && added >= count) break;
                    }
                }
                catch (Exception e)
                {
                    throw new PersistenceException($"ExecStoredProcedure failed: {spName}", e);
                }
                return results;
            }
        }

        // ── EXISTS SUB-QUERY ──────────────────────────────────────────────────────

        internal override QueryFragment GenerateExistsSQLQuery(DataObject obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            var node = new QueryNode(this, obj);
            node.SetTerm(term);
            node.SetSortOrder(sortOrder);
            node.SetStart(start);
            node.SetCount(count);
            node.SetFieldSubset(fieldSubset);
            node.SetConcreteTypes(expectedTypes);
            node.SetConcreteTypeFieldSubsets(expectedTypeFieldSubsets);
            return node.GenerateExistsSQLQuery(outerAlias, outerFieldName, linkField, ref termNumber);
        }

        // ── OBJECT BINDING ────────────────────────────────────────────────────────

        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache)
            => GetObjectBinding(obj, targetExists, useCache, null, false);

        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] expectedTypes)
            => GetObjectBinding(obj, targetExists, useCache, expectedTypes, false);

        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] expectedTypes, bool includeLookupDataObjects)
        {
            if (useCache && expectedTypes == null)
            {
                var cache = GetInstanceCache();
                string key = obj.AssemblyQualifiedName + (includeLookupDataObjects ? "_L" : "_");
                if (!cache.Bindings.TryGetValue(key, out var binding))
                {
                    lock (cache.Bindings)
                    {
                        if (!cache.Bindings.TryGetValue(key, out binding))
                        {
                            binding = new ObjectBinding(obj, this, targetExists, null, null, _factory, includeLookupDataObjects);
                            if (targetExists)
                                cache.Bindings[key] = binding;
                        }
                    }
                }
                return binding;
            }
            return new ObjectBinding(obj, this, targetExists, null, expectedTypes, _factory, includeLookupDataObjects);
        }

        public override ObjectBinding GetChangedObjectBinding(ObjectBase obj, ObjectBase changedObj)
            => new ObjectBinding(obj, this, true, changedObj, null, _factory, false);

        public override ObjectBinding GetDynamicObjectBinding(ObjectBase obj, ReaderBase reader)
        {
            int fieldCount = reader.ColumnCount();
            var binding    = new ObjectBinding();
            for (int i = 0; i < fieldCount; i++)
            {
                var info = new TargetFieldInfo
                {
                    IsInPK       = false,
                    IsIdentity   = false,
                    IsAutoGenerated = false,
                    GeneratorName   = "",
                    TargetName   = reader.ColumnName(i),
                    FieldInfo    = obj.GetType().GetField("BoundFields"),
                    IsIndexed    = true,
                    Index        = i,
                };
                info.TargetType = reader.ColumnValue(i)?.GetType() ?? typeof(object);
                var fb = new FieldBinding { Info = info };
                binding.Fields.Add(fb);
            }
            return binding;
        }

        // ── SQL GENERATION — INSERT ───────────────────────────────────────────────

        internal List<ObjectBinding.InsertSQLInfo> GetInsertSQL(ObjectBinding binding, DataObject obj)
        {
            var results = new List<ObjectBinding.InsertSQLInfo>();
            bool needsIdentity = InsertNeedsIdentity(binding);

            for (int ti = 0; ti < binding.UpdateTableAliases.Count; ti++)
            {
                string tableAlias = binding.UpdateTableAliases[ti];
                string sourceName = binding.AliasToSourceName(tableAlias);
                var fields  = new StringBuilder();
                var values  = new StringBuilder();
                bool first  = true;
                var info    = new ObjectBinding.InsertSQLInfo();

                foreach (var fb in binding.Fields)
                {
                    var tfi = fb.Info;
                    if (tfi.IsReadOnly || fb.MapNode == null || fb.MapNode.Alias != tableAlias || fb.Translation)
                        continue;

                    if (tfi.IsIdentity == false && (tfi.IsAutoGenerated == false || IsAutoIdentity()))
                    {
                        if (!first) { fields.Append(','); values.Append(','); }
                        else first = false;
                        fields.Append(QuoteName(tfi.TargetName));
                        values.Append(GetParameterMark()).Append(tfi.TargetName);
                    }
                    else if (tfi.IsAutoGenerated && !IsAutoIdentity())
                    {
                        if (!first) { fields.Append(','); values.Append(','); }
                        else first = false;
                        fields.Append(QuoteName(tfi.TargetName));
                        values.Append(GetGeneratorOperator(tfi));
                    }

                    // For derived (non-root) tables, also include PK
                    if (ti > 0 && tfi.IsInPK)
                    {
                        if (!first) { fields.Append(','); values.Append(','); }
                        else first = false;
                        fields.Append(QuoteName(tfi.TargetName));
                        values.Append(GetParameterMark()).Append(tfi.TargetName);
                    }
                }

                string preSql = (ti == 0 && needsIdentity && InsertIdentityFields)
                    ? PreInsertIdentityCommand(sourceName) + ";"
                    : "";
                string postSql = (ti == 0 && needsIdentity && InsertIdentityFields)
                    ? ";" + PostInsertIdentityCommand(sourceName)
                    : "";

                info.SQL = fields.Length == 0
                    ? preSql + $"INSERT INTO {QuoteName(sourceName)} DEFAULT VALUES" + postSql
                    : preSql + $"INSERT INTO {QuoteName(sourceName)} ({fields}) VALUES ({values})" + postSql;

                info.SourceName  = sourceName;
                info.TableAlias  = tableAlias;
                info.IsBaseTable = ti == 0;
                results.Add(info);
            }
            return results;
        }

        private bool InsertNeedsIdentity(ObjectBinding binding)
        {
            foreach (var fb in binding.UpdateFields)
            {
                if (fb.Info.IsIdentity && fb.Info.IsAutoGenerated)
                    return true;
            }
            return false;
        }

        protected virtual string PopulateIdentity(DataObject obj, ObjectBinding binding, CommandBase command)
            => ""; // overridden in SqlServerConnection

        protected virtual bool InsertIdentityFields => false;

        // ── SQL GENERATION — DELETE ───────────────────────────────────────────────

        internal List<ObjectBinding.DeleteSQLInfo> GetDeleteSQL(ObjectBinding binding)
        {
            var results  = new List<ObjectBinding.DeleteSQLInfo>();
            string criteria = "";

            for (int ti = 0; ti < binding.UpdateTableAliases.Count; ti++)
            {
                string tableAlias = binding.UpdateTableAliases[ti];
                string sourceName = binding.AliasToSourceName(tableAlias);
                var sqlInfo       = new ObjectBinding.DeleteSQLInfo { SourceName = sourceName, TableAlias = tableAlias };

                foreach (var fb in binding.Fields)
                {
                    if (fb.MapNode?.Alias != tableAlias) continue;
                    if (fb.Info.IsInPK)
                    {
                        if (criteria.Length > 0) criteria += " AND ";
                        criteria += QuoteName(fb.Info.TargetName) + "=" + GetParameterMark() + fb.Info.TargetName;
                    }
                }

                sqlInfo.SQL = $"DELETE FROM {ResolveFullyQualifiedName(sourceName, false)} WHERE {criteria}";
                // Insert at beginning so base table is deleted last
                results.Insert(0, sqlInfo);
            }
            return results;
        }

        internal List<ObjectBinding.DeleteSQLInfo> GetDeleteSQLStub(ObjectBinding binding)
        {
            var results = new List<ObjectBinding.DeleteSQLInfo>();

            for (int ti = 0; ti < binding.UpdateTableAliases.Count; ti++)
            {
                string tableAlias = binding.UpdateTableAliases[ti];
                string sourceName = binding.AliasToSourceName(tableAlias);
                var sqlInfo       = new ObjectBinding.DeleteSQLInfo
                {
                    SQL        = $"DELETE FROM {ResolveFullyQualifiedName(sourceName, false)}",
                    SourceName = sourceName,
                    TableAlias = tableAlias,
                };
                results.Insert(0, sqlInfo);
            }
            return results;
        }

        // ── SQL GENERATION — UPDATE ───────────────────────────────────────────────

        internal List<UpdateSQLInfo> GetUpdateSQL(ObjectBinding binding, DataObject obj)
        {
            var results  = new List<UpdateSQLInfo>();
            string criteria = "";

            for (int ti = 0; ti < binding.UpdateTableAliases.Count; ti++)
            {
                string tableAlias = binding.UpdateTableAliases[ti];
                string sourceName = "";
                var setClause     = new StringBuilder();
                var sqlInfo       = new UpdateSQLInfo();

                foreach (var fb in binding.UpdateFields)
                {
                    var tfi = fb.Info;
                    if (tfi.IsReadOnly || fb.MapNode?.Alias != tableAlias)
                        continue;

                    sourceName = fb.MapNode.SourceName;

                    if (tfi.IsInPK)
                    {
                        if (criteria.Length > 0) criteria += " AND ";
                        criteria += QuoteName(tfi.TargetName) + "=" + GetParameterMark() + tfi.TargetName;
                    }
                    else if (!tfi.IsIdentity && !tfi.IsAutoGenerated)
                    {
                        if (setClause.Length > 0) setClause.Append(',');
                        setClause.Append(QuoteName(tfi.TargetName)).Append('=').Append(GetParameterMark()).Append(tfi.TargetName);
                    }
                }

                if (setClause.Length > 0 && sourceName.Length > 0)
                    sqlInfo.BaseSQL = $"UPDATE {QuoteName(sourceName)} SET {setClause} WHERE {criteria}";

                results.Add(sqlInfo);
            }
            return results;
        }

        // ── SQL GENERATION — READ ─────────────────────────────────────────────────

        protected string GetReadSQL(DataObject obj, ObjectBinding binding, List<FieldBinding> fieldBindingSubset, FieldSubset fieldSubset)
        {
            var fields   = new StringBuilder();
            string criteria = "";
            string joins  = GetJoinSQL(binding, fieldSubset, false);

            foreach (var fb in fieldBindingSubset)
            {
                var tfi  = fb.Info;
                var node = fb.MapNode;
                if (node == null) continue;

                if (tfi.IsInPK && binding.UseAsPK(tfi) && binding.UpdateTableAliases.Contains(node.Alias))
                {
                    if (criteria.Length > 0) criteria += " AND ";
                    if (node.Alias.Length > 0)
                        criteria += QuoteName(node.Alias) + GetSourceNameSeparator();
                    criteria += QuoteName(tfi.TargetName) + "=" + GetParameterMark() + tfi.TargetName;
                }
                else
                {
                    if (fields.Length > 0) fields.Append(',');
                    if (node.Alias.Length > 0)
                        fields.Append(QuoteName(node.Alias)).Append(GetSourceNameSeparator());
                    fields.Append(QuoteName(tfi.TargetName));
                    if (fb.Alias.Length > 0)
                        fields.Append(' ').Append(fb.Alias);
                }
            }

            if (fields.Length == 0) fields.Append('*');
            return $"SELECT {fields} FROM {ResolveFullyQualifiedName(binding.SourceName, binding.Function)} {binding.GetRootAlias()}{joins} WHERE {criteria}";
        }

        protected string GetReadForUpdateSQL(DataObject obj, ObjectBinding binding, List<FieldBinding> fieldBindingSubset, FieldSubset fieldSubset)
        {
            var fields   = new StringBuilder();
            string criteria = "";
            string joins  = GetJoinSQL(binding, fieldSubset, true);

            foreach (var fb in fieldBindingSubset)
            {
                var tfi  = fb.Info;
                var node = fb.MapNode;
                if (node == null) continue;

                if (tfi.IsInPK && binding.UseAsPK(tfi) && binding.UpdateTableAliases.Contains(node.Alias))
                {
                    if (criteria.Length > 0) criteria += " AND ";
                    if (node.Alias.Length > 0)
                        criteria += QuoteName(node.Alias) + GetSourceNameSeparator();
                    criteria += QuoteName(tfi.TargetName) + "=" + GetParameterMark() + tfi.TargetName;
                }
                else
                {
                    if (fields.Length > 0) fields.Append(',');
                    if (node.Alias.Length > 0)
                        fields.Append(QuoteName(node.Alias)).Append(GetSourceNameSeparator());
                    fields.Append(QuoteName(tfi.TargetName));
                    if (fb.Alias.Length > 0)
                        fields.Append(' ').Append(fb.Alias);
                }
            }

            if (fields.Length == 0) fields.Append('*');
            string updateLock = GetUpdateLock();
            return $"SELECT {fields} FROM {ResolveFullyQualifiedName(binding.SourceName, binding.Function)} {binding.GetRootAlias()} {updateLock}{joins} WHERE {criteria}";
        }

        // ── SQL GENERATION — QUERY STUB ───────────────────────────────────────────

        protected string GetQuerySQLStub(ObjectBinding binding, FieldSubset fieldSubset, List<FieldBinding> fieldBindingSubset, int rowCount,
            Dictionary<Type, FieldSubset> fieldSubsets, ObjectParameterCollectionBase objectParameters,
            Dictionary<Type, ObjectParameterCollectionBase> concreteTypeObjectParameters,
            IReadOnlyList<JoinOverride> joinOverrides = null)
        {
            var fields = new StringBuilder(512);

            foreach (var fb in fieldBindingSubset)
            {
                if (fields.Length > 0) fields.Append(',');
                if (fb.MapNode != null && fb.MapNode.Alias.Length > 0)
                    fields.Append(QuoteName(fb.MapNode.Alias)).Append(GetSourceNameSeparator());
                else if (fb.MapNode == null)
                    Debug.WriteLine($"Missing MapNode for field {fb.Info?.TargetName}");
                fields.Append(QuoteName(fb.Info.TargetName));
                if (fb.Alias.Length > 0)
                    fields.Append(' ').Append(fb.Alias);
            }

            if (binding.ConcreteClassDiagnosticFields != null)
            {
                foreach (var fb in binding.ConcreteClassDiagnosticFields)
                {
                    if (fields.Length > 0) fields.Append(',');
                    if (fb.MapNode.Alias.Length > 0)
                        fields.Append(QuoteName(fb.MapNode.Alias)).Append(GetSourceNameSeparator());
                    fields.Append(QuoteName(fb.Info.TargetName));
                    if (fb.Alias.Length > 0)
                        fields.Append(' ').Append(fb.Alias);
                }
            }

            string joins;
            if (fieldSubsets != null)
                joins = GetJoinSQL(binding, fieldSubsets, false, concreteTypeObjectParameters);
            else if (joinOverrides != null && joinOverrides.Count > 0)
                joins = GetJoinSQL(binding, fieldSubset, false, joinOverrides);
            else
                joins = GetJoinSQL(binding, fieldSubset, false);

            string funcParams = "";
            if (binding.Function && objectParameters != null)
            {
                int idx = 0;
                funcParams = "(" + objectParameters.FormatFunctionParameters(ref idx, GetParameterMark(), false) + ")";
            }

            string fieldsStr = fields.Length > 0 ? fields.ToString() : "*";
            string from = $"FROM {ResolveFullyQualifiedName(binding.SourceName, binding.Function)}{funcParams} {binding.GetRootAlias()}{joins}";

            return rowCount == 0
                ? $"SELECT {fieldsStr} {from}"
                : LimitRowCount(rowCount, $"{fieldsStr} {from}");
        }

        // ── SQL GENERATION — JOINS ────────────────────────────────────────────────

        protected string GetJoinSQL(ObjectBinding binding, FieldSubset fieldSubset, bool withUpdateLock)
        {
            var specs = new List<JoinSpecification>();
            binding.GetJoinSpecifications(ref specs, fieldSubset, withUpdateLock);
            return JoinSQLFromJoinSpecifications(specs, withUpdateLock, null);
        }

        /// <summary>
        /// Variant of <see cref="GetJoinSQL(ObjectBinding,FieldSubset,bool)"/> that applies
        /// query-time join-type overrides before generating SQL.
        /// </summary>
        protected string GetJoinSQL(ObjectBinding binding, FieldSubset fieldSubset, bool withUpdateLock, IReadOnlyList<JoinOverride> overrides)
        {
            var specs = new List<JoinSpecification>();
            binding.GetJoinSpecifications(ref specs, fieldSubset, withUpdateLock);
            ApplyJoinOverrides(specs, overrides);
            return JoinSQLFromJoinSpecifications(specs, withUpdateLock, null);
        }

        private static void ApplyJoinOverrides(List<JoinSpecification> specs, IReadOnlyList<JoinOverride> overrides)
        {
            if (overrides == null || overrides.Count == 0) return;
            foreach (var ov in overrides)
                for (int i = 0; i < specs.Count; i++)
                    if (specs[i].JoinTargetClass == ov.TargetType)
                        specs[i].JoinType = ov.JoinType;
        }

        protected string GetJoinSQL(ObjectBinding binding, Dictionary<Type, FieldSubset> fieldSubsets, bool withUpdateLock, Dictionary<Type, ObjectParameterCollectionBase> concreteTypeObjectParams)
        {
            var specs = new List<JoinSpecification>();
            binding.GetPolymorphicJoinSpecifications(ref specs, withUpdateLock, fieldSubsets);
            return JoinSQLFromJoinSpecifications(specs, withUpdateLock, concreteTypeObjectParams);
        }

        protected string JoinSQLFromJoinSpecifications(List<JoinSpecification> specs, bool withUpdateLock, Dictionary<Type, ObjectParameterCollectionBase> concreteTypeObjectParams)
        {
            var sb = new StringBuilder();
            foreach (var join in specs)
            {
                sb.Append(JoinTypeString(join.JoinType));
                sb.Append(ResolveFullyQualifiedName(join.JoinTarget, join.Function));
                sb.Append(' ').Append(QuoteName(join.TargetAlias));
                if (withUpdateLock) sb.Append(' ').Append(GetUpdateLock());
                sb.Append(" ON ");
                sb.Append(QuoteName(join.SourceAlias)).Append(GetSourceNameSeparator()).Append(QuoteName(join.JoinSourceField));
                sb.Append(" = ");
                sb.Append(QuoteName(join.TargetAlias)).Append(GetSourceNameSeparator()).Append(QuoteName(join.JoinTargetField));
            }
            return sb.ToString();
        }

        protected string JoinTypeString(JoinSpecification.JoinTypeEnum joinType)
        {
            return joinType switch
            {
                JoinSpecification.JoinTypeEnum.LeftOuterJoin  => " LEFT OUTER JOIN ",
                JoinSpecification.JoinTypeEnum.RightOuterJoin => " RIGHT OUTER JOIN ",
                _                                              => " INNER JOIN ",
            };
        }

        // ── PAGINATION SUFFIX ────────────────────────────────────────────────────────
        //
        // Dialects that use a suffix-style row limit (e.g. SQLite's LIMIT N OFFSET M)
        // override this method and return the suffix string that is appended after the
        // WHERE and ORDER BY clauses have already been appended to the query.
        // The default implementation returns an empty string (no suffix); such dialects
        // instead embed the row limit via LimitRowCount (e.g. SQL Server's SELECT TOP N).
        //
        // When GetPageSuffix returns a non-empty string the database itself handles the
        // skip, so PerformFetch should use firstSignificant = 0 rather than _start.

        protected virtual string GetPageSuffix(int start, int count) => "";

        // ── ROW FETCH ─────────────────────────────────────────────────────────────

        public void FetchRow(DataObject obj, ReaderBase reader, ObjectBinding binding, List<FieldBinding> fieldBindings, bool omitPK)
            => FetchRow(obj, reader, binding, fieldBindings, omitPK, false);

        public void FetchRow(DataObject obj, ReaderBase reader, ObjectBinding binding, List<FieldBinding> fieldBindings, bool omitPK, bool shallow)
        {
            binding.FetchRowValues(fieldBindings, FetchField, obj, reader, omitPK, shallow, 1);
            obj.PerformPostFetchProcesses();
        }

        public DataObject FetchRow(ReaderBase reader, ObjectBinding binding, List<FieldBinding> fieldBindings, bool omitPK)
        {
            DataObject obj = binding.FetchRowValues(fieldBindings, FetchField, reader, omitPK, false, this, 1);
            if (obj != null) obj.PerformPostFetchProcesses();
            return obj;
        }

        public object FetchValue(ReaderBase reader, FieldBinding fieldBinding)
        {
            object col = reader.ColumnValue(fieldBinding.Alias.Length > 0 ? fieldBinding.Alias : fieldBinding.Info.TargetName);
            return col == DBNull.Value ? null : col;
        }

        public void FetchField(DataObject obj, ReaderBase reader, FieldBinding fieldBinding, ObjectBinding binding, bool omitPK, bool omitPKForOrdinals)
        {
            var info = fieldBinding.Info;
            if (info.IsInPK && omitPK) return;

            string colName = fieldBinding.Alias.Length > 0 ? fieldBinding.Alias : info.TargetName;
            object column;
            try
            {
                int ordinal = reader.ColumnOrdinal(colName);
                column = reader.ColumnValue(ordinal);
            }
            catch (Exception e)
            {
                throw new PersistenceException($"Could not determine ordinal for column '{colName}'", e);
            }

            if (column == null || column is DBNull)
                return;

            // Trim strings unless [NoTrim] is present
            if (column is string s)
            {
                bool noTrim = CustomAttributeCache.GetFieldAttribute(info.FieldInfo, typeof(NoTrimAttribute), true) != null;
                if (!noTrim) column = s.Trim();
            }

            try
            {
                info.SetValue(obj, column);
            }
            catch (Exception e)
            {
                throw new PersistenceException($"Error setting field '{info.FieldName}' from column '{colName}' value '{column}'", e);
            }
        }

        // ── PARAMETER BINDING ─────────────────────────────────────────────────────

        protected void BindInsertParameters(DataObject obj, ObjectBinding binding, CommandBase cmd, bool includePK)
        {
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (info.IsReadOnly) continue;
                if (info.IsIdentity == false && (info.IsAutoGenerated == false || IsAutoIdentity()))
                {
                    object val = obj.IsNull(info.FieldInfo.Name) ? DBNull.Value : (info.GetValue(obj) ?? (object)DBNull.Value);
                    cmd.AddParameter(GetParameterMark() + info.TargetName, val, info);
                }
                else if (includePK && info.IsInPK)
                {
                    object val = obj.IsNull(info.FieldInfo.Name) ? DBNull.Value : (info.GetValue(obj) ?? (object)DBNull.Value);
                    cmd.AddParameter(GetParameterMark() + info.TargetName, val, info);
                }
            }
        }

        protected void BindDeleteParameters(DataObject obj, ObjectBinding binding, CommandBase cmd)
        {
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (!info.IsReadOnly && info.IsInPK)
                {
                    object val = obj.IsNull(info.FieldInfo.Name) ? DBNull.Value : (info.GetValue(obj) ?? (object)DBNull.Value);
                    cmd.AddParameter(GetParameterMark() + info.TargetName, val, info);
                }
            }
        }

        protected void BindUpdateParameters(DataObject obj, ObjectBinding binding, CommandBase cmd)
        {
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (info.IsReadOnly) continue;
                if (!info.IsInPK && !info.IsIdentity && (info.IsAutoGenerated == false || IsAutoIdentity()))
                {
                    object val = obj.IsNull(info.FieldInfo.Name) ? DBNull.Value : (info.GetValue(obj) ?? (object)DBNull.Value);
                    cmd.AddParameter(GetParameterMark() + info.TargetName, val, info);
                }
            }
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (!info.IsReadOnly && info.IsInPK)
                    cmd.AddParameter(GetParameterMark() + info.TargetName, info.GetValue(obj) ?? DBNull.Value, info);
            }
        }

        protected void BindReadParameters(DataObject obj, ObjectBinding binding, CommandBase cmd)
        {
            foreach (var fb in binding.Fields)
            {
                var info = fb.Info;
                if (info.IsInPK && binding.UseAsPK(info))
                    cmd.AddParameter(GetParameterMark() + info.TargetName, info.GetValue(obj) ?? DBNull.Value, info);
            }
        }

        protected void BindQueryParameters(DataObject obj, ObjectBinding binding, CommandBase cmd, QueryTerm query)
        {
            int termNumber = 1;
            BindQueryParameters(obj, binding, cmd, query, ref termNumber);
        }

        protected void BindQueryParameters(DataObject obj, ObjectBinding binding, CommandBase cmd, QueryTerm query, ref int termNumber)
        {
            query?.BindParameters(obj, binding, cmd, ref termNumber);
        }

        protected void BindJoinParameters(DataObject obj, ObjectBinding binding, CommandBase cmd)
        {
            // No translation join parameters in this port.
        }

        protected void BindFunctionParameters(DataObject obj, ObjectBinding binding, CommandBase cmd,
            ObjectParameterCollectionBase objectParameters, Dictionary<Type, ObjectParameterCollectionBase> concreteTypeObjectParams)
        {
            int idx = 0;
            objectParameters?.BindFunctionParameters(cmd, ref idx, GetParameterMark());
            if (concreteTypeObjectParams != null)
            {
                foreach (var kvp in concreteTypeObjectParams)
                    kvp.Value?.BindFunctionParameters(cmd, ref idx, GetParameterMark());
            }
        }

        // ── PARAMETER HELPERS ─────────────────────────────────────────────────────

        protected void SetObjectPKLoaded(DataObject obj, ObjectBinding binding)
        {
            foreach (var fb in binding.Fields)
            {
                var info = fb.Info;
                if (info.IsInPK && binding.UseAsPK(info))
                {
                    if (info.FieldInfo.GetValue(obj) is TField field)
                        field.SetLoaded(true);
                }
            }
        }

        protected void EnsureConcreteTypeFieldSubsetsIncludePrimaryKeys(Dictionary<Type, FieldSubset> subsets)
        {
            if (subsets == null) return;
            foreach (var kvp in subsets)
                kvp.Value?.EnsurePrimaryKeysIncluded();
        }

        // ── DEBUG HELPERS ─────────────────────────────────────────────────────────

        protected string FormatDebugInfo(DataObject obj, ObjectBinding binding, QueryTerm term)
        {
            string q = term != null ? GetQueryParameterDebugString(obj, binding, term) : "";
            return q.Length > 0 ? " { " + q + " } " : "";
        }

        protected string GetInsertParameterDebugString(DataObject obj, ObjectBinding binding, bool includePK)
        {
            var parts = new List<string>();
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (info.IsReadOnly) continue;
                if (info.IsIdentity == false && (info.IsAutoGenerated == false || IsAutoIdentity()))
                    parts.Add(FormatParamDebug(obj, info));
                else if (includePK && info.IsInPK)
                    parts.Add(FormatParamDebug(obj, info));
            }
            return string.Join(", ", parts);
        }

        protected string GetUpdateParameterDebugString(DataObject obj, ObjectBinding binding)
        {
            var parts = new List<string>();
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (info.IsReadOnly) continue;
                if (!info.IsInPK && !info.IsIdentity && !info.IsAutoGenerated)
                    parts.Add(FormatParamDebug(obj, info));
            }
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (!info.IsReadOnly && info.IsInPK)
                    parts.Add(FormatParamDebug(obj, info));
            }
            return string.Join(", ", parts);
        }

        protected string GetDeleteParameterDebugString(DataObject obj, ObjectBinding binding)
        {
            var parts = new List<string>();
            foreach (var fb in binding.UpdateFields)
            {
                var info = fb.Info;
                if (!info.IsReadOnly && info.IsInPK)
                    parts.Add(FormatParamDebug(obj, info));
            }
            return string.Join(", ", parts);
        }

        protected string GetReadParameterDebugString(DataObject obj, ObjectBinding binding)
        {
            var parts = new List<string>();
            if (binding != null)
                foreach (var fb in binding.Fields)
                {
                    var info = fb.Info;
                    if (info.IsInPK && binding.UseAsPK(info))
                        parts.Add(FormatParamDebug(obj, info));
                }
            return string.Join(", ", parts);
        }

        protected string GetQueryParameterDebugString(DataObject obj, ObjectBinding binding, QueryTerm query)
        {
            int n = 1;
            string result = "";
            query?.GetParameterDebugInfo(obj, binding, ref n, ref result);
            return result;
        }

        private string FormatParamDebug(DataObject obj, TargetFieldInfo info)
        {
            bool sensitive = ObjectBase.GetFieldSensitive(info.FieldInfo);
            string val = sensitive ? "***" : (info.GetValue(obj)?.ToString() ?? "null");
            return $"{GetParameterMark()}{info.TargetName}={val}";
        }

        // ── SCHEMA / FIELD INFO ───────────────────────────────────────────────────

        public override TargetFieldInfo GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName)
        {
            // Check cache first
            var cached = GetTargetFieldInfoFromCache(sourceName, fieldName);
            if (cached != null) return cached;

            // Walk all loaded types for a match
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsClass || !type.IsSubclassOf(typeof(DataObject))) continue;
                        if (type.Name != fullClassName && type.FullName != fullClassName) continue;
                        foreach (var fi in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (fi.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                var tfi = BuildTargetFieldInfo(fi, sourceName);
                                AddTargetFieldInfoToCache(sourceName, fieldName, tfi);
                                return tfi;
                            }
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        public override List<TargetFieldInfo> GetTargetFieldInfo(string sourceName)
        {
            var results = new List<TargetFieldInfo>();
            // Simplified: not used in hot path
            return results;
        }

        private TargetFieldInfo BuildTargetFieldInfo(FieldInfo fi, string sourceName)
        {
            var colAttr = CustomAttributeCache.GetFieldAttribute(fi, typeof(ColumnAttribute), false) as ColumnAttribute;
            var idAttr  = CustomAttributeCache.GetFieldAttribute(fi, typeof(IdentityAttribute), false);
            var tfi = new TargetFieldInfo
            {
                FieldInfo    = fi,
                FieldName    = fi.Name,
                TargetName   = colAttr?.ColumnName ?? fi.Name,
                SourceName   = sourceName,
                TargetType   = fi.FieldType,
                IsIdentity   = idAttr != null,
                IsInPK       = idAttr != null,
            };
            return tfi;
        }

        public override TargetFieldInfo GetTargetFieldInfoFromCache(string sourceName, string targetFieldName)
        {
            var cache = GetInstanceCache();
            if (cache == null) return null;
            cache.TargetInfoCache.TryGetValue(sourceName + "." + targetFieldName.ToLower(), out var result);
            return result;
        }

        public override void AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info)
        {
            var cache = GetInstanceCache();
            if (cache == null) return;
            string key = sourceName + "." + targetFieldName.ToLower();
            lock (cache.TargetInfoCache)
            {
                if (!cache.TargetInfoCache.ContainsKey(key))
                    cache.TargetInfoCache[key] = info;
            }
        }

        // ── TABLE EXISTS ──────────────────────────────────────────────────────────

        public override bool TableExists(ObjectBase obj)
        {
            // Attempt a SELECT TOP 1 to test existence; dialect may override
            try
            {
                var meta = DataObjectMetaDataCache.GetTypeMetaData(obj.GetType());
                string testSql = LimitRowCount(1, $"* FROM {QuoteName(meta.SourceName)}");
                using var cmd    = CreateCommand($"SELECT {testSql}");
                using var reader = cmd.ExecuteReader();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── DESCRIPTIONS / VALIDATION ─────────────────────────────────────────────

        public override string GetValidationMessage(string key, string defaultValue)
        {
            var cache = GetInstanceCache();
            return cache?.GetValidationMessage(key) ?? defaultValue;
        }

        public override string GetFieldDescription(FieldInfo fi, ObjectBase obj)
        {
            var cache = GetInstanceCache();
            string key = obj.GetType().FullName + "." + fi.Name;
            string cached = cache?.GetFieldDescription(key);
            if (cached != null) return cached;

            string desc = obj.GetFieldDescription(fi);
            cache?.SetFieldDescription(key, desc);
            return desc;
        }

        public override string GetDataObjectDescription(ObjectBase obj)
        {
            var cache = GetInstanceCache();
            string key = obj.GetType().FullName;
            string cached = cache?.GetObjectDescription(key);
            if (cached != null) return cached;

            string desc = obj.GetType().Name; // default - subclasses can override
            cache?.SetObjectDescription(key, desc);
            return desc;
        }

        // ── IDENTITY COMMANDS ─────────────────────────────────────────────────────

        public override string PreInsertIdentityCommand(string sourceName)  => "";
        public override string PostInsertIdentityCommand(string sourceName) => "";

        // ── ACTION QUEUE ──────────────────────────────────────────────────────────

        protected List<ActionQueueEntry> _actionQueue;

        protected List<ActionQueueEntry> GetActionQueue()
            => _actionQueue ??= new List<ActionQueueEntry>();

        public override void ProcessActionQueue() => RunWrite(() => ProcessActionQueueCore());

        private void ProcessActionQueueCore()
        {
            lock (_syncRoot)
            {
                if (_actionQueue != null)
                {
                    foreach (var entry in _actionQueue)
                        entry.Act();
                    ClearActionQueue();
                }
            }
        }

        public override void ClearActionQueue()
        {
            _actionQueue?.Clear();
        }

        public override void QueueForInsert(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0) GetActionQueue().Add(new InsertActionQueueEntry(this, obj));
                else Insert(obj);
            }
        }

        public override void QueueForUpdate(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0) GetActionQueue().Add(new UpdateActionQueueEntry(this, obj));
                else Update(obj, DataObjectLock.UpdateOption.ReleaseLock);
            }
        }

        public override void QueueForDelete(DataObject obj)
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0) GetActionQueue().Add(new DeleteActionQueueEntry(this, obj, null));
                else Delete(obj);
            }
        }

        public override void QueueForDelete(DataObject obj, QueryTerm term)
        {
            lock (_syncRoot)
            {
                if (_transactionDepth > 0) GetActionQueue().Add(new DeleteActionQueueEntry(this, obj, term));
                else Delete(obj, term);
            }
        }

        // ── ACTION QUEUE ENTRIES ──────────────────────────────────────────────────

        protected abstract class ActionQueueEntry
        {
            protected readonly DataObject          Object;
            protected readonly DBDataConnection    Connection;
            protected ActionQueueEntry(DBDataConnection conn, DataObject obj) { Connection = conn; Object = obj; }
            public abstract void Act();
        }

        protected class InsertActionQueueEntry : ActionQueueEntry
        {
            public InsertActionQueueEntry(DBDataConnection c, DataObject o) : base(c, o) { }
            public override void Act() { if (Object != null) Connection.Insert(Object); }
        }

        protected class UpdateActionQueueEntry : ActionQueueEntry
        {
            public UpdateActionQueueEntry(DBDataConnection c, DataObject o) : base(c, o) { }
            public override void Act() { if (Object != null) Connection.Update(Object, DataObjectLock.UpdateOption.ReleaseLock); }
        }

        protected class DeleteActionQueueEntry : ActionQueueEntry
        {
            private readonly QueryTerm _term;
            public DeleteActionQueueEntry(DBDataConnection c, DataObject o, QueryTerm t) : base(c, o) { _term = t; }
            public override void Act()
            {
                if (Object != null)
                {
                    if (_term == null) Connection.Delete(Object);
                    else Connection.Delete(Object, _term);
                }
            }
        }

        // ── QUERY NODE ────────────────────────────────────────────────────────────

        public class QueryNode
        {
            protected readonly DBDataConnection _conn;
            protected DataObject         _object;
            protected ObjectBinding      _binding;
            protected QueryTerm          _term;
            protected SortOrder          _sortOrder;
            protected FieldSubset        _fieldSubset;
            protected List<FieldBinding> _fieldBindingSubset;
            protected Type[]             _concreteTypes;
            protected Dictionary<Type, FieldSubset> _concreteTypeFieldSubsets;
            protected int    _start;
            protected int    _count;
            protected bool   _returnCount;
            protected bool   _fieldSubsetProvided;
            protected int    _index;
            private   IReadOnlyList<JoinOverride> _joinOverrides;

            public QueryNode(DBDataConnection conn, DataObject obj)
            {
                _conn   = conn;
                _object = obj;
            }

            public void SetTerm(QueryTerm t)          { _term = t; }
            public void SetSortOrder(SortOrder s)     { _sortOrder = s; }
            public void SetFieldSubset(FieldSubset fs){ _fieldSubset = fs; }
            public void SetStart(int s)               { _start = s; }
            public void SetCount(int c)               { _count = c; }
            public void SetReturnCount(bool r)        { _returnCount = r; }

            public void SetConcreteTypes(Type[] types)
            {
                if (types == null) { _concreteTypes = null; return; }
                var mapped = new List<Type>(types.Length);
                foreach (var t in types) mapped.Add(_conn.MapType(t));
                _concreteTypes = mapped.ToArray();
            }

            public void SetConcreteTypeFieldSubsets(Dictionary<Type, FieldSubset> subsets)
                => _concreteTypeFieldSubsets = subsets;

            public void SetBinding(ObjectBinding b) => _binding = b;

            /// <summary>Specifies join-type overrides applied when building the query SQL stub.</summary>
            public void SetJoinOverrides(IReadOnlyList<JoinOverride> overrides) => _joinOverrides = overrides;

            // ── Setup ─────────────────────────────────────────────────────────────

            protected void PrepareBinding()
            {
                bool includeLookups = _object.ShouldIncludeLookupDataObjectsInBinding(_term, _sortOrder);
                _binding = _object.GetBinding(_conn, true, false, _concreteTypes, includeLookups);
            }

            protected void PrepareFieldSubset()
            {
                _fieldSubsetProvided = _fieldSubset != null;
                if (_concreteTypes != null)
                {
                    if (_concreteTypeFieldSubsets == null)
                        _concreteTypeFieldSubsets = new Dictionary<Type, FieldSubset>();
                    foreach (var ct in _concreteTypes)
                    {
                        if (!_concreteTypeFieldSubsets.ContainsKey(ct))
                        {
                            var co = (DataObject)DataObject.CreateDataObject(ct, _conn);
                            _concreteTypeFieldSubsets[ct] = co.DefaultFieldSubset();
                        }
                    }
                    if (_fieldSubset == null) _fieldSubset = _object.DefaultFieldSubset();
                    _fieldSubset.EnsurePrimaryKeysIncluded();
                    _conn.EnsureConcreteTypeFieldSubsetsIncludePrimaryKeys(_concreteTypeFieldSubsets);
                    _fieldBindingSubset = _binding.FieldBindingSubset(_concreteTypeFieldSubsets, _binding.Fields);
                }
                else
                {
                    if (_fieldSubset == null) _fieldSubset = _object.DefaultFieldSubset();
                    _fieldSubset.EnsurePrimaryKeysIncluded();
                    _fieldBindingSubset = _binding.FieldBindingSubset(_fieldSubset, _binding.Fields);
                }
            }

            protected string BuildQuerySQL(int rowLimit = 0)
            {
                PrepareBinding();
                PrepareFieldSubset();

                bool hasJoinOverrides = _joinOverrides != null && _joinOverrides.Count > 0;
                // Never use the cached stub when join overrides are active — the stub would
                // embed the wrong join SQL and the cache should not be poisoned with it.
                bool defaultInUse = !_fieldSubsetProvided && _concreteTypeFieldSubsets == null && !hasJoinOverrides;
                string stub;
                if (defaultInUse && _binding.QuerySQL.Length > 0)
                {
                    stub = _binding.QuerySQL;
                }
                else
                {
                    stub = _conn.GetQuerySQLStub(_binding, _fieldSubset, _fieldBindingSubset, rowLimit, _concreteTypeFieldSubsets, null, null, _joinOverrides);
                    if (defaultInUse) _binding.QuerySQL = stub;
                }

                return stub;
            }

            // ── QueryAll ──────────────────────────────────────────────────────────

            public virtual ObjectCollection QueryAll()
            {
                string stub = BuildQuerySQL(0);
                int termNumber = 1;
                string sql = stub;
                if (_term != null) sql += " WHERE " + _term.GetSQL(_binding, ref termNumber).SQL;
                if (_sortOrder != null) sql += " ORDER BY " + _sortOrder.GetSQL(_binding);
                return PerformFetch(sql, 0);
            }

            // ── QueryPage ─────────────────────────────────────────────────────────

            public virtual ObjectCollection QueryPage()
            {
                // For prefix-style limits (e.g. SQL Server SELECT TOP N) pass the row
                // limit into the stub.  For suffix-style dialects (e.g. SQLite LIMIT N)
                // BuildQuerySQL is called without a row limit so the stub stays clean;
                // the limit is instead appended after WHERE and ORDER BY via GetPageSuffix.
                bool hasSuffix = _count > 0 && _count < int.MaxValue
                    && _conn.GetPageSuffix(0, 1).Length > 0;
                int limit = hasSuffix ? 0
                    : (_count > 0 && _count < int.MaxValue ? _start + _count + 1 : 0);
                string stub = BuildQuerySQL(limit);
                int termNumber = 1;
                string sql = stub;
                if (_term != null) sql += " WHERE " + _term.GetSQL(_binding, ref termNumber).SQL;
                if (_sortOrder != null) sql += " ORDER BY " + _sortOrder.GetSQL(_binding);
                if (hasSuffix) sql += _conn.GetPageSuffix(_start, _count);

                // When the database handles the offset via LIMIT/OFFSET there is no need
                // for PerformFetch to skip rows client-side.
                var results = PerformFetch(sql, hasSuffix ? 0 : _start);

                if (_returnCount)
                {
                    results.TotalRowCountValid = true;
                    results.TotalRowCount      = _conn.QueryCount(_object, _term, _concreteTypes, _fieldSubset, _concreteTypeFieldSubsets);
                }
                return results;
            }

            // ── Core fetch ────────────────────────────────────────────────────────

            protected ObjectCollection PerformFetch(string sql, int firstSignificant)
            {
                var results = new ObjectCollection { StartRecord = _start, PageSize = _count };
                var cmd     = _conn.CreateCommand(sql);
                if (_conn._transactionDepth > 0) cmd.SetTransaction(_conn._transaction);
                BindQueryParameters(cmd);

                try
                {
                    using var reader = cmd.ExecuteReader();
                    int scan = 0, added = 0;
                    while (reader.Read())
                    {
                        if (scan >= firstSignificant)
                        {
                            DataObject newObj;
                            if (_concreteTypes == null)
                            {
                                newObj = _conn.Create(_object.GetType(), null, true);
                                newObj.SetTarget(_conn);
                                _conn.FetchRow(newObj, reader, _binding, _fieldBindingSubset, false);
                                if (!_fieldSubsetProvided && _object.IsExplicitDefaultFieldSubset())
                                    newObj.SetDefaultFieldSubset(_fieldSubset);
                            }
                            else
                            {
                                newObj = _conn.FetchRow(reader, _binding, _fieldBindingSubset, false);
                            }
                            if (newObj != null)
                            {
                                results.AddTail(newObj);
                                newObj.PostFetch();
                                added++;
                            }
                        }
                        scan++;
                        if (_count > 0 && added >= _count)
                        {
                            results.IsMoreData = reader.Read();
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    string dbg = _conn.FormatDebugInfo(_object, _binding, _term);
                    throw new PersistenceException($"Query failed SQL: {sql}{dbg}", e);
                }
                return results;
            }

            protected virtual void BindQueryParameters(CommandBase cmd)
            {
                if (_term != null)
                    _conn.BindQueryParameters(_object, _binding, cmd, _term);
                _conn.BindFunctionParameters(_object, _binding, cmd, null, null);
            }

            // ── EXISTS sub-query ──────────────────────────────────────────────────

            internal QueryFragment GenerateExistsSQLQuery(string outerAlias, string outerFieldName, TField linkField, ref int termNumber)
            {
                string stub = BuildQuerySQL(_count > 0 ? _start + _count + 1 : 0);
                int localTerm = termNumber;
                string where = "";
                if (_term != null)
                    where = " WHERE " + _term.GetSQL(_binding, ref localTerm).SQL;
                else
                    where = " WHERE ";

                if (where.Length > 7) where += " AND ";
                if (outerAlias.Length > 0) where += outerAlias + ".";
                where += _conn.QuoteName(outerFieldName) + "=";

                var linkBinding = _binding.GetFieldBinding(_object, linkField);
                if (linkBinding.MapNode.Alias.Length > 0)
                    where += linkBinding.MapNode.Alias + ".";
                where += _conn.QuoteName(linkBinding.Info.TargetName);

                termNumber = localTerm;
                return new QueryFragment(stub + where);
            }
        }
    }
}
