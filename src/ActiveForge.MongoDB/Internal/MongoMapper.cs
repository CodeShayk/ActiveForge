using System;
using System.Collections.Generic;
using MongoDB.Bson;

namespace ActiveForge.MongoDB.Internal
{
    /// <summary>
    /// Converts DataObject instances to/from BsonDocuments using TField reflection.
    /// </summary>
    internal static class MongoMapper
    {
        // ── DataObject → BsonDocument ─────────────────────────────────────────────────

        public static BsonDocument ToBsonDocument(DataObject obj, bool includeIdentity = true)
        {
            var entry = MongoTypeCache.GetEntry(obj.GetType());
            var doc   = new BsonDocument();

            foreach (var fd in entry.Fields)
            {
                if (fd.IsIdentity && !includeIdentity) continue;

                TField? field = fd.FieldInfo.GetValue(obj) as TField;
                if (field == null || field.IsNull()) continue;

                object? clrValue = GetClrValue(field);
                doc[fd.BsonName] = ClrToBson(clrValue);
            }

            return doc;
        }

        // ── BsonDocument → DataObject ─────────────────────────────────────────────────

        public static void FromBsonDocument(BsonDocument doc, DataObject obj)
        {
            var entry = MongoTypeCache.GetEntry(obj.GetType());

            foreach (var fd in entry.Fields)
            {
                if (!doc.Contains(fd.BsonName)) continue;

                BsonValue bson = doc[fd.BsonName];
                if (bson == BsonNull.Value) continue;

                TField? field = fd.FieldInfo.GetValue(obj) as TField;
                if (field == null) continue;

                object? clrValue = BsonToClr(bson, field.GetType());
                if (clrValue != null)
                {
                    field.SetValue(clrValue);
                    field.SetLoaded(true);
                }
            }
        }

        // ── BsonDocument → DataObject (with joined sub-documents) ────────────────────

        /// <summary>
        /// Maps a BsonDocument returned by an aggregation pipeline (which may contain
        /// joined sub-documents stored under their <see cref="MongoJoinStage.Alias"/> key)
        /// to the root <paramref name="obj"/> and its embedded DataObject fields.
        /// </summary>
        public static void FromBsonDocumentWithJoins(BsonDocument doc, DataObject obj, IReadOnlyList<MongoJoinStage> joinStages)
        {
            // Map root fields
            FromBsonDocument(doc, obj);

            // Map each joined sub-document into the corresponding embedded DataObject field
            foreach (var stage in joinStages)
            {
                if (!doc.Contains(stage.Alias)) continue;

                BsonValue joinedValue = doc[stage.Alias];
                if (joinedValue == BsonNull.Value || !(joinedValue is BsonDocument joinedBson)) continue;

                // Get or create the embedded DataObject
                DataObject? embedded = stage.EmbeddedFieldInfo.GetValue(obj) as DataObject;
                if (embedded == null)
                {
                    try   { embedded = (DataObject)Activator.CreateInstance(stage.EmbeddedType)!; }
                    catch { continue; }
                    stage.EmbeddedFieldInfo.SetValue(obj, embedded);
                }

                FromBsonDocument(joinedBson, embedded);
                embedded.SetLoaded(true);
            }
        }

        // ── Value helpers ─────────────────────────────────────────────────────────────

        private static object? GetClrValue(TField field)
        {
            try { return field.GetValue(); }
            catch { return null; }
        }

        public static BsonValue ClrToBson(object? value)
        {
            if (value == null) return BsonNull.Value;

            return value switch
            {
                int    i  => new BsonInt32(i),
                long   l  => new BsonInt64(l),
                double d  => new BsonDouble(d),
                decimal dc => new BsonDecimal128(dc),
                bool   b  => new BsonBoolean(b),
                string s  => new BsonString(s),
                DateTime dt => new BsonDateTime(dt),
                Guid   g  => new BsonString(g.ToString()),
                byte[] ba => new BsonBinaryData(ba),
                _         => new BsonString(value.ToString()!)
            };
        }

        public static object? BsonToClr(BsonValue bson, Type tFieldType)
        {
            if (bson == BsonNull.Value || bson == null) return null;

            // Determine target CLR type from TField type name
            string typeName = tFieldType.Name;

            try
            {
                return typeName switch
                {
                    "TInt"        or "TPrimaryKey" or "TForeignKey" or "TKey"
                        => bson.IsInt32  ? bson.AsInt32  :
                           bson.IsInt64  ? (int)bson.AsInt64 :
                           bson.IsString ? int.Parse(bson.AsString) :
                           Convert.ToInt32(bson),

                    "TLong"       => bson.IsInt64 ? bson.AsInt64 : Convert.ToInt64(bson),
                    "TDouble"     => bson.IsDouble ? bson.AsDouble : bson.ToDouble(),
                    "TFloat"      => bson.IsDouble ? (float)bson.AsDouble : Convert.ToSingle(bson),

                    "TDecimal"    => bson.IsDecimal128 ? (decimal)bson.AsDecimal128 :
                                     bson.IsDouble     ? (decimal)bson.AsDouble :
                                     Convert.ToDecimal(bson),

                    "TBool"       => bson.IsBoolean ? bson.AsBoolean : Convert.ToBoolean(bson),
                    "TString"     => bson.AsString,
                    "TDateTime"   => bson.IsString ? DateTime.Parse(bson.AsString) : bson.ToUniversalTime(),
                    "TDate"       => bson.IsString ? DateTime.Parse(bson.AsString) : bson.ToUniversalTime(),
                    "TTime"       => bson.IsString ? TimeSpan.Parse(bson.AsString) : TimeSpan.FromMilliseconds(bson.AsInt64),
                    "TGuid"       => Guid.Parse(bson.AsString),
                    "TByteArray"  => bson.AsByteArray,
                    _             => bson.ToString()
                };
            }
            catch
            {
                return null;
            }
        }

        // ── Build a minimal ObjectBinding for QueryTerm translation ───────────────────

        public static ObjectBinding BuildMinimalObjectBinding(DataObject obj)
        {
            var binding = new ObjectBinding();
            var entry   = MongoTypeCache.GetEntry(obj.GetType());
            var meta    = DataObjectMetaDataCache.GetTypeMetaData(obj.GetType());

            // Set SourceName
            binding.SourceName = entry.CollectionName;

            foreach (var fd in entry.Fields)
            {
                var tfi = new TargetFieldInfo
                {
                    FieldInfo   = fd.FieldInfo,
                    FieldName   = fd.FieldInfo.Name,
                    TargetName  = fd.BsonName,
                    SourceName  = entry.CollectionName,
                    IsIdentity  = fd.IsIdentity,
                    IsInPK      = fd.IsIdentity,
                    TargetType  = fd.FieldInfo.FieldType,
                    IsNullable  = true,
                };

                var fb = new FieldBinding
                {
                    Info      = tfi,
                    Alias     = "",
                    MapNode   = null!,   // never accessed in MongoDB path
                };

                binding.Fields.Add(fb);
                binding.UpdateFields.Add(fb);

                if (fd.IsIdentity)
                    binding.Identity = tfi;
            }

            return binding;
        }
    }
}
