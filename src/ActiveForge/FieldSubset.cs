using System.Collections.Generic;
using System.Reflection;

namespace ActiveForge
{
    /// <summary>
    /// Controls which fields are included in a SELECT or UPDATE operation.
    /// Supports field-level and join-level inclusion/exclusion, composable via
    /// <c>+</c>, <c>&amp;</c>, <c>|</c>, and <c>-</c> operators.
    /// </summary>
    public class FieldSubset
    {
        private static readonly System.Type _primaryKeyType = typeof(TPrimaryKey);

        protected BaseFactory  Factory          = null;
        protected System.Type  RootObjectType   = null;
        protected Dictionary<string, FieldInclude> Fields = new Dictionary<string, FieldInclude>();
        protected Dictionary<string, JoinInclude>  Joins  = new Dictionary<string, JoinInclude>();

        public enum InitialState
        {
            IncludeAll      = 1,
            ExcludeAll      = 2,
            Default         = 3,
            IncludeAllJoins = 4,
            ExcludeAllJoins = 5,
        }

        // ── Constructors ─────────────────────────────────────────────────────────────

        /// <summary>Excludes all fields initially (caller adds what's needed).</summary>
        public FieldSubset(BaseRecord rootObject, BaseFactory factory)
            : this(rootObject, InitialState.ExcludeAll, factory) { }

        public FieldSubset(BaseRecord rootObject, InitialState state, BaseFactory factory)
        {
            Factory = factory;
            Populate(rootObject, state);
        }

        protected FieldSubset(FieldSubset source)
        {
            RootObjectType = source.RootObjectType;
            Factory        = source.Factory;
            foreach (var pair in source.Fields)
                Fields.Add(pair.Key, new FieldInclude(pair.Value));
            foreach (var pair in source.Joins)
                Joins.Add(pair.Key, new JoinInclude(pair.Value));
        }

        // ── Operators ────────────────────────────────────────────────────────────────

        /// <summary>Union — includes any field included in either operand.</summary>
        public static FieldSubset operator +(FieldSubset a, FieldSubset b)
        {
            CheckCompatible(a, b);
            var result = new FieldSubset(a);
            foreach (var pair in result.Fields)
            {
                if (b.Fields.TryGetValue(pair.Key, out var bInc) && bInc.Include)
                    pair.Value.Include = true;
            }
            foreach (var pair in result.Joins)
            {
                if (b.Joins.TryGetValue(pair.Key, out var bInc) && bInc.Include)
                {
                    pair.Value.Include      = true;
                    pair.Value.FieldSubset += bInc.FieldSubset;
                }
            }
            return result;
        }

        /// <summary>Intersection — includes only fields included in both operands.</summary>
        public static FieldSubset operator &(FieldSubset a, FieldSubset b)
        {
            CheckCompatible(a, b);
            var result = new FieldSubset(a);
            foreach (var pair in result.Fields)
            {
                if (b.Fields.TryGetValue(pair.Key, out var bInc))
                    pair.Value.Include = pair.Value.Include & bInc.Include;
            }
            foreach (var pair in result.Joins)
            {
                if (b.Joins.TryGetValue(pair.Key, out var bInc))
                {
                    pair.Value.Include      = pair.Value.Include & bInc.Include;
                    pair.Value.FieldSubset &= bInc.FieldSubset;
                }
            }
            return result;
        }

        /// <summary>OR union.</summary>
        public static FieldSubset operator |(FieldSubset a, FieldSubset b)
        {
            CheckCompatible(a, b);
            var result = new FieldSubset(a);
            foreach (var pair in result.Fields)
            {
                if (b.Fields.TryGetValue(pair.Key, out var bInc))
                    pair.Value.Include = pair.Value.Include | bInc.Include;
            }
            foreach (var pair in result.Joins)
            {
                if (b.Joins.TryGetValue(pair.Key, out var bInc))
                {
                    pair.Value.Include      = pair.Value.Include | bInc.Include;
                    pair.Value.FieldSubset |= bInc.FieldSubset;
                }
            }
            return result;
        }

        /// <summary>Subtraction — removes any fields that are in <paramref name="b"/>.</summary>
        public static FieldSubset operator -(FieldSubset a, FieldSubset b)
        {
            CheckCompatible(a, b);
            var result = new FieldSubset(a);
            foreach (var pair in result.Fields)
            {
                if (b.Fields.TryGetValue(pair.Key, out var bInc) && bInc.Include)
                    pair.Value.Include = false;
            }
            foreach (var pair in result.Joins)
            {
                if (b.Joins.TryGetValue(pair.Key, out var bInc) && bInc.Include)
                {
                    pair.Value.FieldSubset -= bInc.FieldSubset;
                    pair.Value.Include      = !pair.Value.FieldSubset.AllExcluded();
                }
            }
            return result;
        }

        // ── Add (in-place union) ─────────────────────────────────────────────────────

        public FieldSubset Add(params FieldSubset[] subsets)
        {
            foreach (var subset in subsets)
            {
                foreach (var pair in subset.Fields)
                {
                    if (pair.Value.Include && Fields.TryGetValue(pair.Key, out var fi))
                        fi.Include = true;
                }
            }
            return this;
        }

        // ── Inclusion queries ────────────────────────────────────────────────────────

        public bool IncludesField(string fieldName)
        {
            if (Fields.TryGetValue(fieldName, out var fi))
                return fi.Include;
            return true; // unknown field → include by default (derived class scenario)
        }

        public bool IncludesField(FieldBinding fieldBinding)
        {
            if (RootObjectType == fieldBinding.MapNode.Class)
            {
                if (Fields.TryGetValue(fieldBinding.Info.FieldName, out var fi))
                    return fi.Include;
                return false;
            }
            foreach (var pair in Joins)
            {
                if (pair.Value.Include)
                {
                    if (pair.Value.FieldSubset.IncludesField(fieldBinding))
                        return true;
                }
            }
            return false;
        }

        public bool IncludesJoin(string objectBaseName)
        {
            if (Joins.TryGetValue(objectBaseName, out var ji))
                return ji.Include;
            return true;
        }

        public FieldSubset FieldSubsetForJoin(string objectBaseName)
        {
            if (Joins.TryGetValue(objectBaseName, out var ji))
                return ji.FieldSubset;
            return null;
        }

        public bool IncludesFieldPath(System.Collections.Generic.Stack<string> path)
        {
            bool finished = false;
            return InnerIncludesFieldPath(path, ref finished);
        }

        protected bool InnerIncludesFieldPath(Stack<string> path, ref bool finished)
        {
            string component = path.Pop();
            if (path.Count == 0)
            {
                if (Fields.TryGetValue(component, out var fi))
                {
                    finished = true;
                    return fi.Include;
                }
                return false;
            }
            else
            {
                if (Joins.TryGetValue(component, out var ji) && ji.Include)
                    return ji.FieldSubset.InnerIncludesFieldPath(path, ref finished);
                finished = true;
                return false;
            }
        }

        public bool IncludesField(BaseRecord root, BaseRecord enclosing, TField field)
        {
            bool complete = false;
            return IncludesFieldInternal(root, enclosing, field, ref complete);
        }

        private bool IncludesFieldInternal(BaseRecord root, BaseRecord enclosing, TField field, ref bool complete)
        {
            if (root == enclosing)
            {
                foreach (var pair in Fields)
                {
                    TField testField = (TField)pair.Value.FieldInfo.GetValue(enclosing);
                    if (ReferenceEquals(testField, field))
                    {
                        complete = true;
                        return pair.Value.Include;
                    }
                }
            }
            else
            {
                foreach (var pair in Joins)
                {
                    if (pair.Value.Include)
                    {
                        var nextRoot = (BaseRecord)pair.Value.FieldInfo.GetValue(root);
                        bool result  = pair.Value.FieldSubset.IncludesFieldInternal(nextRoot, enclosing, field, ref complete);
                        if (complete) return result;
                    }
                }
            }
            return false;
        }

        public bool IncludesEmbeddedObject(BaseRecord root, BaseRecord enclosing, BaseRecord embedded)
        {
            bool complete = false;
            return IncludesEmbeddedObjectInternal(root, enclosing, embedded, ref complete);
        }

        private bool IncludesEmbeddedObjectInternal(BaseRecord root, BaseRecord enclosing, BaseRecord embedded, ref bool complete)
        {
            if (root == enclosing)
            {
                foreach (var pair in Joins)
                {
                    if (pair.Value.Include)
                    {
                        var testObj = (BaseRecord)pair.Value.FieldInfo.GetValue(root);
                        if (testObj == embedded)
                        {
                            complete = true;
                            return true;
                        }
                    }
                }
            }
            else
            {
                foreach (var pair in Joins)
                {
                    if (pair.Value.Include)
                    {
                        var nextRoot = (BaseRecord)pair.Value.FieldInfo.GetValue(root);
                        bool result  = pair.Value.FieldSubset.IncludesEmbeddedObjectInternal(nextRoot, enclosing, embedded, ref complete);
                        if (complete) return result;
                    }
                }
            }
            return false;
        }

        // ── Bulk operations ──────────────────────────────────────────────────────────

        public bool AllIncluded()
        {
            foreach (var pair in Fields)
                if (!pair.Value.Include) return false;
            foreach (var pair in Joins)
                if (!pair.Value.FieldSubset.AllIncluded()) return false;
            return true;
        }

        protected bool AllExcluded()
        {
            foreach (var pair in Fields)
                if (pair.Value.Include) return false;
            foreach (var pair in Joins)
                if (!pair.Value.FieldSubset.AllExcluded()) return false;
            return true;
        }

        public int CountIncludedFields()
        {
            int count = 0;
            foreach (var pair in Fields)
                if (pair.Value.Include) count++;
            foreach (var pair in Joins)
                count += pair.Value.FieldSubset.CountIncludedFields();
            return count;
        }

        public void IncludeAll(bool include)
        {
            foreach (var pair in Fields)  pair.Value.Include = include;
            foreach (var pair in Joins)
            {
                pair.Value.Include = include;
                pair.Value.FieldSubset.IncludeAll(include);
            }
        }

        public void IncludeAll(InitialState state)
        {
            foreach (var pair in Fields)
                pair.Value.Include = IncludeFieldInLoad(pair.Value.FieldInfo, state);
            foreach (var pair in Joins)
            {
                pair.Value.Include = IncludeFieldInLoad(pair.Value.FieldInfo, state);
                if (pair.Value.Include)
                    pair.Value.FieldSubset.IncludeAll(state);
                else
                    pair.Value.FieldSubset.IncludeAll(false);
            }
        }

        // ── Individual include / exclude ─────────────────────────────────────────────

        public void Include(BaseRecord root, BaseRecord enclosing, TField field)
        {
            var rc = IncludeField(root, enclosing, field, true);
            if (!rc) throw new PersistenceException("Error including field in FieldSubset: " + rc.GetValue());
        }

        public void Exclude(BaseRecord root, BaseRecord enclosing, TField field)
        {
            var rc = IncludeField(root, enclosing, field, false);
            if (!rc) throw new PersistenceException("Error excluding field in FieldSubset: " + rc.GetValue());
        }

        public void Include(BaseRecord root, BaseRecord enclosing, BaseRecord joinTarget)
            => Include(root, enclosing, joinTarget, InitialState.Default);

        public void Include(BaseRecord root, BaseRecord enclosing, BaseRecord joinTarget, InitialState state)
        {
            var rc = IncludeJoin(root, enclosing, joinTarget, true, state);
            if (!rc) throw new PersistenceException(
                $"Trying to include {enclosing.GetType().Name} in {root.GetType().Name} — {rc}");
        }

        public void Exclude(BaseRecord root, BaseRecord enclosing, BaseRecord joinTarget)
        {
            var rc = IncludeJoin(root, enclosing, joinTarget, false, InitialState.IncludeAll);
            if (!rc) throw new PersistenceException("Error excluding join in FieldSubset: " + rc.GetValue());
        }

        private ReturnCode IncludeField(BaseRecord root, BaseRecord enclosing, TField field, bool include)
        {
            var rc = new ReturnCode(ReturnCode.Value.ObjectNotFound);
            var meta = RecordMetaDataCache.GetTypeMetaData(RootObjectType);
            if (root == enclosing)
            {
                rc.SetValue(ReturnCode.Value.FieldNotFound);
                foreach (var entry in meta.TFields)
                {
                    if (ReferenceEquals(field, entry.FieldInfo.GetValue(root)))
                    {
                        if (Fields.TryGetValue(entry.Name, out var fi))
                        {
                            fi.Include = include;
                            rc.SetValue(ReturnCode.Value.NoError);
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var entry in meta.DataObjects)
                {
                    var nextObj = (BaseRecord)entry.FieldInfo.GetValue(root);
                    if (Joins.TryGetValue(entry.Name, out var ji))
                    {
                        rc = ji.FieldSubset.IncludeField(nextObj, enclosing, field, include);
                        if (rc)
                        {
                            ji.Include = true;
                            break;
                        }
                    }
                }
            }
            return rc;
        }

        private ReturnCode IncludeJoin(BaseRecord root, BaseRecord enclosing, BaseRecord target, bool include, InitialState state)
        {
            var rc   = new ReturnCode(ReturnCode.Value.ObjectNotFound);
            var meta = RecordMetaDataCache.GetTypeMetaData(RootObjectType);
            if (root == enclosing)
            {
                rc.SetValue(ReturnCode.Value.JoinNotFound);
                foreach (var entry in meta.DataObjects)
                {
                    var nextObj = (BaseRecord)entry.FieldInfo.GetValue(root);
                    if (nextObj == target)
                    {
                        if (Joins.TryGetValue(entry.Name, out var ji))
                        {
                            ji.Include = include;
                            if (!include)
                                ji.FieldSubset.IncludeAll(false);
                            else
                                ji.FieldSubset.IncludeAll(state);
                            rc.SetValue(ReturnCode.Value.NoError);
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var entry in meta.DataObjects)
                {
                    var nextObj = (BaseRecord)entry.FieldInfo.GetValue(root);
                    if (Joins.TryGetValue(entry.Name, out var ji))
                    {
                        rc = ji.FieldSubset.IncludeJoin(nextObj, enclosing, target, include, state);
                        if (rc)
                        {
                            ji.Include = true;
                            break;
                        }
                    }
                }
            }
            return rc;
        }

        // ── PK helpers ───────────────────────────────────────────────────────────────

        public void EnsurePrimaryKeysIncluded()
        {
            foreach (var pair in Fields)
            {
                if (pair.Value.FieldType == _primaryKeyType || pair.Value.FieldType.IsSubclassOf(_primaryKeyType))
                    pair.Value.Include = true;
            }
            foreach (var pair in Joins)
            {
                if (pair.Value.Include)
                    pair.Value.FieldSubset.EnsurePrimaryKeysIncluded();
            }
        }

        // ── Embedded object navigation ───────────────────────────────────────────────

        public FieldSubset FieldSubsetForEmbeddedObject(BaseRecord root, BaseRecord embedded)
        {
            if (root == embedded) return this;
            var meta = RecordMetaDataCache.GetTypeMetaData(RootObjectType);
            foreach (var entry in meta.DataObjects)
            {
                var nextObj = (BaseRecord)entry.FieldInfo.GetValue(root);
                if (Joins.TryGetValue(entry.Name, out var ji))
                {
                    var result = ji.FieldSubset.FieldSubsetForEmbeddedObject(nextObj, embedded);
                    if (result != null) return result;
                }
            }
            return null;
        }

        public System.Collections.Generic.IEnumerable<BaseRecord> IncludedObjects(BaseRecord root, BaseRecord enclosing)
        {
            int count = 0;
            foreach (var pair in Fields)
                if (pair.Value.Include) { count++; break; }
            if (count > 0) yield return enclosing;

            foreach (var pair in Joins)
            {
                if (pair.Value.Include)
                {
                    var next = (BaseRecord)pair.Value.FieldInfo.GetValue(enclosing);
                    foreach (var obj in pair.Value.FieldSubset.IncludedObjects(root, next))
                        yield return obj;
                }
            }
        }

        public System.Collections.Generic.List<IncludedField> GetIncludedFields(BaseRecord root)
        {
            var result = new System.Collections.Generic.List<IncludedField>();
            GetIncludedFieldsInternal(root, root, result);
            return result;
        }

        private void GetIncludedFieldsInternal(BaseRecord root, BaseRecord enclosing, System.Collections.Generic.List<IncludedField> result)
        {
            foreach (var pair in Fields)
            {
                if (pair.Value.Include)
                {
                    result.Add(new IncludedField
                    {
                        Field          = (TField)pair.Value.FieldInfo.GetValue(enclosing),
                        RootObject     = root,
                        EnclosingObject = enclosing
                    });
                }
            }
            foreach (var pair in Joins)
            {
                if (pair.Value.Include)
                {
                    var next = (BaseRecord)pair.Value.FieldInfo.GetValue(root);
                    pair.Value.FieldSubset.GetIncludedFieldsInternal(root, next, result);
                }
            }
        }

        // ── Populate from metadata ───────────────────────────────────────────────────

        private void Populate(BaseRecord root, InitialState state)
        {
            RootObjectType = root.GetType();
            var meta = RecordMetaDataCache.GetTypeMetaData(RootObjectType);

            foreach (var entry in meta.DataObjects)
            {
                bool include = IncludeFieldInLoad(entry.FieldInfo, state);
                Joins.Add(entry.Name, new JoinInclude(include, state, (BaseRecord)entry.FieldInfo.GetValue(root), Factory, entry.FieldInfo));
            }
            foreach (var entry in meta.TFields)
            {
                bool include = IncludeFieldInLoad(entry.FieldInfo, state);
                Fields.Add(entry.Name, new FieldInclude(include, entry.FieldInfo.FieldType, entry.FieldInfo));
            }
        }

        private bool IncludeFieldInLoad(FieldInfo fi, InitialState state)
        {
            if (state == InitialState.ExcludeAll)  return false;
            if (state == InitialState.IncludeAll)  return true;
            if (state == InitialState.Default)     return IsDefaultLoadTrue(fi);
            if (state == InitialState.IncludeAllJoins)
                return fi.FieldType.IsSubclassOf(typeof(Record)) ? true : IsDefaultLoadTrue(fi);
            if (state == InitialState.ExcludeAllJoins)
                return fi.FieldType.IsSubclassOf(typeof(Record)) ? false : IsDefaultLoadTrue(fi);
            return true;
        }

        private bool IsDefaultLoadTrue(FieldInfo fi)
        {
            var attrs = CustomAttributeCache.GetFieldAttributes(fi, typeof(Attributes.EagerLoadAttribute), true);
            if (attrs != null && attrs.Length > 0)
                return ((Attributes.EagerLoadAttribute)attrs[0]).Load;
            return true;
        }

        private static void CheckCompatible(FieldSubset a, FieldSubset b)
        {
            if (a.RootObjectType != b.RootObjectType)
                throw new PersistenceException(
                    $"Incompatible FieldSubset root types: {a.RootObjectType?.Name} and {b.RootObjectType?.Name}");
        }

        // ── Inner types ──────────────────────────────────────────────────────────────

        public class IncludedField
        {
            public TField     Field;
            public BaseRecord RootObject;
            public BaseRecord EnclosingObject;
        }

        protected class FieldInclude
        {
            public bool      Include;
            public System.Type FieldType;
            public FieldInfo FieldInfo;

            public FieldInclude(bool include, System.Type fieldType, FieldInfo fieldInfo)
            {
                Include   = include;
                FieldType = fieldType;
                FieldInfo = fieldInfo;
            }
            public FieldInclude(FieldInclude source)
            {
                Include   = source.Include;
                FieldType = source.FieldType;
                FieldInfo = source.FieldInfo;
            }

            public static implicit operator bool(FieldInclude fi) => fi.Include;
        }

        protected class JoinInclude
        {
            public bool        Include;
            public FieldInfo   FieldInfo;
            public FieldSubset FieldSubset;

            public JoinInclude(bool include, InitialState state, BaseRecord joinTarget, BaseFactory factory, FieldInfo fieldInfo)
            {
                Include    = include;
                FieldInfo  = fieldInfo;
                FieldSubset = new FieldSubset(joinTarget, include ? state : InitialState.ExcludeAll, factory);
            }

            public JoinInclude(JoinInclude source)
            {
                Include     = source.Include;
                FieldInfo   = source.FieldInfo;
                FieldSubset = new FieldSubset(source.FieldSubset);
            }

            public static implicit operator bool(JoinInclude ji) => ji.Include;
        }

        protected class ReturnCode
        {
            public enum Value { NoError, ObjectNotFound, FieldNotFound, JoinNotFound }
            private Value _value;
            public ReturnCode(Value v) { _value = v; }
            public void SetValue(Value v) { _value = v; }
            public Value GetValue() => _value;
            public static implicit operator bool(ReturnCode rc) => rc._value == Value.NoError;
            public override string ToString() => _value.ToString();
        }
    }
}
