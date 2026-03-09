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
    ///
    /// <para>
    /// Supports direct field access (<c>x.Name</c>) and cross-join navigation through
    /// embedded DataObject fields (<c>x.Category.Name</c> for joined entities).
    /// </para>
    /// </summary>
    public static class ExpressionToSortVisitor
    {
        /// <summary>Translates the key selector and returns an <see cref="OrderAscending"/> term.</summary>
        public static SortOrder TranslateAscending(LambdaExpression keySelector, DataObject template)
        {
            var (container, field) = ExtractField(keySelector, template);
            return new OrderAscending(container, field);
        }

        /// <summary>Translates the key selector and returns an <see cref="OrderDescending"/> term.</summary>
        public static SortOrder TranslateDescending(LambdaExpression keySelector, DataObject template)
        {
            var (container, field) = ExtractField(keySelector, template);
            return new OrderDescending(container, field);
        }

        // ── Field resolution ──────────────────────────────────────────────────────────

        private static (DataObject container, TField field) ExtractField(LambdaExpression keySelector, DataObject template)
        {
            ParameterExpression param = keySelector.Parameters[0];
            Expression body = keySelector.Body;

            // Unwrap implicit type conversions.
            if (body is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
                body = ue.Operand;

            if (body is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(TField)))
            {
                DataObject container = ResolveContainer(me.Expression, template, param);
                return (container, (TField)fi.GetValue(container));
            }

            throw new NotSupportedException(
                $"Cannot translate sort key '{keySelector.Body}'. " +
                "The key selector must access a TField field on the query parameter " +
                "(e.g. 'x => x.Name') or via an embedded DataObject " +
                "(e.g. 'x => x.Category.Name' for a joined entity).");
        }

        /// <summary>
        /// Walks a member-access chain rooted at <paramref name="param"/> on the
        /// <paramref name="template"/> object, resolving each DataObject field.
        /// </summary>
        private static DataObject ResolveContainer(Expression expr, DataObject template, ParameterExpression param)
        {
            if (expr == param) return template;

            if (expr is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(DataObject)))
            {
                DataObject parent = ResolveContainer(me.Expression, template, param);
                return (DataObject)fi.GetValue(parent);
            }

            throw new NotSupportedException(
                $"Cannot resolve DataObject container from sort expression '{expr}'. " +
                "Navigation must go through DataObject fields on the query parameter.");
        }
    }
}
