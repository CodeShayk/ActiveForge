using System;
using System.Linq.Expressions;
using System.Reflection;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Linq
{
    /// <summary>
    /// Translates a LINQ key-selector expression (e.g. <c>x => x.Name</c>) into a
    /// Turquoise <see cref="SortOrder"/> (either <see cref="OrderAscending"/> or
    /// <see cref="OrderDescending"/>).
    /// </summary>
    public static class ExpressionToSortVisitor
    {
        /// <summary>Translates the key selector and returns an <see cref="OrderAscending"/> term.</summary>
        public static SortOrder TranslateAscending(LambdaExpression keySelector, DataObject template)
        {
            TField field = ExtractField(keySelector, template);
            return new OrderAscending(template, field);
        }

        /// <summary>Translates the key selector and returns an <see cref="OrderDescending"/> term.</summary>
        public static SortOrder TranslateDescending(LambdaExpression keySelector, DataObject template)
        {
            TField field = ExtractField(keySelector, template);
            return new OrderDescending(template, field);
        }

        // ── Field resolution ──────────────────────────────────────────────────────────

        private static TField ExtractField(LambdaExpression keySelector, DataObject template)
        {
            Expression body = keySelector.Body;

            // Unwrap implicit type conversions.
            if (body is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
                body = ue.Operand;

            if (body is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(TField)))
            {
                return (TField)fi.GetValue(template);
            }

            throw new NotSupportedException(
                $"Cannot translate sort key '{keySelector.Body}'. " +
                "The key selector must access a TField field directly on the query parameter (e.g. x => x.Name).");
        }
    }
}
