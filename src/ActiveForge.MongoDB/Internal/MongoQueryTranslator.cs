using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using ActiveForge.Query;

namespace ActiveForge.MongoDB.Internal
{
    /// <summary>
    /// Translates ActiveForge QueryTerm predicates and SortOrder values
    /// into MongoDB FilterDefinition and SortDefinition respectively.
    /// </summary>
    internal static class MongoQueryTranslator
    {
        // ── Filter translation ────────────────────────────────────────────────────────

        public static FilterDefinition<BsonDocument> Translate(QueryTerm? term, Record obj,
            IReadOnlyList<MongoJoinStage>? joinStages = null)
        {
            if (term == null)
                return Builders<BsonDocument>.Filter.Empty;

            RecordBinding binding = MongoMapper.BuildMinimalObjectBinding(obj);
            return TranslateTerm(term, obj, binding, joinStages);
        }

        private static FilterDefinition<BsonDocument> TranslateTerm(
            QueryTerm term, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            return term switch
            {
                AndTerm and => TranslateAnd(and, obj, binding, joinStages),
                OrTerm  or  => TranslateOr(or, obj, binding, joinStages),
                NotTerm not => TranslateNot(not, obj, binding, joinStages),
                _           => TranslateLeaf(term, obj, binding, joinStages),
            };
        }

        private static FilterDefinition<BsonDocument> TranslateAnd(
            AndTerm term, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            // Access children via known field names using reflection
            var t1 = GetPrivateField<QueryTerm>(term, "Term1");
            var t2 = GetPrivateField<QueryTerm>(term, "Term2");
            return Builders<BsonDocument>.Filter.And(
                TranslateTerm(t1, obj, binding, joinStages),
                TranslateTerm(t2, obj, binding, joinStages));
        }

        private static FilterDefinition<BsonDocument> TranslateOr(
            OrTerm term, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            var t1 = GetPrivateField<QueryTerm>(term, "_term1");
            var t2 = GetPrivateField<QueryTerm>(term, "_term2");
            return Builders<BsonDocument>.Filter.Or(
                TranslateTerm(t1, obj, binding, joinStages),
                TranslateTerm(t2, obj, binding, joinStages));
        }

        private static FilterDefinition<BsonDocument> TranslateNot(
            NotTerm term, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            var inner = GetPrivateField<QueryTerm>(term, "Term1");
            return Builders<BsonDocument>.Filter.Not(TranslateTerm(inner, obj, binding, joinStages));
        }

        private static FilterDefinition<BsonDocument> TranslateLeaf(
            QueryTerm term, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            // Use public API: GetTermFieldInfo + Value
            FieldBinding fb;
            string fieldName;
            try
            {
                fb = term.GetTermFieldInfo(binding);
                // Validate handle: RecordBinding name-based fallback can return the wrong
                // FieldBinding when an embedded Record has a same-named field as the root type.
                var termTarget = GetProtectedField<Record>(term, "Target");
                var termField  = GetProtectedField<TField>(term, "Field");
                var handle     = termField.GetFieldInfo(termTarget).FieldHandle.Value;
                if (fb.Info.FieldInfo.FieldHandle.Value != handle)
                    throw new InvalidOperationException("field belongs to joined object");
                fieldName = fb.Info.TargetName;
            }
            catch
            {
                // Field not in root binding — may belong to a joined Record
                fieldName = ResolveJoinedFieldPath(term, joinStages);
                if (string.IsNullOrEmpty(fieldName))
                    return Builders<BsonDocument>.Filter.Empty;
            }

            object? value = term.Value;

            return term switch
            {
                IsNullTerm    _  => Builders<BsonDocument>.Filter.Eq(fieldName, BsonNull.Value),
                InTerm        _  => TranslateIn(fieldName, (IList<object>)value!),
                ContainsTerm  _  => Builders<BsonDocument>.Filter.Regex(
                                        fieldName,
                                        new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(value?.ToString() ?? ""), "i")),
                GreaterThanTerm    _ => Builders<BsonDocument>.Filter.Gt(fieldName, MongoMapper.ClrToBson(value)),
                GreaterOrEqualTerm _ => Builders<BsonDocument>.Filter.Gte(fieldName, MongoMapper.ClrToBson(value)),
                LessThanTerm       _ => Builders<BsonDocument>.Filter.Lt(fieldName, MongoMapper.ClrToBson(value)),
                LessOrEqualTerm    _ => Builders<BsonDocument>.Filter.Lte(fieldName, MongoMapper.ClrToBson(value)),
                LikeTerm           _ => Builders<BsonDocument>.Filter.Regex(fieldName,
                                            new BsonRegularExpression(
                                                System.Text.RegularExpressions.Regex.Escape(value?.ToString() ?? ""), "i")),
                EqualTerm          _ => Builders<BsonDocument>.Filter.Eq(fieldName, MongoMapper.ClrToBson(value)),
                _                    => Builders<BsonDocument>.Filter.Eq(fieldName, MongoMapper.ClrToBson(value)),
            };
        }

        private static FilterDefinition<BsonDocument> TranslateIn(string fieldName, IList<object> values)
        {
            var bsonValues = new List<BsonValue>(values.Count);
            foreach (var v in values)
                bsonValues.Add(MongoMapper.ClrToBson(v));
            return Builders<BsonDocument>.Filter.In(fieldName, bsonValues);
        }

        // ── Sort translation ──────────────────────────────────────────────────────────

        public static SortDefinition<BsonDocument>? TranslateSort(SortOrder? sortOrder, Record obj,
            IReadOnlyList<MongoJoinStage>? joinStages = null)
        {
            if (sortOrder == null) return null;

            // Handle CombinedSortOrder (built by LINQ ThenBy)
            if (sortOrder.GetType().Name == "CombinedSortOrder")
            {
                var primary   = GetPrivateField<SortOrder>(sortOrder, "_primary");
                var secondary = GetPrivateField<SortOrder>(sortOrder, "_secondary");
                var p = TranslateSort(primary, obj, joinStages);
                var s = TranslateSort(secondary, obj, joinStages);
                if (p == null) return s;
                if (s == null) return p;
                return Builders<BsonDocument>.Sort.Combine(p, s);
            }

            RecordBinding binding = MongoMapper.BuildMinimalObjectBinding(obj);

            // Determine sort direction by type name
            string typeName  = sortOrder.GetType().Name;
            string fieldName = GetSortFieldName(sortOrder, obj, binding, joinStages);

            if (string.IsNullOrEmpty(fieldName)) return null;

            return typeName.Contains("Descending")
                ? Builders<BsonDocument>.Sort.Descending(fieldName)
                : Builders<BsonDocument>.Sort.Ascending(fieldName);
        }

        private static string GetSortFieldName(SortOrder sortOrder, Record obj, RecordBinding binding,
            IReadOnlyList<MongoJoinStage>? joinStages)
        {
            try
            {
                // SortOrder has protected Field and Target — access via reflection
                var field  = GetProtectedField<TField>(sortOrder, "Field");
                var target = GetProtectedField<Record>(sortOrder, "Target");
                try
                {
                    var fi     = binding.GetFieldBinding(target, field);
                    // Validate handle: name-based fallback can return wrong binding
                    var handle = field.GetFieldInfo(target).FieldHandle.Value;
                    if (fi.Info.FieldInfo.FieldHandle.Value != handle)
                        throw new InvalidOperationException("field belongs to joined object");
                    return fi.Info.TargetName;
                }
                catch
                {
                    // Field is on a joined Record — resolve via join alias prefix
                    return ResolveJoinedFieldPath(target, field, joinStages);
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Resolves the dot-notation field path for a field that lives on an embedded (joined)
        /// Record, e.g. <c>__joined_Category.name</c>.
        /// </summary>
        private static string ResolveJoinedFieldPath(QueryTerm term, IReadOnlyList<MongoJoinStage>? stages)
        {
            if (stages == null || stages.Count == 0) return string.Empty;
            try
            {
                var target = GetProtectedField<Record>(term, "Target");
                var field  = GetProtectedField<TField>(term, "Field");
                return ResolveJoinedFieldPath(target, field, stages);
            }
            catch { return string.Empty; }
        }

        private static string ResolveJoinedFieldPath(Record target, TField field,
            IReadOnlyList<MongoJoinStage>? stages)
        {
            if (stages == null || stages.Count == 0) return string.Empty;
            Type targetType = target.GetType();
            foreach (var stage in stages)
            {
                if (stage.EmbeddedType != targetType) continue;
                // Find BSON name for this TField instance on the embedded type
                try
                {
                    var entry = MongoTypeCache.GetEntry(targetType);
                    foreach (var fd in entry.Fields)
                    {
                        if (ReferenceEquals(fd.FieldInfo.GetValue(target), field))
                            return stage.Alias + "." + fd.BsonName;
                    }
                }
                catch { }
            }
            return string.Empty;
        }

        // ── Reflection helpers ────────────────────────────────────────────────────────

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var fi = instance.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (fi == null)
                throw new InvalidOperationException(
                    $"Field '{fieldName}' not found on {instance.GetType().Name}");
            return (T)fi.GetValue(instance)!;
        }

        private static T GetProtectedField<T>(object instance, string fieldName)
        {
            var type = instance.GetType();
            while (type != null)
            {
                var fi = type.GetField(
                    fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (fi != null) return (T)fi.GetValue(instance)!;
                type = type.BaseType;
            }
            throw new InvalidOperationException(
                $"Field '{fieldName}' not found on {instance.GetType().Name} or its base types");
        }
    }
}
