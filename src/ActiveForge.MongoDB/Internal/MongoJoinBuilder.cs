using System;
using System.Collections.Generic;
using System.Reflection;
using ActiveForge.Attributes;

namespace ActiveForge.MongoDB.Internal
{
    /// <summary>
    /// Metadata for a single MongoDB <c>$lookup</c> join stage derived from an embedded
    /// <see cref="Record"/> field on a root Record type.
    /// </summary>
    internal sealed class MongoJoinStage
    {
        /// <summary>The reflected field on the root Record that holds the embedded Record.</summary>
        public FieldInfo EmbeddedFieldInfo { get; init; } = null!;

        /// <summary>CLR type of the embedded Record.</summary>
        public Type EmbeddedType { get; init; } = null!;

        /// <summary>MongoDB collection name for the joined type.</summary>
        public string CollectionName { get; init; } = "";

        /// <summary>BSON field name in the root document that is the foreign key.</summary>
        public string LocalField { get; init; } = "";

        /// <summary>BSON field name in the joined collection (usually <c>_id</c>).</summary>
        public string ForeignField { get; init; } = "";

        /// <summary>Alias key under which the joined sub-document is stored after <c>$unwind</c>.</summary>
        public string Alias { get; init; } = "";

        /// <summary>
        /// When <c>true</c> the <c>$unwind</c> stage uses
        /// <c>preserveNullAndEmptyArrays = true</c> (LEFT OUTER JOIN semantics).
        /// When <c>false</c> unmatched root documents are removed (INNER JOIN semantics).
        /// </summary>
        public bool IsLeftJoin { get; init; } = true;
    }

    /// <summary>
    /// Builds a list of <see cref="MongoJoinStage"/> descriptors by scanning a root
    /// Record type for public fields of a Record sub-type.
    /// <para>
    /// Convention: for an embedded field named <c>Product</c> the builder looks for a
    /// sibling <c>TForeignKey</c>/<c>TInt</c> field named <c>ProductID</c> or <c>ProductId</c>.
    /// Override the convention by decorating the embedded field with <see cref="JoinAttribute"/>.
    /// </para>
    /// </summary>
    internal static class MongoJoinBuilder
    {
        /// <summary>
        /// Scans <paramref name="rootType"/> for embedded Record fields and returns one
        /// <see cref="MongoJoinStage"/> per resolvable join relationship.
        /// </summary>
        /// <param name="rootType">Root Record type to inspect.</param>
        /// <param name="overrides">
        /// Optional list of <see cref="JoinOverride"/> values from the LINQ provider
        /// (<c>.InnerJoin&lt;T&gt;()</c> / <c>.LeftOuterJoin&lt;T&gt;()</c>).
        /// When a match is found the override's join type takes precedence over the
        /// <see cref="JoinAttribute"/> value.
        /// </param>
        public static List<MongoJoinStage> BuildStages(
            Type rootType,
            IReadOnlyList<JoinOverride>? overrides = null)
        {
            var stages = new List<MongoJoinStage>();

            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (FieldInfo fi in rootType.GetFields(bf))
            {
                if (!typeof(Record).IsAssignableFrom(fi.FieldType)) continue;
                if (fi.FieldType == typeof(Record))                  continue; // skip raw base-type refs

                var joinAttr = fi.GetCustomAttribute<JoinAttribute>();

                string localField;
                string foreignField = "_id";
                bool   isLeftJoin   = true;  // default: LEFT OUTER

                if (joinAttr != null && !string.IsNullOrEmpty(joinAttr.ForeignKey))
                {
                    localField   = joinAttr.ForeignKey;
                    foreignField = string.IsNullOrEmpty(joinAttr.TargetField) ? "_id" : joinAttr.TargetField;
                    isLeftJoin   = joinAttr.JoinType != JoinAttribute.JoinTypeEnum.InnerJoin;
                }
                else
                {
                    // Convention: look for TForeignKey/TInt field named <FieldName>ID or <FieldName>Id
                    FieldInfo? fkField = FindForeignKeyField(rootType, fi.Name);
                    if (fkField == null) continue;
                    localField = GetBsonColumnName(fkField);
                }

                // Apply JoinOverride if the caller specified one for this type
                if (overrides != null)
                {
                    foreach (var ov in overrides)
                    {
                        if (ov.TargetType == fi.FieldType)
                        {
                            isLeftJoin = ov.JoinType == JoinSpecification.JoinTypeEnum.LeftOuterJoin;
                            break;
                        }
                    }
                }

                // Resolve target collection name from MongoTypeCache
                string targetCollection;
                try   { targetCollection = MongoTypeCache.GetEntry(fi.FieldType).CollectionName; }
                catch { continue; }  // type not registered as a Mongo entity — skip

                stages.Add(new MongoJoinStage
                {
                    EmbeddedFieldInfo = fi,
                    EmbeddedType      = fi.FieldType,
                    CollectionName    = targetCollection,
                    LocalField        = localField,
                    ForeignField      = foreignField,
                    Alias             = "__joined_" + fi.Name,
                    IsLeftJoin        = isLeftJoin,
                });
            }

            return stages;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static FieldInfo? FindForeignKeyField(Type rootType, string embeddedFieldName)
        {
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (string candidate in new[] { embeddedFieldName + "ID", embeddedFieldName + "Id" })
            {
                FieldInfo? fi = rootType.GetField(candidate, bf);
                if (fi != null && typeof(TField).IsAssignableFrom(fi.FieldType))
                    return fi;
            }
            return null;
        }

        private static string GetBsonColumnName(FieldInfo fi)
        {
            var col = fi.GetCustomAttribute<ColumnAttribute>();
            return col != null ? col.ColumnName : fi.Name;
        }
    }
}
