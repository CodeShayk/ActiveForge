using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ActiveForge.Query;

namespace ActiveForge.Linq
{
    /// <summary>
    /// Translates a LINQ predicate expression (<c>x => ...</c>) into a tree of
    /// ActiveForge <see cref="QueryTerm"/> objects.
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
    /// Field accessor: the left-hand side of a comparison must be a <see cref="TField"/>
    /// field access on the LINQ parameter, either direct (<c>x.Name</c>) or via an
    /// embedded <see cref="Record"/> (<c>x.Category.Name</c> for joined entities).
    /// </para>
    /// </summary>
    public sealed class ExpressionToQueryTermVisitor : ExpressionVisitor
    {
        private readonly Record            _template;
        private readonly ParameterExpression   _parameter;

        private ExpressionToQueryTermVisitor(LambdaExpression lambda, Record template)
        {
            _template  = template;
            _parameter = lambda.Parameters[0];
        }

        /// <summary>
        /// Entry point: translates <paramref name="predicate"/> into a <see cref="QueryTerm"/>.
        /// </summary>
        public static QueryTerm Translate(LambdaExpression predicate, Record template)
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
                        var (target, field) = ExtractField(b.Left);
                        return !new IsNullTerm(target, field);
                    }
                    {
                        var (target, field) = ExtractField(b.Left);
                        return !new EqualTerm(target, field, v);
                    }
                }

                case ExpressionType.GreaterThan:
                {
                    var b = (BinaryExpression)expr;
                    var (target, field) = ExtractField(b.Left);
                    return new GreaterThanTerm(target, field, ExtractValue(b.Right));
                }

                case ExpressionType.GreaterThanOrEqual:
                {
                    var b = (BinaryExpression)expr;
                    var (target, field) = ExtractField(b.Left);
                    return new GreaterOrEqualTerm(target, field, ExtractValue(b.Right));
                }

                case ExpressionType.LessThan:
                {
                    var b = (BinaryExpression)expr;
                    var (target, field) = ExtractField(b.Left);
                    return new LessThanTerm(target, field, ExtractValue(b.Right));
                }

                case ExpressionType.LessThanOrEqual:
                {
                    var b = (BinaryExpression)expr;
                    var (target, field) = ExtractField(b.Left);
                    return new LessOrEqualTerm(target, field, ExtractValue(b.Right));
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
            var (target, field) = ExtractField(left);

            if (value == null && allowNull)
                return new IsNullTerm(target, field);

            return new EqualTerm(target, field, value);
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
                    var (target, field) = ExtractField(mc.Arguments[1]);
                    IList<object> values = EvaluateAsObjectList(mc.Arguments[0]);
                    return new InTerm(target, field, values);
                }

                // instance.Contains(x.Field) — instance method on a list/collection
                if (!mc.Method.IsStatic && mc.Arguments.Count == 1)
                {
                    var (target, field) = ExtractField(mc.Arguments[0]);
                    IList<object> values = EvaluateAsObjectList(mc.Object);
                    return new InTerm(target, field, values);
                }
            }

            // TString / string field.Contains("...") — translate to LikeTerm with %...%
            if (name == "Contains" && mc.Object != null && IsFieldAccess(mc.Object))
            {
                var (target, field) = ExtractField(mc.Object);
                string value = (string)EvaluateExpression(mc.Arguments[0]);
                return new ContainsTerm(target, (TString)field, value);
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

        /// <summary>
        /// Extracts the (containing Record, TField) pair from an expression.
        /// Supports direct field access on the parameter (<c>x.Name</c>) and cross-join
        /// navigation through embedded Record fields (<c>x.Category.Name</c>).
        /// </summary>
        private (Record target, TField field) ExtractField(Expression expr)
        {
            // Handle implicit conversions (Convert node) wrapping the field access.
            if (expr is UnaryExpression ue && ue.NodeType == ExpressionType.Convert)
                expr = ue.Operand;

            if (expr is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(TField)))
            {
                // The member access must be on the LINQ parameter (x.SomeField)
                // or on a Record field reachable from the parameter (x.Category.Name).
                if (me.Expression == _parameter || IsChainedOnParameter(me.Expression))
                {
                    Record container = ResolveContainer(me.Expression);
                    return (container, (TField)fi.GetValue(container));
                }
            }

            throw new NotSupportedException(
                $"Cannot extract TField from expression '{expr}'. " +
                "The left-hand side of a comparison must be a TField field access on the " +
                "query parameter (e.g. 'x.Name') or on an embedded Record " +
                "(e.g. 'x.Category.Name' for a joined entity).");
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

        // ── Navigation helpers ────────────────────────────────────────────────────────

        private bool IsChainedOnParameter(Expression expr)
        {
            // Allows x.NestedObj.Field for join navigation.
            if (expr == _parameter) return true;
            if (expr is MemberExpression me) return IsChainedOnParameter(me.Expression);
            return false;
        }

        /// <summary>
        /// Walks the member-access chain rooted at <see cref="_parameter"/> on the
        /// template object, resolving each Record field along the way.
        /// For <c>x.Category.Name</c> the chain is: parameter → Category (Record) → Name (TField);
        /// this method resolves the "Category" step and returns <c>_template.Category</c>.
        /// </summary>
        private Record ResolveContainer(Expression expr)
        {
            if (expr == _parameter) return _template;

            if (expr is MemberExpression me && me.Member is FieldInfo fi
                && fi.FieldType.IsSubclassOf(typeof(Record)))
            {
                Record parent = ResolveContainer(me.Expression);
                return (Record)fi.GetValue(parent);
            }

            throw new NotSupportedException(
                $"Cannot resolve Record container from expression '{expr}'. " +
                "Navigation must go through Record fields on the query parameter.");
        }
    }
}
