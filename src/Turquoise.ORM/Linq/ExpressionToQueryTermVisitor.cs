using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Linq
{
    /// <summary>
    /// Translates a LINQ predicate expression (<c>x => ...</c>) into a tree of
    /// Turquoise <see cref="QueryTerm"/> objects.
    ///
    /// <para>Supported node types:</para>
    /// <list type="bullet">
    ///   <item>Binary: <c>==</c>, <c>!=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>&lt;</c>, <c>&lt;=</c></item>
    ///   <item>Logical: <c>&amp;&amp;</c> (<see cref="AndTerm"/>), <c>||</c> (<see cref="OrTerm"/>), <c>!</c> (<see cref="NotTerm"/>)</item>
    ///   <item><c>Contains</c> on <c>IEnumerable</c> — maps to <see cref="InTerm"/></item>
    ///   <item><c>null</c> comparisons — maps to <see cref="IsNullTerm"/></item>
    /// </list>
    ///
    /// <para>
    /// Field accessor: the left-hand side of a comparison must be a direct field access on
    /// the LINQ parameter (e.g. <c>x.Name</c> where <c>Name</c> is a <see cref="TField"/>
    /// instance on the template <see cref="DataObject"/>).
    /// </para>
    /// </summary>
    public sealed class ExpressionToQueryTermVisitor : ExpressionVisitor
    {
        private readonly DataObject            _template;
        private readonly ParameterExpression   _parameter;

        private ExpressionToQueryTermVisitor(LambdaExpression lambda, DataObject template)
        {
            _template  = template;
            _parameter = lambda.Parameters[0];
        }

        /// <summary>
        /// Entry point: translates <paramref name="predicate"/> into a <see cref="QueryTerm"/>.
        /// </summary>
        public static QueryTerm Translate(LambdaExpression predicate, DataObject template)
        {
            if (predicate == null)  throw new ArgumentNullException(nameof(predicate));
            if (template  == null)  throw new ArgumentNullException(nameof(template));

            var visitor = new ExpressionToQueryTermVisitor(predicate, template);
            return visitor.TranslateExpression(predicate.Body);
        }

        // ── Core translation ──────────────────────────────────────────────────────────

        private QueryTerm TranslateExpression(Expression expr)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.AndAlso:
                {
                    var b = (BinaryExpression)expr;
                    return TranslateExpression(b.Left) & TranslateExpression(b.Right);
                }

                case ExpressionType.OrElse:
                {
                    var b = (BinaryExpression)expr;
                    return TranslateExpression(b.Left) | TranslateExpression(b.Right);
                }

                case ExpressionType.Not:
                {
                    var u = (UnaryExpression)expr;
                    return !TranslateExpression(u.Operand);
                }

                case ExpressionType.Equal:
                    return TranslateBinary((BinaryExpression)expr, allowNull: true);

                case ExpressionType.NotEqual:
                {
                    var b    = (BinaryExpression)expr;
                    object v = ExtractValue(b.Right);
                    if (v == null)
                    {
                        // x.Field != null  →  NOT IsNull
                        TField field = ExtractField(b.Left);
                        return !new IsNullTerm(_template, field);
                    }
                    return !new EqualTerm(_template, ExtractField(b.Left), v);
                }

                case ExpressionType.GreaterThan:
                {
                    var b = (BinaryExpression)expr;
                    return new GreaterThanTerm(_template, ExtractField(b.Left), ExtractValue(b.Right));
                }

                case ExpressionType.GreaterThanOrEqual:
                {
                    var b = (BinaryExpression)expr;
                    return new GreaterOrEqualTerm(_template, ExtractField(b.Left), ExtractValue(b.Right));
                }

                case ExpressionType.LessThan:
                {
                    var b = (BinaryExpression)expr;
                    return new LessThanTerm(_template, ExtractField(b.Left), ExtractValue(b.Right));
                }

                case ExpressionType.LessThanOrEqual:
                {
                    var b = (BinaryExpression)expr;
                    return new LessOrEqualTerm(_template, ExtractField(b.Left), ExtractValue(b.Right));
                }

                case ExpressionType.Call:
                    return TranslateMethodCall((MethodCallExpression)expr);

                default:
                    throw new NotSupportedException(
                        $"Expression node type '{expr.NodeType}' is not supported in ORM predicates. " +
                        $"Expression: {expr}");
            }
        }

        // ── Binary term helpers ───────────────────────────────────────────────────────

        private QueryTerm TranslateBinary(BinaryExpression b, bool allowNull)
        {
            // Normalise: put the field expression on the left.
            Expression left  = b.Left;
            Expression right = b.Right;

            if (!IsFieldAccess(left) && IsFieldAccess(right))
            {
                (left, right) = (right, left);
            }

            object value = ExtractValue(right);

            if (value == null && allowNull)
                return new IsNullTerm(_template, ExtractField(left));

            TField field = ExtractField(left);
            return new EqualTerm(_template, field, value);
        }

        // ── Method call translation ───────────────────────────────────────────────────

        private QueryTerm TranslateMethodCall(MethodCallExpression mc)
        {
            string name = mc.Method.Name;

            // collection.Contains(x.Field)   →  field IN (collection)
            // x.Field.Contains(value)         handled separately below
            if (name == "Contains")
            {
                // Enumerable.Contains(source, element) — static extension method
                if (mc.Method.IsStatic && mc.Arguments.Count == 2)
                {
                    TField        field  = ExtractField(mc.Arguments[1]);
                    IList<object> values = EvaluateAsObjectList(mc.Arguments[0]);
                    return new InTerm(_template, field, values);
                }

                // instance.Contains(x.Field) — instance method on a list/collection
                if (!mc.Method.IsStatic && mc.Arguments.Count == 1)
                {
                    TField        field  = ExtractField(mc.Arguments[0]);
                    IList<object> values = EvaluateAsObjectList(mc.Object);
                    return new InTerm(_template, field, values);
                }
            }

            // TString / string field.Contains("...") — translate to LikeTerm with %...%
            if (name == "Contains" && mc.Object != null && IsFieldAccess(mc.Object))
            {
                TField field = ExtractField(mc.Object);
                string value = (string)EvaluateExpression(mc.Arguments[0]);
                return new ContainsTerm(_template, (TString)field, value);
            }

            throw new NotSupportedException(
                $"Method call '{mc.Method.DeclaringType?.Name}.{name}' is not supported in ORM predicates.");
        }

        // ── Field extraction ──────────────────────────────────────────────────────────

        private bool IsFieldAccess(Expression expr)
        {
            if (expr is MemberExpression me && me.Member is FieldInfo fi)
                return fi.FieldType.IsSubclassOf(typeof(TField));
            return false;
        }

        private TField ExtractField(Expression expr)
        {
            // Handle implicit conversions (Convert node) wrapping the field access.
            if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
                expr = ue.Operand;

            if (expr is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(TField)))
            {
                // The member access must be on the LINQ parameter (x.SomeField).
                if (me.Expression == _parameter || IsChainedOnParameter(me.Expression))
                    return (TField)fi.GetValue(_template);
            }

            throw new NotSupportedException(
                $"Cannot extract TField from expression '{expr}'. " +
                "The left-hand side of a comparison must be a direct TField field access on the query parameter.");
        }

        // ── Value extraction ──────────────────────────────────────────────────────────

        private object ExtractValue(Expression expr)
        {
            // Strip type conversions.
            while (expr is UnaryExpression ue
                   && (ue.NodeType == ExpressionType.Convert
                       || ue.NodeType == ExpressionType.ConvertChecked))
            {
                expr = ue.Operand;
            }

            // Explicit null constant.
            if (expr is ConstantExpression ce && ce.Value == null) return null;

            // Compile and evaluate the expression (handles local variables, method calls, etc.).
            return EvaluateExpression(expr);
        }

        private static object EvaluateExpression(Expression expr)
        {
            if (expr is ConstantExpression ce) return ce.Value;
            try
            {
                return Expression.Lambda(expr).Compile().DynamicInvoke();
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(
                    $"Cannot evaluate expression '{expr}' as a constant value: {ex.Message}", ex);
            }
        }

        private static IList<object> EvaluateAsObjectList(Expression expr)
        {
            object raw = EvaluateExpression(expr);
            if (raw is IEnumerable enumerable)
                return enumerable.Cast<object>().ToList();
            throw new NotSupportedException(
                $"Expected an enumerable collection, got '{raw?.GetType().Name}'.");
        }

        // ── Navigation ────────────────────────────────────────────────────────────────

        private bool IsChainedOnParameter(Expression expr)
        {
            // Allows x.NestedObj.Field for E2 navigation (partial support).
            if (expr == _parameter) return true;
            if (expr is MemberExpression me) return IsChainedOnParameter(me.Expression);
            return false;
        }
    }
}
