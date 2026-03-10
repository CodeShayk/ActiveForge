using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ActiveForge.Attributes;
using ActiveForge.Query;

namespace ActiveForge
{
    /// <summary>
    /// Holds the fully-resolved SQL stub cache and field-binding arrays for one Record type.
    /// Constructed once per type/connection combination and re-used across queries.
    /// </summary>
    public class RecordBinding
    {
        // ── Constructors ──────────────────────────────────────────────────────────────

        public RecordBinding()
        {
            DeleteSQL          = new List<DeleteSQLInfo>();
            DeleteSQLStub      = new List<DeleteSQLInfo>();
            InsertSQL          = new List<InsertSQLInfo>();
            ReadSQL            = "";
            ReadForUpdateSQL   = "";
            QuerySQL           = "";
            Identity           = null;
            Fields             = new List<FieldBinding>();
            UpdateFields       = new List<FieldBinding>();
            UpdateTableAliases = new List<string>();
            SourceName         = "";
            ObjectBindingMapRoot = null;
            AliasGenerator     = null;
        }

        public RecordBinding(RecordBase obj, DataConnection connection, bool targetExists, RecordBase changedObj, Type[] expectedTypes, FactoryBase factory)
            : this(obj, connection, targetExists, changedObj, expectedTypes, factory, false) { }

        public RecordBinding(RecordBase obj, DataConnection connection, bool targetExists, RecordBase changedObj, Type[] expectedTypes, FactoryBase factory, bool includeLookupDataObjects)
        {
            DeleteSQL          = new List<DeleteSQLInfo>();
            DeleteSQLStub      = new List<DeleteSQLInfo>();
            InsertSQL          = new List<InsertSQLInfo>();
            ReadSQL            = "";
            ReadForUpdateSQL   = "";
            QuerySQL           = "";
            AliasGenerator     = null;
            Identity           = null;
            Fields             = new List<FieldBinding>();
            UpdateFields       = new List<FieldBinding>();
            UpdateTableAliases = new List<string>();
            Class              = obj.GetType();

            var tableAttr = CustomAttributeCache.GetClassAttribute(Class, typeof(TableAttribute), false) as TableAttribute;
            if (tableAttr != null)
            {
                SourceName = tableAttr.SourceName;
            }
            else if (obj.GetSourceName().Length > 0)
            {
                SourceName = obj.GetSourceName();
            }
            else
            {
                SourceName = Class.Name;
            }

            var funcAttr = CustomAttributeCache.GetClassAttribute(Class, typeof(FunctionAttribute), false);
            if (funcAttr != null)
                Function = true;

            AliasGenerator       = ((DBDataConnection)connection).AliasGenerator;
            ObjectBindingMapRoot = new RecordBindingMapNode(Class, AliasGenerator, expectedTypes, factory, includeLookupDataObjects);

            if (expectedTypes != null)
                ConcreteClassDiagnosticFields = new List<FieldBinding>();

            ObjectBindingMapRoot.PopulateFieldArrays(obj, connection, targetExists, changedObj, Fields, UpdateFields, ConcreteClassDiagnosticFields);
            SetNeedFieldAliases(NeedFieldAliases());

            NameToBindingMap = new Dictionary<string, FieldBinding>();
            foreach (var fb in UpdateFields)
            {
                if (fb.Info.IsIdentity)
                    Identity = fb.Info;

                if (!NameToBindingMap.ContainsKey(fb.Info.FieldInfo.Name))
                    NameToBindingMap[fb.Info.FieldInfo.Name] = fb;
            }

            ObjectBindingMapRoot.PopulateUpdateTableAliasArray(UpdateTableAliases);
            PreRetrieveLookupDataObjectValues(connection, obj);

            if (expectedTypes != null)
            {
                PolymorphicTypeMap = new List<PolymorphicTypeMapEntry>();
                foreach (var expectedType in expectedTypes)
                    PolymorphicTypeMap.Add(new PolymorphicTypeMapEntry(expectedType, connection, (Record)obj));
            }
        }

        public RecordBinding(Record obj)
        {
            DeleteSQL          = new List<DeleteSQLInfo>();
            DeleteSQLStub      = new List<DeleteSQLInfo>();
            InsertSQL          = new List<InsertSQLInfo>();
            ReadSQL            = "";
            QuerySQL           = "";
            Identity           = null;
            Fields             = new List<FieldBinding>();
            UpdateFields       = new List<FieldBinding>();
            UpdateTableAliases = new List<string>();
            Class              = obj.GetType();
            SourceName         = "";
        }

        // ── Lookup pre-retrieval ───────────────────────────────────────────────────────

        public void PreRetrieveLookupDataObjectValues(DataConnection connection)
        {
            Record obj = (Record)Record.CreateDataObject(Class, connection);
            PreRetrieveLookupDataObjectValues(connection, obj);
        }

        protected void PreRetrieveLookupDataObjectValues(DataConnection connection, RecordBase obj)
        {
            ObjectBindingMapRoot.PreRetrieveLookupDataObjectValues(connection, obj);
        }

        // ── Alias / field utilities ───────────────────────────────────────────────────

        protected bool NeedFieldAliases()
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fb in Fields)
            {
                if (!found.Add(fb.Info.TargetName))
                    return true;
            }
            if (ConcreteClassDiagnosticFields != null)
            {
                foreach (var fb in ConcreteClassDiagnosticFields)
                {
                    if (!found.Add(fb.Info.TargetName))
                        return true;
                }
            }
            return false;
        }

        public bool IsFieldBindingInDataObject(FieldBinding fieldBinding)
        {
            var testNode = ObjectBindingMapRoot;
            do
            {
                if (testNode.Alias == fieldBinding.MapNode.Alias)
                    return true;
                testNode = testNode.Generalisation;
            }
            while (testNode != null);
            return false;
        }

        public List<RelationshipSpecification> GetRelationships()
            => ObjectBindingMapRoot.GetRelationships();

        public List<RelationshipSpecification> GetRelationships(bool includeBaseClasses)
            => ObjectBindingMapRoot.GetRelationships(includeBaseClasses);

        public void SetNeedFieldAliases(bool need)
        {
            if (!need)
            {
                foreach (var fb in Fields)
                    fb.Alias = "";
            }
        }

        public string AttributeToColumn(string attribute)
        {
            foreach (var fb in Fields)
            {
                if (fb.Info.FieldInfo.Name == attribute)
                    return fb.Alias.Length > 0 ? fb.Alias : fb.Info.TargetName;
            }
            return "";
        }

        public int GetColumnOrdinal(string name, ReaderBase reader, bool omitPK)
            => reader.ColumnOrdinal(name);

        public bool IsDBDerived()
            => ObjectBindingMapRoot.IsDBDerived;

        public bool UseAsPK(TargetFieldInfo info)
            => ObjectBindingMapRoot.UseAsPK(info);

        // ── Field binding lookup ──────────────────────────────────────────────────────

        public FieldBinding GetFieldBinding(Record target, TField field)
        {
            try
            {
                FieldInfo info = field.GetFieldInfo(target);

                // First pass: match by field handle
                var candidates = new List<FieldBinding>();
                foreach (var fb in Fields)
                {
                    if (fb.Info.FieldInfo.FieldHandle.Value == info.FieldHandle.Value)
                        candidates.Add(fb);
                }
                var result = IdentifyCorrectInstanceOfField(target, candidates);

                // Second pass: match by name
                if (result == null)
                {
                    candidates.Clear();
                    foreach (var fb in Fields)
                    {
                        if (fb.Info.FieldInfo.Name == info.Name)
                            candidates.Add(fb);
                    }
                    result = IdentifyCorrectInstanceOfField(target, candidates);
                }

                if (result == null)
                    throw new PersistenceException($"Field {info.Name} not found for Record {target.GetType().FullName}");

                return result;
            }
            catch (PersistenceException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new PersistenceException($"Exception finding binding for {target.GetType().Name}", e);
            }
        }

        /// <summary>
        /// When multiple bindings share the same field name (e.g. Description in lookup objects),
        /// picks the binding whose containing object matches the supplied target instance.
        /// </summary>
        protected FieldBinding IdentifyCorrectInstanceOfField(Record target, List<FieldBinding> candidates)
        {
            if (candidates.Count == 1)
                return candidates[0];

            if (candidates.Count == 0)
                return null;

            Debug.WriteLine($"Choosing between {candidates.Count} candidates");

            // Walk up to root (owner chain)
            Record root = target;
            while (root.GetOwner() != null)
                root = root.GetOwner();

            // First: find candidate whose containing object IS the target by reference
            var containingObjects = new Record[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                Record containing = GetContainingObjectForField(candidates[i], root);
                containingObjects[i] = containing;
                if (ReferenceEquals(containing, target))
                    return candidates[i];
            }

            // Second: match by unique identifier
            for (int i = 0; i < candidates.Count; i++)
            {
                if (containingObjects[i] != null &&
                    containingObjects[i].GetUniqueIdentifier() == target.GetUniqueIdentifier())
                    return candidates[i];
            }

            // Third: match by type
            for (int i = 0; i < candidates.Count; i++)
            {
                if (containingObjects[i] != null &&
                    containingObjects[i].GetType() == root.GetType())
                    return candidates[i];
            }

            return null;
        }

        public Record GetContainingObjectForField(FieldBinding targetFieldBinding, Record rootObject)
            => ObjectBindingMapRoot.GetContainingObjectForField(this, targetFieldBinding, rootObject);

        public string GetPathForField(FieldBinding targetFieldBinding)
        {
            var path = new Stack<string>(5);
            ObjectBindingMapRoot.ConstructPathToField(targetFieldBinding, path);
            string result = "";
            while (path.Count > 0)
                result += "\\" + path.Pop();
            return result;
        }

        public TField GetFieldFromPath(Record obj, string path)
        {
            TField field = null;
            string remaining = path;
            Record current = obj;

            while (field == null)
            {
                var meta = RecordMetaDataCache.GetTypeMetaData(current.GetType());
                remaining = remaining.Substring(1); // strip leading '\'
                int slash = remaining.IndexOf('\\');
                string component;
                if (slash >= 0)
                {
                    component = remaining.Substring(0, slash);
                    remaining = remaining.Substring(slash);
                }
                else
                {
                    component = remaining;
                    remaining = "";
                }

                if (remaining.Length > 0)
                {
                    Record next = null;
                    foreach (var entry in meta.DataObjects)
                    {
                        if (entry.Name == component)
                        {
                            next = (Record)entry.FieldInfo.GetValue(current);
                            break;
                        }
                    }
                    current = next ?? throw new PersistenceException("No contained object matching path: " + component);
                }
                else
                {
                    foreach (var entry in meta.TFields)
                    {
                        if (entry.Name == component)
                        {
                            field = (TField)entry.FieldInfo.GetValue(current);
                            break;
                        }
                    }
                    if (field == null)
                        throw new PersistenceException("No TField matching path: " + component);
                }
            }
            return field;
        }

        public Record GetContainerFromPath(Record obj, string path)
        {
            Record container = null;
            string remaining = path;
            Record current = obj;

            while (container == null)
            {
                var meta = RecordMetaDataCache.GetTypeMetaData(current.GetType());
                remaining = remaining.Substring(1); // strip leading '\'
                int slash = remaining.IndexOf('\\');
                string component;
                if (slash >= 0)
                {
                    component = remaining.Substring(0, slash);
                    remaining = remaining.Substring(slash);
                }
                else
                {
                    component = remaining;
                    remaining = "";
                }

                if (remaining.Length > 0)
                {
                    Record next = null;
                    foreach (var entry in meta.DataObjects)
                    {
                        if (entry.Name == component)
                        {
                            next = (Record)entry.FieldInfo.GetValue(current);
                            break;
                        }
                    }
                    current = next ?? throw new PersistenceException("No contained object matching path: " + component);
                }
                else
                {
                    container = current;
                }
            }
            return container;
        }

        // ── Row fetch ─────────────────────────────────────────────────────────────────

        /// <summary>Polymorphic row fetch: determines the concrete type from diagnostic fields, then fetches into the matching cached instance.</summary>
        public Record FetchRowValues(List<FieldBinding> fieldBindings, FieldFetcher fetcher, ReaderBase reader, bool omitPK, bool shallow, DBDataConnection connection, int depth)
        {
            Type polymorphicType = null;
            foreach (var binding in ConcreteClassDiagnosticFields)
            {
                object val = connection.FetchValue(reader, binding);
                if (val != null)
                {
                    polymorphicType = binding.MapNode.Class;
                    break;
                }
            }

            if (polymorphicType == null)
                return null;

            foreach (var entry in PolymorphicTypeMap)
            {
                if (entry.DataObjectType == polymorphicType)
                {
                    ObjectBindingMapRoot.FetchPolymorphicRowValues(fieldBindings, fetcher, entry.Object, entry.DataObjectType, reader, this, omitPK, omitPK, depth);
                    if (!entry.Object.IsNull("ID"))
                    {
                        Record result = entry.Object;
                        entry.RenewDataObject();
                        return result;
                    }
                }
            }
            return null;
        }

        /// <summary>Non-polymorphic row fetch: fetches into a known concrete Record instance.</summary>
        public void FetchRowValues(List<FieldBinding> fieldBindings, FieldFetcher fetcher, Record obj, ReaderBase reader, bool omitPK, bool shallow, int depth)
        {
            if (ObjectBindingMapRoot != null)
            {
                ObjectBindingMapRoot.FetchRowValues(fieldBindings, fetcher, obj, reader, this, omitPK, omitPK, shallow, depth);
            }
            else
            {
                // Dynamic data objects
                foreach (var fb in fieldBindings)
                    fetcher(obj, reader, fb, this, omitPK, omitPK);
            }
        }

        // ── Join specifications ───────────────────────────────────────────────────────

        public void GetPolymorphicJoinSpecifications(ref List<JoinSpecification> specs, bool forUpdate, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            if (ObjectBindingMapRoot != null)
            {
                ObjectBindingMapRoot.GetGeneralizationJoinSpecifications(ref specs);
                // Polymorphic joins are included in GetJoinSpecifications via the PolymorphicSpecialisations walk;
                // pass a null fieldSubset to include all.
                ObjectBindingMapRoot.GetJoinSpecifications(ref specs, null, forUpdate);
            }
        }

        public void GetJoinSpecifications(ref List<JoinSpecification> specs, FieldSubset fieldSubset, bool forUpdate)
        {
            if (ObjectBindingMapRoot != null)
            {
                ObjectBindingMapRoot.GetJoinSpecifications(ref specs, fieldSubset, forUpdate);
            }
            else
            {
                Debug.WriteLine($"Missing ObjectBindingMapRoot for {Class?.Name}");
            }
        }

        // ── Alias / source helpers ────────────────────────────────────────────────────

        public string GetRootAlias()
            => ObjectBindingMapRoot?.Alias ?? "";

        public string GetMostGeneralAlias()
            => ObjectBindingMapRoot?.GetMostGeneralAlias() ?? "";

        public string AliasToSourceName(string alias)
            => ObjectBindingMapRoot.AliasToSourceName(alias);

        // ── Field set helpers ─────────────────────────────────────────────────────────

        public int GetLargeColumnCount()
        {
            int count = 0;
            foreach (var fb in Fields)
            {
                if (fb.Info.IsLarge)
                    count++;
            }
            return count;
        }

        public List<Record> GetJoinedObjects(Record obj)
        {
            var result = new List<Record>();
            var current = ObjectBindingMapRoot;
            while (current != null)
            {
                var rels = current.GetRelationships();
                if (rels != null)
                {
                    foreach (var rel in rels)
                        result.Add((Record)rel.ObjectTargetFieldInfo.GetValue(obj));
                }
                current = current.Generalisation;
            }
            return result;
        }

        /// <summary>Restricts a flat field binding list to only the bindings included in a FieldSubset.</summary>
        public List<FieldBinding> FieldBindingSubset(FieldSubset fieldSubset, List<FieldBinding> fieldBindings)
        {
            if (fieldSubset.AllIncluded())
                return fieldBindings;

            var result = new List<FieldBinding>();
            var path = new Stack<string>(5);
            foreach (var fb in fieldBindings)
            {
                if (!fb.Translation)
                {
                    path.Clear();
                    ObjectBindingMapRoot.ConstructPathToField(fb, path);
                    if (fieldSubset.IncludesFieldPath(path))
                        result.Add(fb);
                }
            }
            return result;
        }

        /// <summary>Restricts a flat field binding list by a per-type polymorphic field subset map.</summary>
        public List<FieldBinding> FieldBindingSubset(Dictionary<Type, FieldSubset> polymorphicSubsets, List<FieldBinding> fieldBindings)
        {
            bool allIncluded = true;
            foreach (var kvp in polymorphicSubsets)
            {
                if (!kvp.Value.AllIncluded()) { allIncluded = false; break; }
            }

            if (allIncluded)
                return fieldBindings;

            var result = new List<FieldBinding>();
            var path = new Stack<string>(5);
            foreach (var kvp in polymorphicSubsets)
            {
                var subset = kvp.Value;
                foreach (var fb in fieldBindings)
                {
                    if (!fb.Translation && !result.Contains(fb))
                    {
                        path.Clear();
                        ObjectBindingMapRoot.ConstructPathToField(fb, path);
                        if (subset.IncludesFieldPath(path))
                            result.Add(fb);
                    }
                }
            }
            return result;
        }

        public QueryFragment GetPolymorphicRestrictionSQL()
        {
            if (ConcreteClassDiagnosticFields == null)
                return new QueryFragment("");

            var sb = new System.Text.StringBuilder();
            foreach (var fb in ConcreteClassDiagnosticFields)
            {
                if (sb.Length > 0) sb.Append(" OR ");
                if (fb.MapNode.Alias.Length > 0)
                {
                    sb.Append(fb.MapNode.Alias);
                    sb.Append('.');
                }
                sb.Append(fb.Info.TargetName);
                sb.Append(" is not null");
            }
            return new QueryFragment(sb.ToString());
        }

        // ── Fields ────────────────────────────────────────────────────────────────────

        protected RecordBindingMapNode            ObjectBindingMapRoot;
        protected AliasGenerator                  AliasGenerator;

        public string                             SourceName;
        public bool                               Function = false;
        public Type                               Class;
        public List<FieldBinding>                 Fields;
        public List<FieldBinding>                 ConcreteClassDiagnosticFields;
        public List<FieldBinding>                 UpdateFields;

        /// <summary>
        /// Table aliases in the order they must be inserted/updated (root table first,
        /// then generalisation tables in hierarchy order).
        /// </summary>
        public List<string>                       UpdateTableAliases;

        public TargetFieldInfo                    Identity;
        public Dictionary<string, FieldBinding>   NameToBindingMap;

        internal List<InsertSQLInfo>              InsertSQL;
        internal List<DeleteSQLInfo>              DeleteSQL;
        internal List<DeleteSQLInfo>              DeleteSQLStub;

        public List<PolymorphicTypeMapEntry>      PolymorphicTypeMap;

        public string                             ReadSQL;
        public string                             ReadForUpdateSQL;
        public string                             QuerySQL;

        // ── SQL info inner types ──────────────────────────────────────────────────────

        /// <summary>Cached INSERT SQL for one table in the object's class hierarchy.</summary>
        public class InsertSQLInfo
        {
            public string SQL;
            public string TableAlias;
            public string SourceName;
            public bool   IsBaseTable;
        }

        /// <summary>Cached DELETE SQL for one table in the object's class hierarchy.</summary>
        public class DeleteSQLInfo
        {
            public string SQL;
            public string TableAlias;
            public string SourceName;
        }

        // ── Polymorphic type map entry ────────────────────────────────────────────────

        public class PolymorphicTypeMapEntry
        {
            public PolymorphicTypeMapEntry(Type type, DataConnection connection, Record baseDataObject)
            {
                Connection      = connection;
                DataObjectType  = type;
                _baseDataObject = baseDataObject;
                RenewDataObject();
            }

            public void RenewDataObject()
            {
                Object = (Record)Record.CreateDataObject(DataObjectType, Connection, _baseDataObject);
            }

            private readonly Record    _baseDataObject;
            private readonly DataConnection Connection;
            public  Type                   DataObjectType;
            public  Record             Object;
        }
    }
}
