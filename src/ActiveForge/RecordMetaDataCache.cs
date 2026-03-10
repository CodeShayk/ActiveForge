using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace ActiveForge
{
    /// <summary>
    /// Thread-safe per-type cache of reflected field metadata for Record subclasses.
    /// Avoids repeated expensive reflection on hot paths.
    /// </summary>
    public static class RecordMetaDataCache
    {
        private static readonly ConcurrentDictionary<Type, DataObjectMetaDataCacheEntry> _cache
            = new ConcurrentDictionary<Type, DataObjectMetaDataCacheEntry>();

        public static DataObjectMetaDataCacheEntry GetTypeMetaData(Type type)
            => _cache.GetOrAdd(type, t => new DataObjectMetaDataCacheEntry(t));

        // ── Cache entry ───────────────────────────────────────────────────────────────

        public class DataObjectMetaDataCacheEntry
        {
            private static readonly Type _tFieldType      = typeof(TField);
            private static readonly Type _dataObjectType  = typeof(Record);

            public List<FieldInfoCacheEntry> TFields     = new List<FieldInfoCacheEntry>();
            public List<FieldInfoCacheEntry> DataObjects = new List<FieldInfoCacheEntry>();
            public FieldInfo[]               AllFields;
            public string                    SourceName;
            public string                    ClassName;
            public string                    AssemblyQualifiedClassName;

            internal DataObjectMetaDataCacheEntry(Type type)
            {
                AllFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var fi in AllFields)
                {
                    if (fi.FieldType.IsSubclassOf(_tFieldType))
                        TFields.Add(new FieldInfoCacheEntry(fi));
                    else if (fi.FieldType.IsSubclassOf(_dataObjectType))
                        DataObjects.Add(new FieldInfoCacheEntry(fi));
                }

                AssemblyQualifiedClassName = type.AssemblyQualifiedName;
                ClassName  = type.Name;
                SourceName = ClassName;

                var src = CustomAttributeCache.GetClassAttribute(type, typeof(Attributes.TableAttribute), false) as Attributes.TableAttribute;
                if (src != null) SourceName = src.SourceName;
            }
        }

        public class FieldInfoCacheEntry
        {
            public FieldInfo FieldInfo;
            public string    Name;

            public FieldInfoCacheEntry(FieldInfo fi)
            {
                FieldInfo = fi;
                Name      = fi.Name;
            }
        }
    }
}
