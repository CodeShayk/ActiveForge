using System;
using System.Collections.Generic;
using System.Reflection;
using Turquoise.ORM.Query;

namespace Turquoise.ORM
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

        // ── CRUD ──────────────────────────────────────────────────────────────────────

        public abstract bool Insert(DataObject obj);
        public abstract bool Delete(DataObject obj);
        public abstract bool Delete(DataObject obj, QueryTerm term);
        internal abstract void Delete(DataObject obj, QueryTerm term, Type[] concreteTypes);
        internal abstract FieldSubset Update(DataObject obj, DataObjectLock.UpdateOption option);
        internal abstract FieldSubset UpdateAll(DataObject obj);
        internal abstract FieldSubset UpdateChanged(DataObject obj);

        // ── Action queue ──────────────────────────────────────────────────────────────

        public abstract void ProcessActionQueue();
        public abstract void ClearActionQueue();
        public abstract void QueueForInsert(DataObject obj);
        public abstract void QueueForUpdate(DataObject obj);
        public abstract void QueueForDelete(DataObject obj);
        public abstract void QueueForDelete(DataObject obj, QueryTerm term);

        // ── Read / ReadForUpdate ──────────────────────────────────────────────────────

        public abstract bool Read(DataObject obj);
        public abstract bool Read(DataObject obj, FieldSubset fieldSubset);
        public abstract bool ReadForUpdate(DataObject obj, FieldSubset fieldSubset);

        // ── QueryFirst ────────────────────────────────────────────────────────────────

        public abstract bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset);
        public abstract bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fieldSubset, ObjectParameterCollectionBase objectParameters);

        // ── QueryCount ────────────────────────────────────────────────────────────────

        public abstract int QueryCount(DataObject obj);
        public abstract int QueryCount(DataObject obj, QueryTerm term);
        public abstract int QueryCount(DataObject obj, QueryTerm term, Type[] expectedTypes);
        public abstract int QueryCount(DataObject obj, QueryTerm term, Type[] expectedTypes, FieldSubset subsetIn);

        // ── QueryAll ──────────────────────────────────────────────────────────────────

        public abstract ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset);
        public abstract ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset);
        public abstract ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);

        public abstract IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset) where T : DataObject;
        public abstract IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset) where T : DataObject;

        // ── QueryPage ─────────────────────────────────────────────────────────────────

        public abstract ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset);
        public abstract ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes);
        public abstract ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);
        public abstract ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets, bool returnCountInfo);

        // ── ExecSQL ───────────────────────────────────────────────────────────────────

        public abstract ObjectCollection ExecSQL(DataObject obj, string sql);
        public abstract ObjectCollection ExecSQL(DataObject obj, string sqlFormat, params object[] values);
        public abstract ObjectCollection ExecSQL(DataObject obj, string sql, Dictionary<string, object> parameters);
        public abstract ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count);
        public abstract ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count, Dictionary<string, object> parameters);
        public abstract ReaderBase       ExecSQL(string sql);
        public abstract ReaderBase       ExecSQL(string sql, Dictionary<string, CommandBase.Parameter> parameters);
        public virtual  string           ExecSQLParameterName(string name) => GetParameterMark() + name;

        // ── Stored procedures ─────────────────────────────────────────────────────────

        public abstract ObjectCollection ExecStoredProcedure(DataObject obj, string spName, int start, int count, params DataObject.SPParameter[] spParameters);

        // ── Exists-sub-query support ──────────────────────────────────────────────────

        internal abstract QueryFragment GenerateExistsSQLQuery(DataObject obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets);

        // ── Binding ───────────────────────────────────────────────────────────────────

        public abstract ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache);
        public abstract ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] expectedTypes);
        public abstract ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] expectedTypes, bool includeLookupDataObjects);
        public abstract ObjectBinding GetChangedObjectBinding(ObjectBase obj, ObjectBase changedObj);
        public abstract ObjectBinding GetDynamicObjectBinding(ObjectBase obj, ReaderBase reader);

        // ── Schema / field info ───────────────────────────────────────────────────────

        public abstract TargetFieldInfo        GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName);
        public abstract List<TargetFieldInfo>  GetTargetFieldInfo(string sourceName);
        public abstract TargetFieldInfo        GetTargetFieldInfoFromCache(string sourceName, string targetFieldName);
        public abstract void                   AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info);
        public abstract bool                   TableExists(ObjectBase obj);

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

        /// <summary>Quotes an identifier with the dialect's left/right quote characters.</summary>
        public string QuoteName(string name) => GetLeftNameQuote() + name + GetRightNameQuote();

        // ── FieldSubset factories ─────────────────────────────────────────────────────

        public abstract Type       MapType(Type generalization);
        public abstract FieldSubset DefaultFieldSubset(DataObject rootObject);
        public abstract FieldSubset FieldSubset(DataObject rootObject, FieldSubset.InitialState state);
        public abstract FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, TField enclosed);
        public abstract FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed);
        public abstract FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed, FieldSubset.InitialState state);

        // ── Pre/post identity ─────────────────────────────────────────────────────────

        public abstract string PreInsertIdentityCommand(string sourceName);
        public abstract string PostInsertIdentityCommand(string sourceName);

        // ── Object creation helpers ───────────────────────────────────────────────────

        public abstract DataObject Create(Type type);
        public abstract DataObject Create(Type type, DataObject owner, bool isTemplate = false);

        // ── Timeout ───────────────────────────────────────────────────────────────────

        public virtual int GetTimeout() => 30;

        // ── Object templating ─────────────────────────────────────────────────────────

        public virtual bool IsObjectTemplatingEnabled => false;

        // ── Transaction support ───────────────────────────────────────────────────────

        public abstract TransactionBase BeginTransaction();
        public abstract TransactionBase BeginTransaction(System.Data.IsolationLevel level);
        public abstract void            CommitTransaction(TransactionBase transaction);
        public abstract void            RollbackTransaction(TransactionBase transaction);
        public abstract TransactionStates TransactionState(TransactionBase transaction);

        // ── Field descriptions ────────────────────────────────────────────────────────

        public abstract string GetValidationMessage(string key, string defaultValue);
        public abstract string GetFieldDescription(FieldInfo fi, ObjectBase obj);
        public abstract string GetDataObjectDescription(ObjectBase obj);
    }
}
