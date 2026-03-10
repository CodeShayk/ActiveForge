using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using ActiveForge.Attributes;

namespace ActiveForge.MongoDB.Internal
{
    /// <summary>
    /// Cached descriptor for one TField member of a Record type.
    /// Maps C# field name → BSON field name and records identity/PK flags.
    /// </summary>
    internal sealed class MongoFieldDescriptor
    {
        public FieldInfo FieldInfo   { get; }
        public string    BsonName    { get; }
        public bool      IsIdentity  { get; }

        public MongoFieldDescriptor(FieldInfo fi, string bsonName, bool isIdentity)
        {
            FieldInfo  = fi;
            BsonName   = bsonName;
            IsIdentity = isIdentity;
        }
    }

    /// <summary>
    /// Per-type cache of MongoFieldDescriptors, built once via reflection.
    /// </summary>
    internal static class MongoTypeCache
    {
        private static readonly ConcurrentDictionary<Type, MongoTypeEntry> _cache
            = new ConcurrentDictionary<Type, MongoTypeEntry>();

        public static MongoTypeEntry GetEntry(Type type)
            => _cache.GetOrAdd(type, BuildEntry);

        private static MongoTypeEntry BuildEntry(Type type)
        {
            var meta = RecordMetaDataCache.GetTypeMetaData(type);

            string collectionName = meta.SourceName;

            var fields     = new List<MongoFieldDescriptor>();
            MongoFieldDescriptor? identity = null;

            foreach (var entry in meta.TFields)
            {
                FieldInfo fi = entry.FieldInfo;

                bool isIdentity = CustomAttributeCache.GetFieldAttribute(
                    fi, typeof(IdentityAttribute), false) != null;

                string bsonName;
                if (isIdentity)
                {
                    bsonName = "_id";
                }
                else
                {
                    var colAttr = CustomAttributeCache.GetFieldAttribute(
                        fi, typeof(ColumnAttribute), false) as ColumnAttribute;
                    bsonName = colAttr?.ColumnName ?? fi.Name;
                }

                var descriptor = new MongoFieldDescriptor(fi, bsonName, isIdentity);
                fields.Add(descriptor);

                if (isIdentity)
                    identity = descriptor;
            }

            return new MongoTypeEntry(collectionName, fields, identity);
        }
    }

    internal sealed class MongoTypeEntry
    {
        public string                        CollectionName { get; }
        public IReadOnlyList<MongoFieldDescriptor> Fields  { get; }
        public MongoFieldDescriptor?         Identity       { get; }

        public MongoTypeEntry(string collectionName,
                              List<MongoFieldDescriptor> fields,
                              MongoFieldDescriptor? identity)
        {
            CollectionName = collectionName;
            Fields         = fields;
            Identity       = identity;
        }
    }
}
