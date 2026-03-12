using System;
using System.Collections.Generic;
using System.Reflection;
using ActiveForge.Query;
using ActiveForge.Transactions;

namespace ActiveForge
{
    public enum TransactionStates { None, Active, Committed, RolledBack }

    /// <summary>
    /// Abstract interface for a database connection that provides full ORM CRUD,
    /// query, and schema-discovery services.
    /// </summary>
    [Serializable]
    public abstract class DataConnection
    {
        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        public abstract bool Connect();
        public abstract bool Disconnect();

        /// <summary>
        /// Returns <c>true</c> when the underlying physical connection is open.
        /// Overridden by <c>DBDataConnection</c> and <c>MongoDataConnection</c>.
        /// </summary>
        public virtual bool IsOpen => false;

        // ── Automatic lifecycle management ────────────────────────────────────────────

        /// <summary>
        /// Optional <see cref="IUnitOfWork"/> to use for automatic transaction management.
        /// When set, write operations (Insert, Update, Delete, …) automatically begin a
        /// transaction if one is not already active and commit (or roll back) it when the
        /// operation completes.  If the connection is not open when a write is attempted it
        /// is opened first and closed when the operation finishes.
        /// </summary>
        public IUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// Wraps a write operation with automatic connection open/close and transaction
        /// begin/commit/rollback, honouring the current <see cref="UnitOfWork"/> and the
        /// current <see cref="IsOpen"/> state.
        /// <para>
        /// When a <see cref="UnitOfWork"/> is present it owns the connection lifetime:
        /// <see cref="IUnitOfWork.CreateTransaction"/> opens the connection and
        /// <see cref="IUnitOfWork.Commit"/>/<see cref="IUnitOfWork.Rollback"/> close it.
        /// When no UoW is set the connection is opened and closed around this single call.
        /// </para>
        /// </summary>
        protected virtual T RunWrite<T>(Func<T> operation)
        {
            // Only self-manage the connection when there is no UnitOfWork.
            // When a UoW is present it opens the connection in CreateTransaction and
            // closes it after Commit/Rollback — we must not double-open or double-close.
            bool openedConn = !IsOpen && UnitOfWork == null;
            if (openedConn) Connect();

            bool startedTx = UnitOfWork != null && !UnitOfWork.InTransaction;
            if (startedTx) UnitOfWork.CreateTransaction();

            bool committed = false;
            try
            {
                T result = operation();
                if (startedTx)
                {
                    UnitOfWork.Commit();
                    committed = true;
                }
                return result;
            }
            catch (Exception primaryEx)
            {
                if (startedTx && !committed)
                {
                    try
                    {
                        UnitOfWork.Rollback();
                    }
                    catch (Exception rollbackEx)
                    {
                        throw new AggregateException(
                            "Transaction rollback failed after an operation failure.",
                            primaryEx, rollbackEx);
                    }
                }
                throw;
            }
            finally
            {
                if (openedConn) Disconnect();
            }
        }

        /// <summary>Void overload of <see cref="RunWrite{T}"/>.</summary>
        protected void RunWrite(Action operation)
            => RunWrite<object>(() => { operation(); return null; });

        /// <summary>
        /// Wraps a read operation with automatic connection open/close when no transaction
        /// is already active.  If the connection is already open (e.g. inside a
        /// <c>[Transaction]</c> scope) it is reused and left open on exit.
        /// </summary>
        protected virtual T RunRead<T>(Func<T> operation)
        {
            bool openedConn = !IsOpen;
            if (openedConn) Connect();
            try
            {
                return operation();
            }
            finally
            {
                if (openedConn) Disconnect();
            }
        }

        /// <summary>Void overload of <see cref="RunRead{T}"/>.</summary>
        protected void RunRead(Action operation)
            => RunRead<object>(() => { operation(); return null; });

        /// <summary>
        /// Called by <see cref="BaseUnitOfWork"/> after a transaction is successfully committed.
        /// Override in provider subclasses to sync any internal transaction-depth counters.
        /// </summary>
        protected virtual void OnUoWCommitted() { }

        /// <summary>
        /// Called by <see cref="BaseUnitOfWork"/> after a transaction is rolled back.
        /// Override in provider subclasses to sync internal state.
        /// </summary>
        protected virtual void OnUoWRolledBack() { }

        // Called by BaseUnitOfWork (same assembly) — routes to the protected hooks above.
        internal void NotifyTransactionCommitted()  => OnUoWCommitted();
        internal void NotifyTransactionRolledBack() => OnUoWRolledBack();

        // ── CRUD ──────────────────────────────────────────────────────────────────────

        public abstract bool Insert(Record obj);
        public abstract bool Delete(Record obj);
        public abstract bool Delete(Record obj, QueryTerm term);
        internal abstract void Delete(Record obj, QueryTerm term, Type[] concreteTypes);
        internal abstract FieldSubset Update(Record obj, RecordLock.UpdateOption option);
        internal abstract FieldSubset UpdateAll(Record obj);
        internal abstract FieldSubset UpdateChanged(Record obj);

        // ── Action queue ──────────────────────────────────────────────────────────────

        public abstract void ProcessActionQueue();
        public abstract void ClearActionQueue();
        public abstract void QueueForInsert(Record obj);
        public abstract void QueueForUpdate(Record obj);
        public abstract void QueueForDelete(Record obj);
        public abstract void QueueForDelete(Record obj, QueryTerm term);

        // ── Read / ReadForUpdate ──────────────────────────────────────────────────────

        public abstract bool Read(Record obj);
        public abstract bool Read(Record obj, FieldSubset fieldSubset);
        public abstract bool ReadForUpdate(Record obj, FieldSubset fieldSubset);

        // ── QueryFirst ────────────────────────────────────────────────────────────────

        public abstract bool QueryFirst(Record obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset);
        public abstract bool QueryFirst(Record obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset, BaseRecordParameterCollection objectParameters);

        // ── QueryCount ────────────────────────────────────────────────────────────────

        public abstract int QueryCount(Record obj);
        public abstract int QueryCount(Record obj, QueryTerm term);
        public abstract int QueryCount(Record obj, QueryTerm term, Type[] expectedTypes);
        public abstract int QueryCount(Record obj, QueryTerm term, Type[] expectedTypes, FieldSubset subsetIn);

        // ── QueryAll ──────────────────────────────────────────────────────────────────

        public abstract RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset);
        public abstract RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset);
        public abstract RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);

        public abstract IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset) where T : Record;
        public abstract IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset) where T : Record;

        // ── Query with join-type overrides (virtual; default ignores overrides) ────────

        /// <summary>
        /// Executes a QueryAll query with optional query-time join-type overrides.
        /// Overrides change the join type (INNER/LEFT OUTER) for specific embedded
        /// <see cref="Record"/> types without modifying the entity class.
        /// The default implementation ignores <paramref name="joinOverrides"/> and
        /// falls back to the standard <see cref="QueryAll(Record,QueryTerm,SortOrder,int,FieldSubset)"/>.
        /// </summary>
        public virtual RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
            => QueryAll(obj, term, sortOrder, pageSize, fieldSubset);

        /// <inheritdoc cref="QueryAll(Record,QueryTerm,SortOrder,int,FieldSubset,IReadOnlyList{JoinOverride})"/>
        public virtual IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides) where T : Record
            => LazyQueryAll<T>(obj, term, sortOrder, pageSize, fieldSubset);

        /// <inheritdoc cref="QueryAll(Record,QueryTerm,SortOrder,int,FieldSubset,IReadOnlyList{JoinOverride})"/>
        public virtual RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset);

        // ── QueryPage ─────────────────────────────────────────────────────────────────

        public abstract RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset);
        public abstract RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes);
        public abstract RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);
        public abstract RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets, bool returnCountInfo);

        // ── ExecSQL ───────────────────────────────────────────────────────────────────

        public abstract RecordCollection ExecSQL(Record obj, string sql);
        public abstract RecordCollection ExecSQL(Record obj, string sqlFormat, params object[] values);
        public abstract RecordCollection ExecSQL(Record obj, string sql, Dictionary<string, object> parameters);
        public abstract RecordCollection ExecSQL(Record obj, string sql, int start, int count);
        public abstract RecordCollection ExecSQL(Record obj, string sql, int start, int count, Dictionary<string, object> parameters);
        public abstract BaseReader       ExecSQL(string sql);
        public abstract BaseReader       ExecSQL(string sql, Dictionary<string, BaseCommand.Parameter> parameters);
        public virtual  string           ExecSQLParameterName(string name) => GetParameterMark() + name;

        // ── Stored procedures ─────────────────────────────────────────────────────────

        public abstract RecordCollection ExecStoredProcedure(Record obj, string spName, int start, int count, params Record.SPParameter[] spParameters);

        // ── Exists-sub-query support ──────────────────────────────────────────────────

        internal abstract QueryFragment GenerateExistsSQLQuery(Record obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);

        // ── Binding ───────────────────────────────────────────────────────────────────

        public abstract RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache);
        public abstract RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache, Type[] expectedTypes);
        public abstract RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache, Type[] expectedTypes, bool includeLookupDataObjects);
        public abstract RecordBinding GetChangedObjectBinding(BaseRecord obj, BaseRecord changedObj);
        public abstract RecordBinding GetDynamicObjectBinding(BaseRecord obj, BaseReader reader);

        // ── Schema / field info ───────────────────────────────────────────────────────

        public abstract TargetFieldInfo        GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName);
        public abstract List<TargetFieldInfo>  GetTargetFieldInfo(string sourceName);
        public abstract TargetFieldInfo        GetTargetFieldInfoFromCache(string sourceName, string targetFieldName);
        public abstract void                   AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info);
        public abstract bool                   TableExists(BaseRecord obj);

        // ── Dialect helpers ───────────────────────────────────────────────────────────

        public abstract string GetStringConnectionOperator();
        public abstract string CreateConcatenateOperator(params string[] parts);
        public abstract string GetParameterMark();
        public abstract string GetLeftNameQuote();
        public abstract string GetRightNameQuote();
        public abstract string GetSourceNameSeparator();
        public abstract string GetUpdateLock();
        public abstract bool   IsAutoIdentity();
        public abstract string GetGeneratorOperator(TargetFieldInfo info);

        /// <summary>
        /// Quotes an identifier with the dialect's left/right quote characters.
        /// Any embedded right-quote characters are escaped by doubling them (standard SQL).
        /// </summary>
        public string QuoteName(string name)
        {
            string rq      = GetRightNameQuote();
            string escaped = string.IsNullOrEmpty(rq) ? name : name.Replace(rq, rq + rq);
            return GetLeftNameQuote() + escaped + rq;
        }

        // ── FieldSubset factories ─────────────────────────────────────────────────────

        public abstract Type       MapType(Type generalization);
        public abstract FieldSubset DefaultFieldSubset(Record rootObject);
        public abstract FieldSubset FieldSubset(Record rootObject, FieldSubset.InitialState state);
        public abstract FieldSubset FieldSubset(Record rootObject, Record enclosing, TField enclosed);
        public abstract FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed);
        public abstract FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed, FieldSubset.InitialState state);

        // ── Pre/post identity ─────────────────────────────────────────────────────────

        public abstract string PreInsertIdentityCommand(string sourceName);
        public abstract string PostInsertIdentityCommand(string sourceName);

        // ── Object creation helpers ───────────────────────────────────────────────────

        public abstract Record Create(Type type);
        public abstract Record Create(Type type, Record owner, bool isTemplate = false);

        // ── Timeout ───────────────────────────────────────────────────────────────────

        public virtual int GetTimeout() => 30;

        // ── Object templating ─────────────────────────────────────────────────────────

        public virtual bool IsObjectTemplatingEnabled => false;

        // ── Transaction support ───────────────────────────────────────────────────────

        public abstract BaseTransaction BeginTransaction();
        public abstract BaseTransaction BeginTransaction(System.Data.IsolationLevel level);
        public abstract void            CommitTransaction(BaseTransaction transaction);
        public abstract void            RollbackTransaction(BaseTransaction transaction);
        public abstract TransactionStates TransactionState(BaseTransaction transaction);

        // ── Field descriptions ────────────────────────────────────────────────────────

        public abstract string GetValidationMessage(string key, string defaultValue);
        public abstract string GetFieldDescription(FieldInfo fi, BaseRecord obj);
        public abstract string GetDataObjectDescription(BaseRecord obj);
    }
}
