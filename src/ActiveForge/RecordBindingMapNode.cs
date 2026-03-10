using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ActiveForge.Attributes;

namespace ActiveForge
{
    public delegate void FieldFetcher(Record obj, ReaderBase reader, FieldBinding info, RecordBinding binding, bool omitPK, bool omitPKForOrdinals);

    /// <summary>
    /// Represents one node in the SQL join tree for a Record class hierarchy.
    /// Each node maps to a single DB table (source) and links to generalisation nodes
    /// (parent tables), relationship nodes (FK-joined embedded objects), and polymorphic
    /// specialisation nodes (concrete subtype tables).
    /// </summary>
    public class RecordBindingMapNode
    {
        // ── Static type caches ────────────────────────────────────────────────────────
        private static readonly Type _tForeignKeyType           = typeof(TForeignKey);
        private static readonly Type _dataObjectType            = typeof(Record);
        private static readonly Type _lookupDataObjectType      = typeof(LookupRecord);
        private static readonly Type _baseTableAttributeType    = typeof(BaseTableAttribute);
        private static readonly Type _computedAttributeType     = typeof(ComputedAttribute);

        // ── Links to other nodes ──────────────────────────────────────────────────────
        public RecordBindingMapNode             Generalisation;
        public List<RelationshipSpecification>  Relations;
        public List<PolymorphicJoinSpec>        PolymorphicSpecialisations;

        // ── Node values ───────────────────────────────────────────────────────────────
        public List<Type>  ClassesInNode;
        public bool        IsDBDerived;
        public bool        IsBase;
        public string      SourceName;
        public string      Alias;
        public string      FKJoinName;
        public string      FieldName;
        public Type        Class;
        public JoinSpecification.JoinTypeEnum JoinType = JoinSpecification.JoinTypeEnum.InnerJoin;
        public bool        Function        = false;
        public string      FunctionTempTable = "";

        protected AliasGenerator  AliasGenerator;
        protected Stack<Type>     SpecialisationStack;
        protected Type[]          ExpectedTypes;
        protected FactoryBase     Factory;
        protected bool            _includeLookupDataObjects;

        // ── Constructors ──────────────────────────────────────────────────────────────

        public RecordBindingMapNode()
        {
            Generalisation = null;
            Relations      = null;
            ClassesInNode  = null;
            SourceName     = "";
            FieldName      = "";
            FKJoinName     = "";
            IsBase         = false;
            IsDBDerived    = false;
        }

        /// <summary>Root constructor — used for the root object of a binding.</summary>
        public RecordBindingMapNode(Type classType, AliasGenerator aliasGen, Type[] expectedTypes, FactoryBase factory, bool includeLookup)
            : this()
        {
            Class                    = classType;
            AliasGenerator           = aliasGen;
            ExpectedTypes            = expectedTypes;
            Factory                  = factory;
            _includeLookupDataObjects = includeLookup;
            PopulateAttributes();
            PopulateClassesInNode();
            Populate();
        }

        /// <summary>Relationship constructor — used for FK-joined embedded objects.</summary>
        public RecordBindingMapNode(Type classType, AliasGenerator aliasGen, string fkJoinName, string fieldName, JoinSpecification.JoinTypeEnum joinType, FactoryBase factory, bool includeLookup)
            : this()
        {
            Class                    = classType;
            AliasGenerator           = aliasGen;
            FKJoinName               = fkJoinName;
            FieldName                = fieldName;
            JoinType                 = joinType;
            Factory                  = factory;
            _includeLookupDataObjects = includeLookup;
            PopulateAttributes();
            PopulateClassesInNode();
            Populate();
        }

        /// <summary>Polymorphic specialisation constructor.</summary>
        public RecordBindingMapNode(Type classType, AliasGenerator aliasGen, string fkJoinName, string fieldName, JoinSpecification.JoinTypeEnum joinType, Stack<Type> specialisationStack, FactoryBase factory, RecordBindingMapNode generalisation, bool includeLookup)
            : this()
        {
            Class                    = classType;
            AliasGenerator           = aliasGen;
            FKJoinName               = fkJoinName;
            FieldName                = fieldName;
            JoinType                 = joinType;
            SpecialisationStack      = specialisationStack;
            Factory                  = factory;
            Generalisation           = generalisation;
            _includeLookupDataObjects = includeLookup;
            PopulateAttributes();
            PopulateClassesInNode();
            Populate();
            SpecialisationStack = null;
            Generalisation      = null;
        }

        /// <summary>Internal constructor used for generalisation nodes.</summary>
        public RecordBindingMapNode(Type classType, AliasGenerator aliasGen, JoinSpecification.JoinTypeEnum joinType, FactoryBase factory, bool includeLookup)
            : this()
        {
            Class                    = classType;
            AliasGenerator           = aliasGen;
            JoinType                 = joinType;
            Factory                  = factory;
            _includeLookupDataObjects = includeLookup;
            PopulateAttributes();
            PopulateClassesInNode();
            Populate();
        }

        // ── Relationship accessors ────────────────────────────────────────────────────

        public List<RelationshipSpecification> GetRelationships() => Relations;

        public List<RelationshipSpecification> GetRelationships(bool includeBaseClasses)
        {
            var result = new List<RelationshipSpecification>();
            if (Relations != null) result.AddRange(Relations);
            if (includeBaseClasses && Generalisation != null)
            {
                var parentRels = Generalisation.GetRelationships(true);
                if (parentRels != null) result.AddRange(parentRels);
            }
            return result;
        }

        public void PreRetrieveLookupDataObjectValues(DataConnection connection, RecordBase obj)
        {
            if (Generalisation != null)
                Generalisation.PreRetrieveLookupDataObjectValues(connection, obj);

            if (Relations != null)
            {
                foreach (var rel in Relations)
                {
                    if (rel.ObjectTargetMapNode.Class.IsSubclassOf(_lookupDataObjectType))
                    {
                        rel.ObjectTargetMapNode.PreRetrieveLookupDataObjectValues(connection, (RecordBase)rel.ObjectTargetFieldInfo.GetValue(obj));
                        if (rel.ObjectTargetFieldInfo != null)
                        {
                            var ldo = (LookupRecord)rel.ObjectTargetFieldInfo.GetValue(obj);
                            ldo.PrimeAndQueryCache(null, null, 0);
                        }
                    }
                    else
                    {
                        rel.ObjectTargetMapNode.PreRetrieveLookupDataObjectValues(connection, (RecordBase)rel.ObjectTargetFieldInfo.GetValue(obj));
                    }
                }
            }

            if (PolymorphicSpecialisations != null)
            {
                foreach (var rel in PolymorphicSpecialisations)
                {
                    var concreteObj = (Record)Record.CreateDataObject(rel.ObjectTargetMapNode.Class, connection);
                    rel.ObjectTargetMapNode.PreRetrieveLookupDataObjectValues(connection, concreteObj);
                }
            }
        }

        // ── Attribute population ──────────────────────────────────────────────────────

        protected void PopulateAttributes()
        {
            var derived = CustomAttributeCache.GetClassAttribute(Class, _computedAttributeType, false);
            if (derived != null)
                IsDBDerived = true;

            var baseAttr = CustomAttributeCache.GetClassAttribute(Class, _baseTableAttributeType, false);
            if (baseAttr != null)
            {
                IsBase = true;
            }
            else if (CustomAttributeCache.GetClassAttribute(Class, _baseTableAttributeType, true) == null)
            {
                IsBase = true; // no explicit base anywhere in hierarchy → single table
            }
            else
            {
                if (!IsDBDerived)
                    IsBase = true; // explicit base exists, but no DBDerived between us and it
            }
        }

        protected void PopulateClassesInNode()
        {
            int    structuralAttribs = 0;
            string tableSourceName   = GetSourceName(Class);
            Type   nextType          = Class;
            bool   dbDerivedFound    = false;

            ClassesInNode = new List<Type>();

            if (CustomAttributeCache.GetClassAttribute(Class, _computedAttributeType, false) != null)
            {
                structuralAttribs++;
                dbDerivedFound = true;
            }
            if (CustomAttributeCache.GetClassAttribute(Class, _baseTableAttributeType, false) != null)
                structuralAttribs++;

            while ((string.Equals(GetSourceName(nextType), tableSourceName, StringComparison.OrdinalIgnoreCase)) || IsIntermediateClass(nextType, dbDerivedFound))
            {
                ClassesInNode.Add(nextType);
                nextType = nextType.BaseType;

                if (nextType == null || nextType.FullName.StartsWith("ActiveForge"))
                    break;
            }

            if (structuralAttribs > 1)
                throw new PersistenceException($"{Class.FullName}: [Computed] attribute must only be used to introduce a new table into an inheritance hierarchy");
        }

        protected bool IsIntermediateClass(Type type, bool dbDerivedFound)
        {
            if (!dbDerivedFound) return false;
            if (CustomAttributeCache.GetClassAttribute(type, _computedAttributeType, false) != null) return false;
            if (CustomAttributeCache.GetClassAttribute(type, _baseTableAttributeType, false) != null) return false;
            return true;
        }

        protected string GetSourceName(Type type)
        {
            var source = CustomAttributeCache.GetClassAttribute(type, typeof(TableAttribute), false) as TableAttribute;
            return source != null ? source.SourceName : type.Name;
        }

        // ── Core populate ─────────────────────────────────────────────────────────────

        protected void Populate()
        {
            SourceName = GetSourceName(Class);

            if (AliasGenerator != null)
                Alias = AliasGenerator.NewAlias();

            // Handle generalisation (derived table inheriting from base table)
            if (IsDBDerived && SpecialisationStack == null)
            {
                Type nextType = Class.BaseType;
                while (ClassesInNode.Contains(nextType))
                    nextType = nextType.BaseType;
                Generalisation = new RecordBindingMapNode(nextType, AliasGenerator, JoinType, Factory, _includeLookupDataObjects);
            }

            var joinedObjects = new Dictionary<string, object>();
            PopulateRelationshipsUsingJoinSpec(joinedObjects);
            PopulateRelationshipsUsingJoin(joinedObjects);

            // Polymorphic expected types
            if (ExpectedTypes != null)
            {
                foreach (var expected in ExpectedTypes)
                {
                    var inheritance = InheritanceStack(expected);
                    var next = inheritance.Peek();
                    while (next != Class) { inheritance.Pop(); next = inheritance.Peek(); }
                    inheritance.Pop();
                    AddPolymorphicJoin(inheritance);
                }
            }

            if (SpecialisationStack != null && SpecialisationStack.Count > 0)
                AddPolymorphicJoin(SpecialisationStack);
        }

        // ── Relationship population ───────────────────────────────────────────────────

        protected void PopulateRelationshipsUsingJoinSpec(Dictionary<string, object> joinedFields)
        {
            // Collect all JoinSpecAttributes from class and fields
            var joinSpecs = new List<object>();
            Type nextType = Class;
            while (ClassesInNode.Contains(nextType))
            {
                var classAttrs = CustomAttributeCache.GetClassAttributes(nextType, typeof(JoinSpecAttribute), false);
                if (classAttrs != null) joinSpecs.AddRange(classAttrs);
                nextType = nextType.BaseType;
            }

            var fieldList = Class.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var fi in fieldList)
            {
                if (ClassesInNode.Contains(fi.DeclaringType) || IsBase)
                {
                    var joinAttr = CustomAttributeCache.GetFieldAttribute(fi, typeof(JoinSpecAttribute), false) as JoinSpecAttribute;
                    if (joinAttr != null) joinSpecs.Add(joinAttr);
                }
            }

            foreach (JoinSpecAttribute spec in joinSpecs)
            {
                var fkField     = FindFieldByName(fieldList, spec.ForeignKeyField);
                var targetField = FindFieldByName(fieldList, spec.TargetField);

                if (fkField == null)
                    throw new PersistenceException($"JoinSpec FK field '{spec.ForeignKeyField}' not found.");
                if (targetField == null)
                    throw new PersistenceException($"JoinSpec target field '{spec.TargetField}' not found.");
                if (!targetField.FieldType.IsSubclassOf(_dataObjectType))
                    throw new PersistenceException($"JoinSpec target '{spec.TargetField}' is not derived from Record.");

                if (!joinedFields.ContainsKey(spec.TargetField))
                {
                    var overriddenJoinType = spec.JoinType;
                    if (JoinType == JoinSpecification.JoinTypeEnum.LeftOuterJoin)
                        overriddenJoinType = JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin;

                    var relatedType = targetField.FieldType;
                    if (Factory != null) relatedType = Factory.MapType(relatedType);

                    string joinFieldName = ResolveTargetFieldName(fkField);
                    string aliasKey      = GetSourceNameForAliasLookup(fkField.DeclaringType);

                    Relations ??= new List<RelationshipSpecification>();
                    var rel = new RelationshipSpecification(
                        fkField,
                        SourceNameToAlias(aliasKey),
                        targetField,
                        spec.TargetPrimaryKeyField,
                        new RecordBindingMapNode(relatedType, AliasGenerator, joinFieldName, spec.TargetField, JoinSpecification.MapJoinType(overriddenJoinType), Factory, _includeLookupDataObjects));
                    Relations.Add(rel);
                    joinedFields[spec.TargetField] = null;
                }
            }
        }

        protected void PopulateRelationshipsUsingJoin(Dictionary<string, object> joinedFields)
        {
            var fieldList      = Class.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var embeddedInfos  = new List<FieldInfoContainer>();

            foreach (var fi in fieldList)
            {
                if (fi.FieldType.IsSubclassOf(_dataObjectType))
                    embeddedInfos.Add(new FieldInfoContainer(fi.FieldType.Name, fi.Name, fi));
            }

            foreach (var fkFI in fieldList)
            {
                if (fkFI.FieldType != _tForeignKeyType) continue;

                string fkFieldName       = fkFI.Name;
                string embeddedFieldName = "";
                var    innerJoinType     = JoinType == JoinSpecification.JoinTypeEnum.LeftOuterJoin
                    ? JoinSpecification.JoinTypeEnum.LeftOuterJoin
                    : JoinSpecification.JoinTypeEnum.InnerJoin;

                var joinAttr = CustomAttributeCache.GetFieldAttribute(fkFI, typeof(JoinAttribute), false) as JoinAttribute;
                if (joinAttr != null)
                {
                    if (joinAttr.ForeignKey.Length > 0)  fkFieldName       = joinAttr.ForeignKey;
                    if (joinAttr.TargetField.Length > 0) embeddedFieldName = joinAttr.TargetField;
                    innerJoinType = JoinSpecification.MapJoinType(joinAttr.JoinType);
                }

                if (embeddedFieldName.Length == 0 && fkFieldName.Length > 2 && fkFieldName.ToLower().EndsWith("id"))
                    embeddedFieldName = fkFieldName.Substring(0, fkFieldName.Length - 2);

                if (fkFieldName.Length > 2 && fkFieldName.ToLower().EndsWith("id"))
                {
                    string foreignTableName = fkFieldName.Substring(0, fkFieldName.Length - 2);
                    foreach (var container in embeddedInfos)
                    {
                        if (embeddedFieldName == container.FieldName)
                        {
                            if (container.FieldTypeName.EndsWith(foreignTableName))
                            {
                                if (ClassesInNode.Contains(container.FieldInfo.DeclaringType) && !joinedFields.ContainsKey(container.FieldName))
                                {
                                    var relatedType  = container.FieldInfo.FieldType;
                                    if (Factory != null) relatedType = Factory.MapType(relatedType);

                                    string joinFieldName = ResolveTargetFieldName(fkFI);
                                    string aliasKey      = GetSourceNameForAliasLookup(fkFI.DeclaringType);

                                    Relations ??= new List<RelationshipSpecification>();
                                    var rel = new RelationshipSpecification(
                                        fkFI,
                                        SourceNameToAlias(aliasKey),
                                        container.FieldInfo,
                                        "ID",
                                        new RecordBindingMapNode(relatedType, AliasGenerator, joinFieldName, container.FieldInfo.Name, innerJoinType, Factory, _includeLookupDataObjects));
                                    Relations.Add(rel);
                                    joinedFields[container.FieldName] = null;
                                }
                            }
                            else if (!joinedFields.ContainsKey(container.FieldName) && container.FieldInfo.DeclaringType.Name == Class.Name)
                            {
                                throw new PersistenceException($"Use [JoinSpec] to link {fkFieldName} and {embeddedFieldName} in {Class.FullName}");
                            }
                        }
                    }
                }
            }
        }

        // ── Polymorphic joins ─────────────────────────────────────────────────────────

        public void AddPolymorphicJoin(Stack<Type> inheritance)
        {
            PolymorphicSpecialisations ??= new List<PolymorphicJoinSpec>();
            if (inheritance.Count == 0) return;

            var child = inheritance.Pop();
            foreach (var spec in PolymorphicSpecialisations)
            {
                if (spec.ObjectTargetMapNode.Class == child)
                {
                    spec.ObjectTargetMapNode.AddPolymorphicJoin(inheritance);
                    return;
                }
            }

            FieldInfo keyField = null;
            foreach (var fi in Class.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fi.Name == "ID") { keyField = fi; break; }
            }

            PolymorphicSpecialisations.Add(new PolymorphicJoinSpec(keyField,
                new RecordBindingMapNode(child, AliasGenerator, "ID", "ID", JoinSpecification.JoinTypeEnum.LeftOuterJoin, inheritance, Factory, this, _includeLookupDataObjects)));
        }

        // ── Join specifications ───────────────────────────────────────────────────────

        public void GetJoinSpecifications(ref List<JoinSpecification> specs, FieldSubset fieldSubset, bool forUpdate)
        {
            if (Generalisation != null)
            {
                specs ??= new List<JoinSpecification>();
                var s = new JoinSpecification
                {
                    SourceAlias     = Alias,
                    JoinSource      = SourceName,
                    JoinSourceField = "ID",
                    TargetAlias     = Generalisation.Alias,
                    JoinTarget      = Generalisation.SourceName,
                    JoinTargetField = "ID",
                    JoinType        = Generalisation.JoinType,
                };
                if (!s.InList(specs))
                {
                    specs.Add(s);
                    Generalisation.GetJoinSpecifications(ref specs, fieldSubset, forUpdate);
                }
            }

            if (!forUpdate && Relations != null)
            {
                specs ??= new List<JoinSpecification>();
                foreach (var rel in Relations)
                {
                    if (!rel.ObjectTargetMapNode.Class.IsSubclassOf(_lookupDataObjectType) || _includeLookupDataObjects)
                    {
                        bool include       = true;
                        FieldSubset childSubset = null;
                        if (fieldSubset != null)
                        {
                            include     = fieldSubset.IncludesJoin(rel.ObjectTargetFieldInfo.Name);
                            childSubset = fieldSubset.FieldSubsetForJoin(rel.ObjectTargetFieldInfo.Name);
                        }
                        if (include)
                        {
                            var s = new JoinSpecification
                            {
                                SourceAlias     = rel.KeyFieldAlias,
                                JoinSource      = SourceName,
                                JoinSourceField = rel.ObjectTargetMapNode.FKJoinName,
                                TargetAlias     = rel.ObjectTargetMapNode.Alias,
                                JoinTarget      = rel.ObjectTargetMapNode.SourceName,
                                JoinTargetField = rel.TargetPrimaryKeyField,
                                JoinType        = rel.ObjectTargetMapNode.JoinType,
                                JoinTargetClass = rel.ObjectTargetMapNode.Class,
                            };
                            if (!s.InList(specs))
                            {
                                specs.Add(s);
                                rel.ObjectTargetMapNode.GetJoinSpecifications(ref specs, childSubset, forUpdate);
                            }
                        }
                    }
                }
            }
        }

        public void GetGeneralizationJoinSpecifications(ref List<JoinSpecification> specs)
        {
            if (Generalisation == null) return;
            specs ??= new List<JoinSpecification>();
            var s = new JoinSpecification
            {
                SourceAlias     = Alias,
                JoinSource      = SourceName,
                JoinSourceField = "ID",
                TargetAlias     = Generalisation.Alias,
                JoinTarget      = Generalisation.SourceName,
                JoinTargetField = "ID",
                JoinType        = Generalisation.JoinType,
            };
            if (!s.InList(specs))
            {
                specs.Add(s);
                Generalisation.GetGeneralizationJoinSpecifications(ref specs);
            }
        }

        // ── Field fetch ───────────────────────────────────────────────────────────────

        public void FetchRowValues(List<FieldBinding> fieldBindings, FieldFetcher fetcher, Record obj, ReaderBase reader, RecordBinding binding, bool omitPK, bool omitPKForOrdinals, bool shallow, int depth)
        {
            bool includesRelatedFields = false;
            foreach (var fb in fieldBindings)
            {
                if (fb.MapNode == this)
                    fetcher(obj, reader, fb, binding, omitPK, omitPKForOrdinals);
                else
                    includesRelatedFields = true;
            }

            if (Generalisation != null)
                Generalisation.FetchRowValues(fieldBindings, fetcher, obj, reader, binding, omitPK, omitPKForOrdinals, shallow, depth);

            if (!shallow && Relations != null)
            {
                foreach (var rel in Relations)
                {
                    if (rel.ObjectTargetMapNode.Class.IsSubclassOf(_lookupDataObjectType) && !_includeLookupDataObjects)
                    {
                        var ldo    = (LookupRecord)rel.ObjectTargetFieldInfo.GetValue(obj);
                        var newLdo = (LookupRecord)ldo.GetConnection().Create(rel.ObjectTargetMapNode.Class);
                        newLdo.ID  = (TForeignKey)rel.KeyFieldInfo.GetValue(obj);
                        if (newLdo.ID.IsNull() || !newLdo.ID.IsLoaded())
                            newLdo.SetNulls();
                        else
                            newLdo.Read();
                        rel.ObjectTargetFieldInfo.SetValue(obj, newLdo);
                    }
                    else if (includesRelatedFields)
                    {
                        var embedded = (Record)rel.ObjectTargetFieldInfo.GetValue(obj);
                        rel.ObjectTargetMapNode.FetchRowValues(fieldBindings, fetcher, embedded, reader, binding, false, omitPKForOrdinals, shallow, depth);
                    }
                }
            }
        }

        public void FetchPolymorphicRowValues(List<FieldBinding> fieldBindings, FieldFetcher fetcher, Record obj, Type objectType, ReaderBase reader, RecordBinding binding, bool omitPK, bool omitPKForOrdinals, int depth)
        {
            var inheritance = InheritanceStack(objectType);
            while (inheritance.Peek() != Class) inheritance.Pop();
            FetchPolymorphicRowValuesInternal(fieldBindings, fetcher, obj, reader, binding, omitPK, omitPKForOrdinals, inheritance, depth);
        }

        private void FetchPolymorphicRowValuesInternal(List<FieldBinding> fieldBindings, FieldFetcher fetcher, Record obj, ReaderBase reader, RecordBinding binding, bool omitPK, bool omitPKForOrdinals, Stack<Type> inheritance, int depth)
        {
            if (inheritance.Peek() != Class) return;
            inheritance.Pop();
            FetchRowValues(fieldBindings, fetcher, obj, reader, binding, omitPK, omitPKForOrdinals, false, depth);

            if (PolymorphicSpecialisations != null)
            {
                foreach (var spec in PolymorphicSpecialisations)
                {
                    spec.ObjectTargetMapNode.FetchPolymorphicRowValuesInternal(fieldBindings, fetcher, obj, reader, binding, omitPK, omitPKForOrdinals, inheritance, depth);
                    if (inheritance.Count == 0) break;
                }
            }
        }

        // ── Field array population ────────────────────────────────────────────────────

        public void PopulateFieldArrays(RecordBase obj, DataConnection connection, bool targetExists, RecordBase changedObject, List<FieldBinding> readFields, List<FieldBinding> updateFields, List<FieldBinding> diagnosticFields)
        {
            var fieldList = Class.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var fi in fieldList)
            {
                if (!AllowedDeclaringClass(fi.DeclaringType)) continue;
                if (fi.FieldType.IsSubclassOf(_dataObjectType)) continue;

                var fb = PopulateFieldInfo(fi, obj, connection, targetExists, changedObject);
                if (fb != null)
                {
                    readFields?.Add(fb);
                    if (!fb.Info.IsReadOnly) updateFields?.Add(fb);
                }
            }

            if (diagnosticFields != null)
            {
                if (PolymorphicSpecialisations == null || PolymorphicSpecialisations.Count == 0)
                {
                    // leaf node — populate ID as concrete class discriminator
                    foreach (var fi in fieldList)
                    {
                        if (!fi.FieldType.IsSubclassOf(_dataObjectType) && fi.Name == "ID")
                        {
                            var fb = PopulateFieldInfo(fi, obj, connection, targetExists, changedObject);
                            if (fb != null) diagnosticFields.Add(fb);
                        }
                    }
                }
            }

            Generalisation?.PopulateFieldArrays(obj, connection, targetExists, changedObject, readFields, updateFields, null);

            if (Relations != null && changedObject == null && targetExists)
            {
                foreach (var rel in Relations)
                {
                    if (!rel.ObjectTargetMapNode.Class.IsSubclassOf(_lookupDataObjectType) || _includeLookupDataObjects)
                        rel.ObjectTargetMapNode.PopulateFieldArrays(obj, connection, targetExists, changedObject, readFields, null, null);
                }
            }

            if (PolymorphicSpecialisations != null && changedObject == null && targetExists)
            {
                foreach (var spec in PolymorphicSpecialisations)
                {
                    if (!spec.ObjectTargetMapNode.Class.IsSubclassOf(_lookupDataObjectType) || _includeLookupDataObjects)
                        spec.ObjectTargetMapNode.PopulateFieldArrays(obj, connection, targetExists, changedObject, readFields, null, diagnosticFields);
                }
            }
        }

        public void PopulateUpdateTableAliasArray(List<string> tables)
        {
            tables.Insert(0, Alias);
            Generalisation?.PopulateUpdateTableAliasArray(tables);
        }

        // ── Path navigation ───────────────────────────────────────────────────────────

        public void ConstructPathToField(FieldBinding targetBinding, Stack<string> path)
        {
            if (ReferenceEquals(targetBinding.MapNode, this))
            {
                path.Push(targetBinding.Info.FieldName);
                return;
            }

            if (Generalisation != null && path.Count == 0)
                Generalisation.ConstructPathToField(targetBinding, path);

            if (Relations != null && path.Count == 0)
            {
                foreach (var rel in Relations)
                {
                    rel.ObjectTargetMapNode.ConstructPathToField(targetBinding, path);
                    if (path.Count > 0) { path.Push(rel.ObjectTargetFieldInfo.Name); break; }
                }
            }

            if (PolymorphicSpecialisations != null && path.Count == 0)
            {
                foreach (var spec in PolymorphicSpecialisations)
                    spec.ObjectTargetMapNode.ConstructPathToField(targetBinding, path);
            }
        }

        public Record GetContainingObjectForField(RecordBinding binding, FieldBinding targetBinding, Record parentObject)
            => GetContainingObjectForField(binding, targetBinding, parentObject, true);

        protected Record GetContainingObjectForField(RecordBinding binding, FieldBinding targetBinding, Record parentObject, bool chasePolymorphic)
        {
            Record container = null;

            foreach (var fb in binding.Fields)
            {
                if (ReferenceEquals(fb.MapNode, targetBinding.MapNode) && fb.MapNode == this)
                {
                    container = parentObject;
                    break;
                }
            }

            if (container == null && Generalisation != null)
                container = Generalisation.GetContainingObjectForField(binding, targetBinding, parentObject, false);

            if (container == null && Relations != null)
            {
                foreach (var rel in Relations)
                {
                    if (rel.ObjectTargetMapNode.Class.IsSubclassOf(_dataObjectType) && rel.ObjectTargetFieldInfo != null)
                    {
                        var do_ = rel.ObjectTargetFieldInfo.GetValue(parentObject) as Record;
                        container = rel.ObjectTargetMapNode.GetContainingObjectForField(binding, targetBinding, do_, false);
                        if (container != null) break;
                    }
                }
            }

            if (chasePolymorphic && container == null && PolymorphicSpecialisations != null)
            {
                foreach (var spec in PolymorphicSpecialisations)
                {
                    var do_ = (Record)Record.CreateDataObject(spec.ObjectTargetMapNode.Class, parentObject.GetConnection());
                    container = spec.ObjectTargetMapNode.GetContainingObjectForField(binding, targetBinding, do_, false);
                    if (container != null) break;
                }
            }

            return container;
        }

        // ── Alias helpers ─────────────────────────────────────────────────────────────

        public string GetMostGeneralAlias()
            => Generalisation != null ? Generalisation.GetMostGeneralAlias() : Alias;

        public string AliasToSourceName(string alias)
        {
            if (alias == Alias) return SourceName;
            if (Generalisation != null)
            {
                var r = Generalisation.AliasToSourceName(alias);
                if (r.Length > 0) return r;
            }
            if (Relations != null)
            {
                foreach (var rel in Relations)
                {
                    var r = rel.ObjectTargetMapNode.AliasToSourceName(alias);
                    if (r.Length > 0) return r;
                }
            }
            return "";
        }

        public string SourceNameToAlias(string sourceNameIn)
        {
            if (string.Equals(sourceNameIn, SourceName, StringComparison.OrdinalIgnoreCase))
                return Alias;

            if (ClassesInNode != null)
            {
                foreach (Type ct in ClassesInNode)
                {
                    Type cur = ct;
                    while (cur != null)
                    {
                        if (string.Equals(sourceNameIn, cur.Name, StringComparison.OrdinalIgnoreCase))
                            return Alias;
                        cur = cur.BaseType;
                    }
                }
            }

            if (Generalisation != null)
            {
                var r = Generalisation.SourceNameToAlias(sourceNameIn);
                if (r.Length > 0) return r;
            }
            if (Relations != null)
            {
                foreach (var rel in Relations)
                {
                    var r = rel.ObjectTargetMapNode.SourceNameToAlias(sourceNameIn);
                    if (r.Length > 0) return r;
                }
            }
            return "";
        }

        public bool UseAsPK(TargetFieldInfo info)
        {
            if (info.SourceName == SourceName) return true;
            return Generalisation?.UseAsPK(info) ?? false;
        }

        // ── Private helpers ───────────────────────────────────────────────────────────

        private FieldBinding PopulateFieldInfo(FieldInfo fi, RecordBase obj, DataConnection connection, bool targetExists, RecordBase changedObject)
        {
            bool optionalMissing = false;
            if (!ShouldIncludeField(fi, obj, changedObject)) return null;

            string targetName  = ResolveTargetFieldName(fi);
            var    generatorAttr = CustomAttributeCache.GetFieldAttribute(fi, typeof(GeneratorAttribute), false) as GeneratorAttribute;
            var    mapperAttr    = CustomAttributeCache.GetFieldAttribute(fi, typeof(FieldMappingAttribute), false) as FieldMappingAttribute;
            var    encAttr       = CustomAttributeCache.GetFieldAttribute(fi, typeof(EncryptedAttribute), false) as EncryptedAttribute;
            var    readOnlyAttr  = CustomAttributeCache.GetFieldAttribute(fi, typeof(ReadOnlyAttribute), false) as ReadOnlyAttribute;

            TargetFieldInfo info;
            if (targetExists)
            {
                info = connection.GetTargetFieldInfo(Class.FullName, SourceName, targetName);
                if (info == null)
                {
                    if (CustomAttributeCache.GetFieldAttribute(fi, typeof(OptionalAttribute), false) != null)
                    {
                        optionalMissing = true;
                    }
                    else
                    {
                        throw new PersistenceException($"No DB field info for: {Class.FullName}.{targetName}");
                    }
                }

                if (!optionalMissing)
                {
                    if (generatorAttr != null)
                    {
                        info.IsAutoGenerated = true;
                        info.GeneratorName   = generatorAttr.GeneratorName.Length > 0 ? generatorAttr.GeneratorName : "G_" + SourceName;
                    }
                    if (mapperAttr != null)
                    {
                        if (!mapperAttr.MapperType.IsSubclassOf(typeof(IDBFieldMapper)))
                            throw new PersistenceException($"Field mapper type '{mapperAttr.MapperType.FullName}' does not implement IDBFieldMapper");
                        info.FieldMapper = (IDBFieldMapper)Activator.CreateInstance(mapperAttr.MapperType);
                    }
                    if (encAttr != null)
                    {
                        info.Encryption = EncryptionAlgorithm.CreateAlgorithm(encAttr, SourceName, targetName);
                    }
                    if (readOnlyAttr != null)
                    {
                        info.IsReadOnly = true;
                    }
                }
            }
            else
            {
                info = new TargetFieldInfo
                {
                    IsInPK         = false,
                    TargetType     = fi.FieldType,
                    IsIdentity     = false,
                    IsAutoGenerated = false,
                    GeneratorName  = "",
                    SourceName     = SourceName,
                };
            }

            if (optionalMissing) return null;

            if (info.IsInPK || ShouldIncludeField(fi, obj, changedObject))
            {
                var fb     = new FieldBinding();
                fb.Info    = info;
                fb.MapNode = this;
                fb.Alias   = AliasGenerator?.NewAlias() ?? "";
                info.FieldInfo  = fi;
                info.FieldName  = fi.Name;
                info.TargetName = targetName;
                return fb;
            }
            return null;
        }

        private bool ShouldIncludeField(FieldInfo fi, RecordBase obj, RecordBase changedObject)
        {
            if (changedObject == null) return true;
            var v1 = fi.GetValue(obj);
            var v2 = fi.GetValue(changedObject);
            if (v1 is TField f1 && v2 is TField f2)
            {
                if (!f1.IsLoaded()) return false;
                if (f1.IsNull() != f2.IsNull()) return true;
                if (!f1.IsNull() && f1.ToString() != f2.ToString()) return true;
                return false;
            }
            if (v1 == null && v2 == null) return false;
            return v1?.ToString() != v2?.ToString();
        }

        private bool AllowedDeclaringClass(Type declaringType)
        {
            if (IsDBDerived)
                return ClassesInNode.Contains(declaringType);

            Type test = Class;
            while (test != null && test.FullName != "ActiveForge.Record")
            {
                if (declaringType == test) return true;
                test = test.BaseType;
            }
            return false;
        }

        private string ResolveTargetFieldName(FieldInfo fi)
        {
            var col = CustomAttributeCache.GetFieldAttribute(fi, typeof(ColumnAttribute), false) as ColumnAttribute;
            if (col != null) return col.ColumnName;

            // Strip hungarian-notation prefix (lowercase letters before first uppercase/digit)
            var    chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            int    start = fi.Name.IndexOfAny("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray());
            return start < 0 ? fi.Name : fi.Name.Substring(start);
        }

        private string GetSourceNameForAliasLookup(Type type)
        {
            var src = CustomAttributeCache.GetClassAttribute(type, typeof(TableAttribute), false) as TableAttribute;
            return src != null ? src.SourceName : type.Name;
        }

        private FieldInfo FindFieldByName(FieldInfo[] fieldList, string name)
        {
            foreach (var fi in fieldList)
                if (fi.Name == name) return fi;
            return null;
        }

        protected Stack<Type> InheritanceStack(Type startClass)
        {
            var  stack        = new Stack<Type>();
            bool dbDerivedFound = false;

            Type cur = startClass;
            do
            {
                if (!IsIntermediateClass(cur, dbDerivedFound))
                    stack.Push(cur);

                if (CustomAttributeCache.GetClassAttribute(cur, _computedAttributeType, false) != null)
                    dbDerivedFound = true;

                bool hasBase = CustomAttributeCache.GetClassAttribute(cur, _baseTableAttributeType, false) != null;
                cur = cur.BaseType;

                if (hasBase || !dbDerivedFound) break;
            }
            while (cur != null);

            return stack;
        }

        // ── Helper inner type ─────────────────────────────────────────────────────────

        protected class FieldInfoContainer
        {
            public string    FieldTypeName;
            public string    FieldName;
            public FieldInfo FieldInfo;
            public FieldInfoContainer(string typeName, string name, FieldInfo fi)
            {
                FieldTypeName = typeName;
                FieldName     = name;
                FieldInfo     = fi;
            }
        }
    }

    // ── Supporting specification classes ─────────────────────────────────────────────

    public class NodeJoin
    {
        public FieldInfo            KeyFieldInfo         = null;
        public RecordBindingMapNode ObjectTargetMapNode  = null;

        public NodeJoin(FieldInfo keyFI, RecordBindingMapNode targetNode)
        {
            KeyFieldInfo        = keyFI;
            ObjectTargetMapNode = targetNode;
        }
    }

    public class RelationshipSpecification : NodeJoin
    {
        public FieldInfo ObjectTargetFieldInfo = null;
        public string    KeyFieldAlias;
        public string    TargetPrimaryKeyField;

        public RelationshipSpecification(FieldInfo keyFI, string keyAlias, FieldInfo targetFI, string targetPKField, RecordBindingMapNode targetNode)
            : base(keyFI, targetNode)
        {
            ObjectTargetFieldInfo = targetFI;
            KeyFieldAlias         = keyAlias;
            TargetPrimaryKeyField = targetPKField;
        }
    }

    public class PolymorphicJoinSpec : NodeJoin
    {
        public PolymorphicJoinSpec(FieldInfo keyFI, RecordBindingMapNode targetNode)
            : base(keyFI, targetNode) { }
    }

    /// <summary>
    /// Generates unique short SQL aliases (consonant-only to avoid SQL keywords).
    /// </summary>
    public class AliasGenerator
    {
        private int _i1, _i2, _i3, _i4, _i5, _i6;

        private static readonly char[] _chars = {
            ' ', 'B', 'C', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
            'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Z',
        };

        public string NewAlias()
        {
            _i1++;
            if (_i1 == _chars.Length) { _i1 = 1; _i2++;
            if (_i2 == _chars.Length) { _i2 = 1; _i3++;
            if (_i3 == _chars.Length) { _i3 = 1; _i4++;
            if (_i4 == _chars.Length) { _i4 = 1; _i5++;
            if (_i5 == _chars.Length) { _i5 = 1; _i6++;
            if (_i6 == _chars.Length) throw new PersistenceException("AliasGenerator exhausted"); } } } } }

            return (_chars[_i5].ToString() + _chars[_i4] + _chars[_i3] + _chars[_i2] + _chars[_i1]).Trim();
        }
    }
}
