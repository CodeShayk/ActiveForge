using System;
using System.Collections.Generic;
using System.Reflection;
using Turquoise.ORM;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Tests.Helpers
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

        // ── Stubs for everything else ─────────────────────────────────────────────────

        public override bool   Connect()    => true;
        public override bool   Disconnect() => true;
        public override bool   Insert(DataObject obj)  => throw new NotImplementedException();
        public override bool   Delete(DataObject obj)  => throw new NotImplementedException();
        public override bool   Delete(DataObject obj, QueryTerm term) => throw new NotImplementedException();
        internal override void Delete(DataObject obj, QueryTerm term, Type[] concreteTypes) => throw new NotImplementedException();
        internal override FieldSubset Update(DataObject obj, DataObjectLock.UpdateOption option) => throw new NotImplementedException();
        internal override FieldSubset UpdateAll(DataObject obj) => throw new NotImplementedException();
        internal override FieldSubset UpdateChanged(DataObject obj) => throw new NotImplementedException();

        public override void ProcessActionQueue() { }
        public override void ClearActionQueue()   { }
        public override void QueueForInsert(DataObject obj) { }
        public override void QueueForUpdate(DataObject obj) { }
        public override void QueueForDelete(DataObject obj) { }
        public override void QueueForDelete(DataObject obj, QueryTerm term) { }

        public override bool Read(DataObject obj) => throw new NotImplementedException();
        public override bool Read(DataObject obj, FieldSubset fieldSubset) => throw new NotImplementedException();
        public override bool ReadForUpdate(DataObject obj, FieldSubset fieldSubset) => throw new NotImplementedException();

        public override bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fs) => throw new NotImplementedException();
        public override bool QueryFirst(DataObject obj, QueryTerm term, SortOrder sortOrder, FieldSubset fs, ObjectParameterCollectionBase p) => throw new NotImplementedException();

        public override int QueryCount(DataObject obj) => throw new NotImplementedException();
        public override int QueryCount(DataObject obj, QueryTerm term) => throw new NotImplementedException();
        public override int QueryCount(DataObject obj, QueryTerm term, Type[] t) => throw new NotImplementedException();
        public override int QueryCount(DataObject obj, QueryTerm term, Type[] t, FieldSubset s) => throw new NotImplementedException();

        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sort, int pageSize, FieldSubset fs) => throw new NotImplementedException();
        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs) => throw new NotImplementedException();
        public override ObjectCollection QueryAll(DataObject obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs, Dictionary<Type, FieldSubset> d) => throw new NotImplementedException();

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sort, int pageSize, FieldSubset fs) => throw new NotImplementedException();
        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sort, int pageSize, Type[] t, FieldSubset fs) => throw new NotImplementedException();

        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs) => throw new NotImplementedException();
        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t) => throw new NotImplementedException();
        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t, Dictionary<Type, FieldSubset> d) => throw new NotImplementedException();
        public override ObjectCollection QueryPage(DataObject obj, QueryTerm term, SortOrder sort, int start, int count, FieldSubset fs, Type[] t, Dictionary<Type, FieldSubset> d, bool r) => throw new NotImplementedException();

        public override ObjectCollection ExecSQL(DataObject obj, string sql) => throw new NotImplementedException();
        public override ObjectCollection ExecSQL(DataObject obj, string sqlFormat, params object[] values) => throw new NotImplementedException();
        public override ObjectCollection ExecSQL(DataObject obj, string sql, Dictionary<string, object> parameters) => throw new NotImplementedException();
        public override ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count) => throw new NotImplementedException();
        public override ObjectCollection ExecSQL(DataObject obj, string sql, int start, int count, Dictionary<string, object> p) => throw new NotImplementedException();
        public override ReaderBase       ExecSQL(string sql) => throw new NotImplementedException();
        public override ReaderBase       ExecSQL(string sql, Dictionary<string, CommandBase.Parameter> p) => throw new NotImplementedException();
        public override ObjectCollection ExecStoredProcedure(DataObject obj, string spName, int start, int count, params DataObject.SPParameter[] sp) => throw new NotImplementedException();

        internal override QueryFragment GenerateExistsSQLQuery(DataObject obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets) => throw new NotImplementedException();

        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache) => throw new NotImplementedException();
        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] t) => throw new NotImplementedException();
        public override ObjectBinding GetObjectBinding(ObjectBase obj, bool targetExists, bool useCache, Type[] t, bool includeLookup) => throw new NotImplementedException();
        public override ObjectBinding GetChangedObjectBinding(ObjectBase obj, ObjectBase changedObj) => throw new NotImplementedException();
        public override ObjectBinding GetDynamicObjectBinding(ObjectBase obj, ReaderBase reader) => throw new NotImplementedException();

        public override TargetFieldInfo       GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName) => throw new NotImplementedException();
        public override List<TargetFieldInfo> GetTargetFieldInfo(string sourceName) => throw new NotImplementedException();
        public override TargetFieldInfo       GetTargetFieldInfoFromCache(string sourceName, string targetFieldName) => null;
        public override void                  AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info) { }
        public override bool                  TableExists(ObjectBase obj) => throw new NotImplementedException();

        public override Type        MapType(Type generalization) => generalization;
        public override FieldSubset DefaultFieldSubset(DataObject rootObject) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(DataObject rootObject, FieldSubset.InitialState state) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, TField enclosed) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed) => throw new NotImplementedException();
        public override FieldSubset FieldSubset(DataObject rootObject, DataObject enclosing, DataObject enclosed, FieldSubset.InitialState state) => throw new NotImplementedException();

        public override TransactionBase    BeginTransaction() => throw new NotImplementedException();
        public override TransactionBase    BeginTransaction(System.Data.IsolationLevel level) => throw new NotImplementedException();
        public override void               CommitTransaction(TransactionBase transaction) { }
        public override void               RollbackTransaction(TransactionBase transaction) { }
        public override TransactionStates  TransactionState(TransactionBase transaction) => TransactionStates.None;

        public override string GetValidationMessage(string key, string defaultValue) => defaultValue;
        public override string GetFieldDescription(FieldInfo fi, ObjectBase obj) => "";
        public override string GetDataObjectDescription(ObjectBase obj) => "";

        public override DataObject Create(Type type) => (DataObject)Activator.CreateInstance(type);
        public override DataObject Create(Type type, DataObject owner, bool isTemplate = false) => (DataObject)Activator.CreateInstance(type);
    }
}
