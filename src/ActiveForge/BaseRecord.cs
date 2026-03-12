using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace ActiveForge
{
    /// <summary>
    /// Root base class for all persistent objects.
    /// Provides field-subset support, reflection helpers, and field description caching.
    /// </summary>
    [Serializable]
    public abstract class BaseRecord : IComparable
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
        public virtual FieldSubset FieldSubset(Record enclosing, TField enclosed) => null;
        public virtual FieldSubset FieldSubset(Record enclosing, Record enclosed) => null;
        public virtual FieldSubset FieldSubset(Record enclosing, Record enclosed, FieldSubset.InitialState state) => null;

        public virtual bool IsEquivalentConstrainedLinkedObject(Record comparison) => false;

        public virtual BaseRecordParameterCollection GetDefaultObjectParameters() => null;

        // ── Field description helpers ─────────────────────────────────────────────────

        public virtual string GetFieldDescription(FieldInfo fi)
        {
            var entry = _fieldDescCache.GetOrAdd(fi, f => new FieldDescriptionEntry(f));
            return entry.Description;
        }

        public virtual string GetFieldDescription(TField field)
        {
            var meta = RecordMetaDataCache.GetTypeMetaData(GetType());
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

        public virtual BaseRecord CreateCompatibleObject()
            => (BaseRecord)Activator.CreateInstance(GetType());

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
