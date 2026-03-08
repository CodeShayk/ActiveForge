using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.MongoDB.Internal
{
    /// <summary>
    /// Translates Turquoise.ORM QueryTerm predicates and SortOrder values
    /// into MongoDB FilterDefinition and SortDefinition respectively.
    /// </summary>
    internal static class MongoQueryTranslator
    {
        // ── Filter translation ────────────────────────────────────────────────────────

        public static FilterDefinition<BsonDocument> Translate(QueryTerm? term, DataObject obj)
        {
            if (term == null)
                return Builders<BsonDocument>.Filter.Empty;

            ObjectBinding binding = MongoMapper.BuildMinimalObjectBinding(obj);
            return TranslateTerm(term, obj, binding);
        }

        private static FilterDefinition<BsonDocument> TranslateTerm(
            QueryTerm term, DataObject obj, ObjectBinding binding)
        {
            return term switch
            {
                AndTerm and => TranslateAnd(and, obj, binding),
                OrTerm  or  => TranslateOr(or, obj, binding),
                NotTerm not => TranslateNot(not, obj, binding),
                _           => TranslateLeaf(term, obj, binding),
            };
        }

        private static FilterDefinition<BsonDocument> TranslateAnd(
            AndTerm term, DataObject obj, ObjectBinding binding)
        {
            // Access children via known field names using reflection
            var t1 = GetPrivateField<QueryTerm>(term, "Term1");
            var t2 = GetPrivateField<QueryTerm>(term, "Term2");
            return Builders<BsonDocument>.Filter.And(
                TranslateTerm(t1, obj, binding),
                TranslateTerm(t2, obj, binding));
        }

        private static FilterDefinition<BsonDocument> TranslateOr(
            OrTerm term, DataObject obj, ObjectBinding binding)
        {
            var t1 = GetPrivateField<QueryTerm>(term, "_term1");
            var t2 = GetPrivateField<QueryTerm>(term, "_term2");
            return Builders<BsonDocument>.Filter.Or(
                TranslateTerm(t1, obj, binding),
                TranslateTerm(t2, obj, binding));
        }

        private static FilterDefinition<BsonDocument> TranslateNot(
            NotTerm term, DataObject obj, ObjectBinding binding)
        {
            var inner = GetPrivateField<QueryTerm>(term, "Term1");
            return Builders<BsonDocument>.Filter.Not(TranslateTerm(inner, obj, binding));
        }

        private static FilterDefinition<BsonDocument> TranslateLeaf(
            QueryTerm term, DataObject obj, ObjectBinding binding)
        {
            // Use public API: GetTermFieldInfo + Value
            FieldBinding fb;
            try
            {
                fb = term.GetTermFieldInfo(binding);
            }
            catch
            {
                return Builders<BsonDocument>.Filter.Empty;
            }

            string fieldName = fb.Info.TargetName;
            object? value    = term.Value;

            return term switch
            {
                IsNullTerm    _  => Builders<BsonDocument>.Filter.Eq(fieldName, BsonNull.Value),
                InTerm        _  => TranslateIn(fieldName, (IList<object>)value!),
                ContainsTerm  _  => Builders<BsonDocument>.Filter.Regex(
                                        fieldName,
                                        new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(value?.ToString() ?? ""), "i")),
                GreaterThanTerm _ => Builders<BsonDocument>.Filter.Gt(fieldName, MongoMapper.ClrToBson(value)),
                LessThanTerm    _ => Builders<BsonDocument>.Filter.Lt(fieldName, MongoMapper.ClrToBson(value)),
                EqualTerm       _ => Builders<BsonDocument>.Filter.Eq(fieldName, MongoMapper.ClrToBson(value)),
                _                 => Builders<BsonDocument>.Filter.Eq(fieldName, MongoMapper.ClrToBson(value)),
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

        public static SortDefinition<BsonDocument>? TranslateSort(SortOrder? sortOrder, DataObject obj)
        {
            if (sortOrder == null) return null;

            ObjectBinding binding = MongoMapper.BuildMinimalObjectBinding(obj);

            // Determine sort direction by type name
            string typeName  = sortOrder.GetType().Name;
            string fieldName = GetSortFieldName(sortOrder, obj, binding);

            if (string.IsNullOrEmpty(fieldName)) return null;

            return typeName.Contains("Descending")
                ? Builders<BsonDocument>.Sort.Descending(fieldName)
                : Builders<BsonDocument>.Sort.Ascending(fieldName);
        }

        private static string GetSortFieldName(SortOrder sortOrder, DataObject obj, ObjectBinding binding)
        {
            try
            {
                // SortOrder has protected Field and Target — access via reflection
                var field  = GetProtectedField<TField>(sortOrder, "Field");
                var target = GetProtectedField<DataObject>(sortOrder, "Target");
                var fi     = binding.GetFieldBinding(target, field);
                return fi.Info.TargetName;
            }
            catch
            {
                return string.Empty;
            }
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
