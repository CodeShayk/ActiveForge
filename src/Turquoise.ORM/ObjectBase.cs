using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Turquoise.ORM
{
    /// <summary>
    /// Root base class for all persistent objects.
    /// Provides field-subset support, reflection helpers, and field description caching.
    /// </summary>
    [Serializable]
    public class ObjectBase : IComparable
    {
        // ── Static type caches ────────────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<FieldInfo, FieldDescriptionEntry> _fieldDescCache
            = new ConcurrentDictionary<FieldInfo, FieldDescriptionEntry>();

        // ── Virtual extension points ──────────────────────────────────────────────────

        public virtual Type[] GetConcreteTypes() => new[] { GetType() };

        public virtual string GetSourceName() => "";

        public virtual void SetDefaultFieldSubset(FieldSubset subset) { }
        public virtual void SetDefaultFieldSubset(FieldSubset.InitialState state) { }
        public virtual FieldSubset DefaultFieldSubset() => null;
        public virtual FieldSubset DefaultFieldSubset(Type[] expectedTypes) => null;
        public virtual bool IsExplicitDefaultFieldSubset() => false;

        public virtual FieldSubset FieldSubset(FieldSubset.InitialState includeAll) => null;
        public virtual FieldSubset FieldSubset(DataObject enclosing, TField enclosed) => null;
        public virtual FieldSubset FieldSubset(DataObject enclosing, DataObject enclosed) => null;
        public virtual FieldSubset FieldSubset(DataObject enclosing, DataObject enclosed, FieldSubset.InitialState state) => null;

        public virtual bool IsEquivalentConstrainedLinkedObject(DataObject comparison) => false;

        public virtual ObjectParameterCollectionBase GetDefaultObjectParameters() => null;

        // ── Field description helpers ─────────────────────────────────────────────────

        public virtual string GetFieldDescription(FieldInfo fi)
        {
            var entry = _fieldDescCache.GetOrAdd(fi, f => new FieldDescriptionEntry(f));
            return entry.Description;
        }

        public virtual string GetFieldDescription(TField field)
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(GetType());
            foreach (var entry in meta.TFields)
            {
                var testField = (TField)entry.FieldInfo.GetValue(this);
                if (ReferenceEquals(testField, field))
                    return GetFieldDescription(entry.FieldInfo);
            }
            throw new PersistenceException($"Field not found in {GetType().Name}");
        }

        public static bool GetFieldSensitive(FieldInfo fi)
        {
            var entry = _fieldDescCache.GetOrAdd(fi, f => new FieldDescriptionEntry(f));
            return entry.Sensitive;
        }

        public virtual ObjectBase CreateCompatibleObject()
            => (ObjectBase)Activator.CreateInstance(GetType());

        // ── IComparable ───────────────────────────────────────────────────────────────

        public virtual int CompareTo(object obj)
            => obj.GetHashCode().CompareTo(GetHashCode());

        public string AssemblyQualifiedName => GetType().AssemblyQualifiedName;

        // ── Inner cache type ──────────────────────────────────────────────────────────

        protected class FieldDescriptionEntry
        {
            public string Description { get; }
            public bool   Sensitive   { get; }

            public FieldDescriptionEntry(FieldInfo fi)
            {
                var descAttr = CustomAttributeCache.GetFieldAttribute(fi, typeof(Attributes.DescriptionAttribute), false)
                               as Attributes.DescriptionAttribute;
                Description = descAttr?.Description ?? fi.Name;

                var sensitiveAttr = CustomAttributeCache.GetFieldAttribute(fi, typeof(Attributes.SensitiveAttribute), false);
                Sensitive = sensitiveAttr != null;
            }
        }
    }
}
