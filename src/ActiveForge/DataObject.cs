using System;
using System.Collections.Generic;
using System.Reflection;
using ActiveForge.Query;

namespace ActiveForge
{
    /// <summary>
    /// Base class for all database-persisted objects (Active Record pattern).
    /// Provides CRUD delegation, query methods, field initialisation, and
    /// embedded object support. Subclasses declare public <see cref="TField"/>
    /// fields and optionally nested <see cref="DataObject"/> fields.
    /// </summary>
    [Serializable]
    public class DataObject : ObjectBase
    {
        // ── Backing state ─────────────────────────────────────────────────────────

        private   DataConnection _innerTarget;
        private   Guid           _uniqueIdentifier = Guid.NewGuid();
        protected bool           Loaded;
        protected FieldSubset    DefaultSubset;
        protected DataObject     Owner;

        // ── Target propagates connection to embedded objects ───────────────────────

        protected DataConnection Target
        {
            get => _innerTarget;
            set
            {
                _innerTarget = value;
                var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
                foreach (var entry in meta.DataObjects)
                {
                    var embedded = entry.FieldInfo.GetValue(this) as DataObject;
                    embedded?.SetTarget(value);
                }
            }
        }

        // ── Constructors ──────────────────────────────────────────────────────────

        public DataObject()
        {
            CreateTFields();
        }

        public DataObject(DataConnection target)
        {
            _innerTarget = target;
            CreateTFields();
            CreateEmbeddedDataObjects();
            InitializeFields();
        }

        public DataObject(DataConnection target, DataObject copyFrom)
        {
            _innerTarget = target;
            CreateTFields();
            CreateEmbeddedDataObjects(copyFrom);
            InitializeFields();
            CopyDataObjectState(copyFrom);
        }

        /// <summary>Override to set any dynamic initial field values after construction.</summary>
        protected virtual void InitializeFields() { }

        // ── Field / embedded object initialisation ────────────────────────────────

        private void CreateTFields()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var field = TField.Create(entry.FieldInfo.FieldType, this);
                if (field == null) continue;

                var defVal = CustomAttributeCache.GetFieldAttribute(entry.FieldInfo,
                                 typeof(Attributes.DefaultValueAttribute), false)
                             as Attributes.DefaultValueAttribute;
                if (defVal != null) field.SetValue(defVal.GetDefaultValue());

                entry.FieldInfo.SetValue(this, field);
            }
        }

        private void CreateEmbeddedDataObjects()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.DataObjects)
            {
                Type specialization = _innerTarget != null
                    ? _innerTarget.MapType(entry.FieldInfo.FieldType)
                    : entry.FieldInfo.FieldType;

                var newObj = (DataObject)Activator.CreateInstance(specialization);
                newObj.Owner        = this;
                newObj._innerTarget = _innerTarget;
                entry.FieldInfo.SetValue(this, newObj);
            }
        }

        private void CreateEmbeddedDataObjects(DataObject copyFrom)
        {
            var meta           = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            Type sourceType    = copyFrom?.GetType();

            foreach (var entry in meta.DataObjects)
            {
                Type specialization = _innerTarget != null
                    ? _innerTarget.MapType(entry.FieldInfo.FieldType)
                    : entry.FieldInfo.FieldType;

                DataObject embeddedSource = null;
                if (copyFrom != null)
                {
                    Type declaring = entry.FieldInfo.DeclaringType;
                    if (sourceType == declaring || sourceType.IsSubclassOf(declaring))
                        embeddedSource = entry.FieldInfo.GetValue(copyFrom) as DataObject;
                }

                DataObject newObj = _innerTarget != null
                    ? _innerTarget.Create(specialization, embeddedSource)
                    : (DataObject)Activator.CreateInstance(specialization);

                newObj.Owner        = this;
                newObj._innerTarget = _innerTarget;
                entry.FieldInfo.SetValue(this, newObj);
            }
        }

        internal virtual void CopyDataObjectState(DataObject copyFrom)
        {
            CopyFrom(copyFrom, Target);
        }

        // ── Public surface ────────────────────────────────────────────────────────

        public void SetTarget(DataConnection target) { Target = target; }
        public DataConnection GetConnection()        => _innerTarget;

        protected DataConnection GetDBConnection()   => _innerTarget;

        public void SetOwner(DataObject owner)       { Owner = owner; }
        public DataObject GetOwner()                 => Owner;

        // ── Loaded flag ───────────────────────────────────────────────────────────

        public virtual bool IsLoaded()    => Loaded;
        public virtual void SetLoaded(bool value) { Loaded = value; }

        /// <summary>
        /// Propagates the loaded flag upward from embedded objects.
        /// Called internally after a fetch row operation.
        /// </summary>
        public virtual void SetLoaded()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.DataObjects)
            {
                var embedded = entry.FieldInfo.GetValue(this) as DataObject;
                if (embedded != null)
                {
                    embedded.SetLoaded();
                    Loaded |= embedded.IsLoaded();
                }
            }
        }

        // ── PostFetch ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Called after a row has been fetched from the database.
        /// Recurses into embedded objects that have a non-null identity.
        /// </summary>
        public virtual void PostFetch()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.DataObjects)
            {
                var embedded = entry.FieldInfo.GetValue(this) as DataObject;
                if (embedded is IdentDataObject ido && !ido.ID.IsNull())
                    embedded.PostFetch();
            }
        }

        /// <summary>
        /// Extension point for post-row-fetch processing (e.g. translations).
        /// No-op in this port.
        /// </summary>
        public virtual void PerformPostFetchProcesses() { }

        // ── Factory helper ────────────────────────────────────────────────────────

        /// <summary>Creates a new DataObject of this type using the current connection.</summary>
        public virtual DataObject Create()
            => _innerTarget != null ? _innerTarget.Create(GetType()) : (DataObject)Activator.CreateInstance(GetType());

        public static DataObject CreateDataObject(Type objectType, DataConnection target)
            => target.Create(objectType);

        public static DataObject CreateDataObject(Type objectType, DataConnection target, DataObject copyFrom)
            => target.Create(objectType, copyFrom);

        public static T CreateDataObject<T>(DataConnection target) where T : DataObject
            => (T)target.Create(typeof(T));

        public static T CreateDataObject<T>(DataConnection target, DataObject copyFrom) where T : DataObject
            => (T)target.Create(typeof(T), copyFrom);

        /// <summary>Creates a new DataObject of the given type using this object's connection.</summary>
        public ObjectBase CreateDataObject(Type wantedType) => CreateDataObject(wantedType, Target);

        public override ObjectBase CreateCompatibleObject()
        {
            if (_innerTarget != null) return CreateDataObject(GetType(), _innerTarget);
            return base.CreateCompatibleObject();
        }

        // ── Binding ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the ObjectBinding for this object against the given connection.
        /// </summary>
        public ObjectBinding GetBinding(DataConnection conn, bool targetExists, bool useCache,
            Type[] expectedTypes = null, bool includeLookups = false)
            => conn.GetObjectBinding(this, targetExists, useCache, expectedTypes, includeLookups);

        public bool ShouldIncludeLookupDataObjectsInBinding(QueryTerm term, SortOrder sortOrder)
        {
            if (term != null && term.IncludesLookupDataObject(this)) return true;
            return false;
        }

        // ── FieldSubset factories ─────────────────────────────────────────────────

        public override void SetDefaultFieldSubset(FieldSubset subset)
            { DefaultSubset = subset; }

        public override void SetDefaultFieldSubset(FieldSubset.InitialState state)
            { DefaultSubset = FieldSubset(state); }

        public override FieldSubset DefaultFieldSubset()
            => DefaultSubset ?? GetDBConnection()?.DefaultFieldSubset(this);

        public override bool IsExplicitDefaultFieldSubset() => DefaultSubset != null;

        public override FieldSubset FieldSubset(FieldSubset.InitialState includeAll)
            => GetDBConnection()?.FieldSubset(this, includeAll);

        public override FieldSubset FieldSubset(DataObject enclosing, TField enclosed)
            => GetDBConnection()?.FieldSubset(this, enclosing, enclosed);

        public override FieldSubset FieldSubset(DataObject enclosing, DataObject enclosed)
            => GetDBConnection()?.FieldSubset(this, enclosing, enclosed);

        public override FieldSubset FieldSubset(DataObject enclosing, DataObject enclosed, FieldSubset.InitialState state)
            => GetDBConnection()?.FieldSubset(this, enclosing, enclosed, state);

        // ── GetFieldInfo helper (used by TField.GetFieldInfo) ─────────────────────

        public FieldInfo GetFieldInfo(TField field)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
                if (ReferenceEquals(field, entry.FieldInfo.GetValue(this)))
                    return entry.FieldInfo;
            return null;
        }

        // ── CopyFrom ──────────────────────────────────────────────────────────────

        public void CopyFrom(DataObject source)
            => CopyFrom(source, source.GetConnection());

        protected virtual void CopyFrom(DataObject source, DataConnection connection)
        {
            if (connection != null && _innerTarget == null)
                _innerTarget = connection;

            ObjectBinding binding = GetBinding(Target ?? connection, true, true, null, true);
            Type sourceType       = source.GetType();
            bool sameType         = sourceType == GetType();

            SetLoaded(source.Loaded);
            DefaultSubset = source.DefaultSubset;

            foreach (var fb in binding.Fields)
            {
                var info = fb.Info;
                if (binding.IsFieldBindingInDataObject(fb))
                {
                    if (sameType || sourceType == info.FieldInfo?.DeclaringType
                        || (info.FieldInfo?.DeclaringType != null && sourceType.IsSubclassOf(info.FieldInfo.DeclaringType)))
                    {
                        object srcVal = info.FieldInfo?.GetValue(source);
                        if (srcVal is TField srcField)
                        {
                            var destField = info.FieldInfo.GetValue(this) as TField;
                            if (destField == null)
                            {
                                destField = TField.Create(info.FieldInfo.FieldType, this);
                                info.FieldInfo.SetValue(this, destField);
                            }
                            destField.CopyFrom(srcField);
                        }
                        else if (srcVal != null)
                        {
                            info.FieldInfo?.SetValue(this, srcVal);
                        }
                    }
                }
            }

            // Deep copy embedded objects
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.DataObjects)
            {
                Type declaring = entry.FieldInfo.DeclaringType;
                if (!sameType && sourceType != declaring && !sourceType.IsSubclassOf(declaring)) continue;

                var srcEmbedded  = entry.FieldInfo.GetValue(source)  as DataObject;
                var destEmbedded = entry.FieldInfo.GetValue(this)     as DataObject;
                if (srcEmbedded == null) continue;

                if (destEmbedded == null || destEmbedded.GetType() != srcEmbedded.GetType())
                {
                    destEmbedded = _innerTarget != null
                        ? _innerTarget.Create(srcEmbedded.GetType())
                        : (DataObject)Activator.CreateInstance(srcEmbedded.GetType());
                    destEmbedded.Owner        = this;
                    destEmbedded._innerTarget = _innerTarget;
                    entry.FieldInfo.SetValue(this, destEmbedded);
                }
                destEmbedded.CopyFrom(srcEmbedded, connection);
            }
        }

        // ── Null / field state helpers ────────────────────────────────────────────

        public virtual void SetNulls()    => SetAllFieldNulls(true);
        public virtual void ClearNulls()  => SetAllFieldNulls(false);

        protected virtual void SetAllFieldNulls(bool isNull)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var field = entry.FieldInfo.GetValue(this) as TField;
                field?.SetNull(isNull);
            }
        }

        public bool FieldsAllNull()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var field = entry.FieldInfo.GetValue(this) as TField;
                if (field != null && !field.IsNull()) return false;
            }
            return true;
        }

        public virtual bool IsEmpty() => FieldsAllNull();

        // ── Compare / GetDifferences ──────────────────────────────────────────────

        public bool Compare(DataObject other)
        {
            if (other == this) return true;
            if (other.GetType() != GetType()) return false;

            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var f1 = entry.FieldInfo.GetValue(this)  as TField;
                var f2 = entry.FieldInfo.GetValue(other) as TField;
                if (f1 == null && f2 == null) continue;
                if (f1 == null || f2 == null) return false;
                if (!f1.Equals(f2)) return false;
            }
            return true;
        }

        public FieldSubset GetDifferences(DataObject other)
        {
            if (other.GetType() != GetType())
                throw new PersistenceException("Attempting to find differences between objects of different type");

            var result = new global::ActiveForge.FieldSubset(this, global::ActiveForge.FieldSubset.InitialState.ExcludeAll, null);
            if (other == this) return result;

            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var f1 = entry.FieldInfo.GetValue(this)  as TField;
                var f2 = entry.FieldInfo.GetValue(other) as TField;
                if (f1 != null && !f1.Equals(f2))
                    result.Include(this, this, f1);
            }
            return result;
        }

        // ── Identity / field helpers ──────────────────────────────────────────────

        /// <summary>Returns a unique in-process identifier for this object instance.</summary>
        public Guid GetUniqueIdentifier() => _uniqueIdentifier;

        /// <summary>Returns true if the named TField has a null value.</summary>
        public bool IsNull(string fieldName)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
                if (string.Equals(entry.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return (entry.FieldInfo.GetValue(this) as TField)?.IsNull() ?? true;
            return true;
        }

        // ── Embedded object helpers ───────────────────────────────────────────────

        public DataObject GetEmbeddedDataObject(string fieldName)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.DataObjects)
                if (string.Equals(entry.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return entry.FieldInfo.GetValue(this) as DataObject;
            return null;
        }

        public TField GetEmbeddedTField(string fieldName)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
                if (string.Equals(entry.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return entry.FieldInfo.GetValue(this) as TField;
            return null;
        }

        public virtual DataObject GetContainingDataObject(DataObject embedded)
        {
            ObjectBinding binding = GetBinding(Target, true, true, null, true);
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            var candidates = new List<DataObject>();

            foreach (var entry in meta.DataObjects)
            {
                var candidate = entry.FieldInfo.GetValue(this) as DataObject;
                if (candidate == null) continue;
                candidates.Add(candidate);
                if (candidate == embedded) return this;
            }
            foreach (var candidate in candidates)
            {
                var found = candidate.GetContainingDataObject(embedded);
                if (found != null) return found;
            }
            return null;
        }

        public bool DataObjectTableExists(DataConnection connection) => connection.TableExists(this);

        // ── Default sort order ────────────────────────────────────────────────────

        public virtual SortOrder GetDefaultSortOrder() => null;

        // ── GetSourceName ─────────────────────────────────────────────────────────

        public override string GetSourceName()
            => DataObjectMetaDataCache.GetTypeMetaData(GetType()).SourceName;

        // ── CRUD — Insert ─────────────────────────────────────────────────────────

        public virtual bool Insert(DataConnection target) { Target = target; return Insert(); }

        public virtual bool Insert()
        {
            if (Target == null) throw new PersistenceException("Insert failed (no connection): " + GetType().FullName);
            Target.Insert(this);
            SetLoaded(true);
            return true;
        }

        // ── CRUD — Delete ─────────────────────────────────────────────────────────

        public virtual bool Delete(DataConnection target) { Target = target; return Delete(); }

        public virtual bool Delete()
        {
            if (Target == null) throw new PersistenceException("Delete failed (no connection): " + GetType().FullName);
            return Target.Delete(this);
        }

        public virtual bool Delete(QueryTerm term)
        {
            if (Target == null) throw new PersistenceException("Delete failed (no connection): " + GetType().FullName);
            return Target.Delete(this, term);
        }

        internal virtual void Delete(QueryTerm term, Type[] concreteTypes)
        {
            if (Target == null) throw new PersistenceException("Delete failed (no connection): " + GetType().FullName);
            Target.Delete(this, term, concreteTypes);
        }

        // ── Action queue — Insert ─────────────────────────────────────────────────

        public virtual bool QueueForInsert(DataConnection target) { Target = target; return QueueForInsert(); }

        public virtual bool QueueForInsert()
        {
            if (Target == null) throw new PersistenceException("QueueForInsert failed (no connection): " + GetType().FullName);
            Target.QueueForInsert(this);
            return true;
        }

        // ── Action queue — Delete ─────────────────────────────────────────────────

        public virtual bool QueueForDelete(DataConnection target) { Target = target; return QueueForDelete(); }

        public virtual bool QueueForDelete()
        {
            if (Target == null) throw new PersistenceException("QueueForDelete failed (no connection): " + GetType().FullName);
            Target.QueueForDelete(this);
            return true;
        }

        public virtual bool QueueForDelete(QueryTerm term)
        {
            if (Target == null) throw new PersistenceException("QueueForDelete failed (no connection): " + GetType().FullName);
            Target.QueueForDelete(this, term);
            return true;
        }

        // ── CRUD — Update ─────────────────────────────────────────────────────────

        public virtual FieldSubset Update(DataConnection target) { Target = target; return Update(); }
        public virtual FieldSubset Update() => Update(DataObjectLock.UpdateOption.IgnoreLock);

        public virtual FieldSubset Update(DataObjectLock.UpdateOption option)
        {
            if (Target == null) throw new PersistenceException("Update failed (no connection): " + GetType().FullName);
            return Target.Update(this, option);
        }

        public virtual bool QueueForUpdate(DataConnection target) { Target = target; return QueueForUpdate(); }

        public virtual bool QueueForUpdate()
        {
            if (Target == null) throw new PersistenceException("QueueForUpdate failed (no connection): " + GetType().FullName);
            Target.QueueForUpdate(this);
            return true;
        }

        public virtual FieldSubset UpdateAll(DataConnection target) { Target = target; return UpdateAll(); }

        public virtual FieldSubset UpdateAll()
        {
            if (Target == null) throw new PersistenceException("UpdateAll failed (no connection): " + GetType().FullName);
            return Target.UpdateAll(this);
        }

        public virtual FieldSubset UpdateChanged(DataConnection target) { Target = target; return UpdateChanged(); }

        public virtual FieldSubset UpdateChanged()
        {
            if (Target == null) throw new PersistenceException("UpdateChanged failed (no connection): " + GetType().FullName);
            return Target.UpdateChanged(this);
        }

        // ── CRUD — Read ───────────────────────────────────────────────────────────

        public virtual bool Read(DataConnection target) { Target = target; return Read(); }
        public virtual bool Read() => Read(DefaultSubset);
        public virtual bool Read(DataConnection target, FieldSubset fieldSubset) { Target = target; return Read(fieldSubset); }

        public virtual bool Read(FieldSubset fieldSubset)
        {
            if (Target == null) throw new PersistenceException("Read failed (no connection): " + GetType().FullName);
            return Target.Read(this, fieldSubset);
        }

        // ── ReadForUpdate ─────────────────────────────────────────────────────────

        public virtual bool ReadForUpdate(DataConnection target, FieldSubset fieldSubset) { Target = target; return ReadForUpdate(fieldSubset); }
        public virtual bool ReadForUpdate() => ReadForUpdate((FieldSubset)null);

        public virtual bool ReadForUpdate(FieldSubset fieldSubset)
        {
            if (Target == null) throw new PersistenceException("ReadForUpdate failed (no connection): " + GetType().FullName);
            return Target.ReadForUpdate(this, fieldSubset);
        }

        // ── QueryFirst ────────────────────────────────────────────────────────────

        public virtual bool QueryFirst(DataConnection target, QueryTerm query, SortOrder sortOrder)
            { Target = target; return QueryFirst(query, sortOrder); }

        public virtual bool QueryFirst(DataConnection target, QueryTerm query, SortOrder sortOrder, FieldSubset fieldSubset)
            { Target = target; return QueryFirst(query, sortOrder, fieldSubset); }

        public virtual bool QueryFirst(QueryTerm query, SortOrder sortOrder)
            => QueryFirst(query, sortOrder, null);

        public virtual bool QueryFirst(QueryTerm query, SortOrder sortOrder, FieldSubset fieldSubset)
            => QueryFirst(query, sortOrder, fieldSubset, null);

        public virtual bool QueryFirst(QueryTerm query, SortOrder sortOrder, FieldSubset fieldSubset, ObjectParameterCollectionBase objectParameters)
        {
            if (Target == null) throw new PersistenceException("QueryFirst failed (no connection): " + GetType().FullName);
            return Target.QueryFirst(this, query, sortOrder, fieldSubset, objectParameters);
        }

        // ── QueryCount ────────────────────────────────────────────────────────────

        public virtual int QueryCount(DataConnection target) { Target = target; return QueryCount(); }
        public virtual int QueryCount(DataConnection target, QueryTerm term) { Target = target; return QueryCount(term); }
        public virtual int QueryCount() => QueryCount((QueryTerm)null);
        public virtual int QueryCount(QueryTerm term) => QueryCount(term, null, null);
        public virtual int QueryCount(QueryTerm term, FieldSubset subset) => QueryCount(term, null, subset);

        public virtual int QueryCount(QueryTerm term, Type[] expectedTypes, FieldSubset subset)
        {
            if (Target == null) throw new PersistenceException("QueryCount failed (no connection): " + GetType().FullName);
            return Target.QueryCount(this, term, expectedTypes, subset);
        }

        // ── QueryAll ─────────────────────────────────────────────────────────────

        public virtual ObjectCollection QueryAll(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset)
            { Target = target; return QueryAll(query, sortOrder, pageSize, expectedTypes, fieldSubset); }

        public virtual ObjectCollection QueryAll(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes)
            => QueryAll(target, query, sortOrder, pageSize, expectedTypes, null);

        public virtual ObjectCollection QueryAll(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize)
            => QueryAll(target, query, sortOrder, pageSize, null);

        public virtual ObjectCollection QueryAll(QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset)
        {
            if (Target == null) throw new PersistenceException("QueryAll failed (no connection): " + GetType().FullName);
            return Target.QueryAll(this, query, sortOrder, pageSize, expectedTypes, fieldSubset);
        }

        public virtual ObjectCollection QueryAll(QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            if (Target == null) throw new PersistenceException("QueryAll failed (no connection): " + GetType().FullName);
            return Target.QueryAll(this, query, sortOrder, pageSize, expectedTypes, fieldSubset, expectedTypeFieldSubsets);
        }

        public virtual ObjectCollection QueryAll(QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes)
            => QueryAll(query, sortOrder, pageSize, expectedTypes, null);

        public virtual ObjectCollection QueryAll(QueryTerm query, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset)
            => QueryAll(query, sortOrder, pageSize, null, fieldSubset);

        public virtual ObjectCollection QueryAll(QueryTerm query, SortOrder sortOrder, int pageSize)
            => QueryAll(query, sortOrder, pageSize, null, null);

        public virtual ObjectCollection QueryAll()
            => QueryAll(null, null, 0, null, null);

        public virtual ObjectCollection QueryAll(FieldSubset fieldSubset)
            => QueryAll(null, null, 0, null, fieldSubset);

        // ── LazyQueryAll ─────────────────────────────────────────────────────────

        public virtual IEnumerable<T> LazyQueryAll<T>(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset) where T : DataObject
            { Target = target; return LazyQueryAll<T>(query, sortOrder, pageSize, expectedTypes, fieldSubset); }

        public virtual IEnumerable<T> LazyQueryAll<T>(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes) where T : DataObject
            => LazyQueryAll<T>(target, query, sortOrder, pageSize, expectedTypes, null);

        public virtual IEnumerable<T> LazyQueryAll<T>(DataConnection target, QueryTerm query, SortOrder sortOrder, int pageSize) where T : DataObject
            => LazyQueryAll<T>(target, query, sortOrder, pageSize, null, null);

        public virtual IEnumerable<T> LazyQueryAll<T>(QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes, FieldSubset fieldSubset) where T : DataObject
        {
            if (Target == null) throw new PersistenceException("LazyQueryAll failed (no connection): " + GetType().FullName);
            return Target.LazyQueryAll((T)this, query, sortOrder, pageSize, expectedTypes, fieldSubset);
        }

        public virtual IEnumerable<T> LazyQueryAll<T>(QueryTerm query, SortOrder sortOrder, int pageSize, Type[] expectedTypes) where T : DataObject
            => LazyQueryAll<T>(query, sortOrder, pageSize, expectedTypes, null);

        public virtual IEnumerable<T> LazyQueryAll<T>(QueryTerm query, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset) where T : DataObject
            => LazyQueryAll<T>(query, sortOrder, pageSize, null, fieldSubset);

        public virtual IEnumerable<T> LazyQueryAll<T>(QueryTerm query, SortOrder sortOrder, int pageSize) where T : DataObject
            => LazyQueryAll<T>(query, sortOrder, pageSize, null, null);

        public virtual IEnumerable<T> LazyQueryAll<T>() where T : DataObject
            => LazyQueryAll<T>(null, null, 0, null, null);

        public virtual IEnumerable<T> LazyQueryAll<T>(FieldSubset fieldSubset) where T : DataObject
            => LazyQueryAll<T>(null, null, 0, null, fieldSubset);

        // ── QueryPage ─────────────────────────────────────────────────────────────

        public virtual ObjectCollection QueryPage(int start, int count)
            => QueryPage(null, null, start, count, null);

        public virtual ObjectCollection QueryPage(int start, int count, FieldSubset fieldSubset)
            => QueryPage(null, null, start, count, fieldSubset);

        public virtual ObjectCollection QueryPage(DataConnection target, QueryTerm query, SortOrder sortOrder, int start, int count)
            { Target = target; return QueryPage(query, sortOrder, start, count, null); }

        public virtual ObjectCollection QueryPage(DataConnection target, QueryTerm query, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset)
            { Target = target; return QueryPage(query, sortOrder, start, count, fieldSubset); }

        public virtual ObjectCollection QueryPage(QueryTerm query, SortOrder sortOrder, int start, int count)
            => QueryPage(query, sortOrder, start, count, null);

        public virtual ObjectCollection QueryPage(QueryTerm query, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset)
            => QueryPage(query, sortOrder, start, count, fieldSubset, null);

        public virtual ObjectCollection QueryPage(QueryTerm query, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes)
            => QueryPage(query, sortOrder, start, count, fieldSubset, expectedTypes, null);

        public virtual ObjectCollection QueryPage(QueryTerm query, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets)
        {
            if (Target == null) throw new PersistenceException("QueryPage failed (no connection): " + GetType().FullName);
            return Target.QueryPage(this, query, sortOrder, start, count, fieldSubset, expectedTypes, expectedTypeFieldSubsets);
        }

        public virtual ObjectCollection QueryPage(QueryTerm query, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, Type[] expectedTypes, Dictionary<Type, FieldSubset> expectedTypeFieldSubsets, bool returnCountInfo)
        {
            if (Target == null) throw new PersistenceException("QueryPage failed (no connection): " + GetType().FullName);
            return Target.QueryPage(this, query, sortOrder, start, count, fieldSubset, expectedTypes, expectedTypeFieldSubsets, returnCountInfo);
        }

        // ── ExecSQL ───────────────────────────────────────────────────────────────

        public virtual ObjectCollection ExecSQL(string sql)
        {
            if (Target == null) throw new PersistenceException("ExecSQL failed (no connection): " + GetType().FullName);
            return Target.ExecSQL(this, sql);
        }

        public virtual ObjectCollection ExecSQL(string sqlFormat, params object[] values)
        {
            if (Target == null) throw new PersistenceException("ExecSQL failed (no connection): " + GetType().FullName);
            return Target.ExecSQL(this, sqlFormat, values);
        }

        public virtual ObjectCollection ExecSQL(string sql, Dictionary<string, object> parameters)
        {
            if (Target == null) throw new PersistenceException("ExecSQL failed (no connection): " + GetType().FullName);
            return Target.ExecSQL(this, sql, parameters);
        }

        public virtual ObjectCollection ExecSQL(string sql, int start, int count)
        {
            if (Target == null) throw new PersistenceException("ExecSQL failed (no connection): " + GetType().FullName);
            return Target.ExecSQL(this, sql, start, count);
        }

        public virtual ObjectCollection ExecSQL(string sql, int start, int count, Dictionary<string, object> parameters)
        {
            if (Target == null) throw new PersistenceException("ExecSQL failed (no connection): " + GetType().FullName);
            return Target.ExecSQL(this, sql, start, count, parameters);
        }

        public virtual ObjectCollection ExecSQL(DataConnection target, string sql, int start, int count)
            { Target = target; return ExecSQL(sql, start, count); }

        public virtual ObjectCollection ExecSQL(DataConnection target, string sql, params object[] values)
            { Target = target; return ExecSQL(sql, values); }

        public virtual ObjectCollection ExecSQL(DataConnection target, string sql)
            { Target = target; return ExecSQL(sql); }

        // ── ExecStoredProcedure ───────────────────────────────────────────────────

        public virtual ObjectCollection ExecStoredProcedure(string spName, int start, int count, params SPParameter[] spParameters)
        {
            if (Target == null) throw new PersistenceException("ExecStoredProcedure failed (no connection): " + GetType().FullName);
            return Target.ExecStoredProcedure(this, spName, start, count, spParameters);
        }

        // ── GetFieldDescription override ──────────────────────────────────────────

        public override string GetFieldDescription(FieldInfo fi)
        {
            if (_innerTarget != null) return _innerTarget.GetFieldDescription(fi, this);
            return base.GetFieldDescription(fi);
        }

        // ── GetDBBaseClass helpers ────────────────────────────────────────────────

        public static Type GetDBBaseClass(Type type)
        {
            Type current = type;
            while (current != null && current != typeof(DataObject) && current.BaseType != typeof(DataObject)
                   && current.BaseType != null && current.BaseType != typeof(object))
            {
                if (current.BaseType == typeof(DataObject)
                    || current.BaseType == typeof(IdentDataObject)
                    || (current.BaseType?.Namespace?.StartsWith("ActiveForge") == true
                        && !current.BaseType.IsAbstract))
                    break;
                current = current.BaseType;
            }
            return current;
        }

        public virtual string GetDBBaseClassName() => GetDBBaseClass(GetType())?.Name ?? GetType().Name;

        // ── SPParameter inner types ───────────────────────────────────────────────

        public class SPParameter
        {
            public enum eDirection { Input = 1, Output = 2 }

            public string     ParameterMark     = "";
            public string     Name              = "";
            public object     Value             = null;
            public eDirection ParameterDirection = eDirection.Input;

            public SPParameter(string name, object value, eDirection direction)
            {
                Name              = name;
                Value             = value;
                ParameterDirection = direction;
            }
        }

        public class SPInputParameter : SPParameter
        {
            public SPInputParameter(string name, object value)
                : base(name, value, eDirection.Input) { }
        }

        public abstract class SPOutputParameter : SPParameter
        {
            protected SPOutputParameter(string name, object exampleValue)
                : base(name, exampleValue, eDirection.Output) { }
        }

        public class SPOutputParameterVarchar : SPOutputParameter
        {
            public int Length;
            public SPOutputParameterVarchar(string name, int length)
                : base(name, "") { Length = length; }
        }

        public class SPOutputParameterInteger : SPOutputParameter
        {
            public SPOutputParameterInteger(string name) : base(name, 0) { }
        }

        public class SPOutputParameterDateTime : SPOutputParameter
        {
            public SPOutputParameterDateTime(string name) : base(name, DateTime.MinValue) { }
        }
    }
}
