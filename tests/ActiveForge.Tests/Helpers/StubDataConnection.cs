using System;
using System.Collections.Generic;
using System.Reflection;
using ActiveForge;
using ActiveForge.Query;

namespace ActiveForge.Tests.Helpers
{
    /// <summary>
    /// Minimal concrete stub of <see cref="DataConnection"/> that supports enough
    /// dialect methods for <see cref="QueryTerm"/> construction in unit tests.
    /// All CRUD / query operations throw <see cref="NotImplementedException"/>.
    /// </summary>
    internal sealed class StubDataConnection : DataConnection
    {
        public override string GetParameterMark()            => "@";
        public override string GetLeftNameQuote()            => "[";
        public override string GetRightNameQuote()           => "]";
        public override string GetSourceNameSeparator()      => ".";
        public override string GetUpdateLock()               => "";
        public override bool   IsAutoIdentity()              => true;
        public override string GetStringConnectionOperator() => "+";
        public override string CreateConcatenateOperator(params string[] parts) => string.Join("+", parts);
        public override string GetGeneratorOperator(TargetFieldInfo info) => "";
        public override string PreInsertIdentityCommand(string sourceName) => "";
        public override string PostInsertIdentityCommand(string sourceName) => "";

        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        private bool _isOpen;
        public override bool IsOpen       => _isOpen;
        public override bool Connect()    { _isOpen = true;  return true; }
        public override bool Disconnect() { _isOpen = false; return true; }
        public override bool   Insert(Record obj)  => throw new NotImplementedException();
        public override bool   Delete(Record obj)  => throw new NotImplementedException();
        public override bool   Delete(Record obj, QueryTerm term) => throw new NotImplementedException();
        internal override void Delete(Record obj, QueryTerm term, Type[] concreteTypes) => throw new NotImplementedException();
        internal override FieldSubset Update(Record obj, RecordLock.UpdateOption option) => throw new NotImplementedException();
        internal override FieldSubset UpdateAll(Record obj) => throw new NotImplementedException();
        internal override FieldSubset UpdateChanged(Record obj) => throw new NotImplementedException();

        public override void ProcessActionQueue() { }
        public override void ClearActionQueue()   { }
        public override void QueueForInsert(Record obj) { }
        public override void QueueForUpdate(Record obj) { }
        public override void QueueForDelete(Record obj) { }
        public override void QueueForDelete(Record obj, QueryTerm term) { }

        public override bool Read(Record obj) => throw new NotImplementedException();
        public override bool Read(Record obj, FieldSubset fieldSubset) => throw new NotImplementedException();
        public override bool ReadForUpdate(Record obj, FieldSubset fieldSubset) => throw new NotImplementedException();

        public override bool QueryFirst(Record obj, QueryTerm term, SortOrder sortOrder, FieldSubset fs) => throw new NotImplementedException();
        public override bool QueryFirst(Record obj, QueryTerm term, SortOrder sortOrder, FieldSubset fs, BaseRecordParameterCollection p) => throw new NotImplementedException();

        public override int QueryCount(Record obj) => throw new NotImplementedException();
        public override int QueryCount(Record obj, QueryTerm term) => throw new NotImplementedException();
        public override int QueryCount(Record obj, QueryTerm term, Type[] t) => throw new NotImplementedException();
        public override int QueryCount(Record obj, QueryTerm term, Type[] t, FieldSubset s) => throw new NotImplementedException();

        public override RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sort, int pageSize, FieldSubset fs) => throw new NotImplementedException();
        public override RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs) => throw new NotImplementedException();
        public override RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs, Dictionary<Type, FieldSubset> d) => throw new NotImplementedException();

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sort, int pageSize, FieldSubset fs) => throw new NotImplementedException();
        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs) => throw new NotImplementedException();

        public override RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs) => throw new NotImplementedException();
        public override RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t) => throw new NotImplementedException();
        public override RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t, Dictionary<Type, FieldSubset> d) => throw new NotImplementedException();
        public override RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t, Dictionary<Type, FieldSubset> d, bool r) => throw new NotImplementedException();

        public override RecordCollection ExecSQL(Record obj, string sql) => throw new NotImplementedException();
        public override RecordCollection ExecSQL(Record obj, string sqlFormat, params object[] values) => throw new NotImplementedException();
        public override RecordCollection ExecSQL(Record obj, string sql, Dictionary<string, object> parameters) => throw new NotImplementedException();
        public override RecordCollection ExecSQL(Record obj, string sql, int start, int count) => throw new NotImplementedException();
        public override RecordCollection ExecSQL(Record obj, string sql, int start, int count, Dictionary<string, object> p) => throw new NotImplementedException();
        public override BaseReader       ExecSQL(string sql) => throw new NotImplementedException();
        public override BaseReader       ExecSQL(string sql, Dictionary<string, BaseCommand.Parameter> p) => throw new NotImplementedException();
        public override RecordCollection ExecStoredProcedure(Record obj, string spName, int start, int count, params Record.SPParameter[] sp) => throw new NotImplementedException();

        internal override QueryFragment GenerateExistsSQLQuery(Record obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets) => throw new NotImplementedException();

        public override RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache) => throw new NotImplementedException();
        public override RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache, Type[] t) => throw new NotImplementedException();
        public override RecordBinding GetObjectBinding(BaseRecord obj, bool targetExists, bool useCache, Type[] t, bool includeLookup) => throw new NotImplementedException();
        public override RecordBinding GetChangedObjectBinding(BaseRecord obj, BaseRecord changedObj) => throw new NotImplementedException();
        public override RecordBinding GetDynamicObjectBinding(BaseRecord obj, BaseReader reader) => throw new NotImplementedException();

        public override TargetFieldInfo       GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName) => throw new NotImplementedException();
        public override List<TargetFieldInfo> GetTargetFieldInfo(string sourceName) => throw new NotImplementedException();
        public override TargetFieldInfo       GetTargetFieldInfoFromCache(string sourceName, string targetFieldName) => null;
        public override void                  AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info) { }
        public override bool                  TableExists(BaseRecord obj) => throw new NotImplementedException();

        public override Type        MapType(Type generalization) => generalization;
        public override FieldSubset DefaultFieldSubset(Record rootObject) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(Record rootObject, FieldSubset.InitialState state) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, TField enclosed) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed, FieldSubset.InitialState state) => throw new NotImplementedException();

        public override BaseTransaction    BeginTransaction() => throw new NotImplementedException();
        public override BaseTransaction    BeginTransaction(System.Data.IsolationLevel level) => throw new NotImplementedException();
        public override void               CommitTransaction(BaseTransaction transaction) { }
        public override void               RollbackTransaction(BaseTransaction transaction) { }
        public override TransactionStates  TransactionState(BaseTransaction transaction) => TransactionStates.None;

        public override string GetValidationMessage(string key, string defaultValue) => defaultValue;
        public override string GetFieldDescription(FieldInfo fi, BaseRecord obj) => "";
        public override string GetDataObjectDescription(BaseRecord obj) => "";

        public override Record Create(Type type) => (Record)Activator.CreateInstance(type);
        public override Record Create(Type type, Record owner, bool isTemplate = false) => (Record)Activator.CreateInstance(type);
    }
}
