using System;
using System.Linq.Expressions;
using System.Reflection;
using ActiveForge.Query;

namespace ActiveForge.Linq
{
    /// <summary>
    /// Translates a LINQ Select projection (<c>x => ...</c>) into an
    /// ActiveForge <see cref="FieldSubset"/> to limit the queries.
    /// </summary>
    public sealed class ExpressionToSelectVisitor : ExpressionVisitor
    {
        private readonly Record            _template;
        private readonly ParameterExpression   _parameter;
        private readonly FieldSubset       _subset;

        private ExpressionToSelectVisitor(LambdaExpression lambda, Record template)
        {
            _template  = template;
            _parameter = lambda.Parameters[0];
            // We use ExcludeAll so we only select the fields explicitly grabbed in the lambda
            _subset = new FieldSubset(template, FieldSubset.InitialState.ExcludeAll, null);
        }

        /// <summary>
        /// Entry point: Translates a select lambda into a FieldSubset.
        /// </summary>
        public static FieldSubset ExtractFieldSubset(LambdaExpression selector, Record template)
        {
            if (selector == null)  throw new ArgumentNullException(nameof(selector));
            if (template  == null)  throw new ArgumentNullException(nameof(template));

            var visitor = new ExpressionToSelectVisitor(selector, template);
            visitor.Visit(selector.Body);
            
            // Ensure primary keys are included for hydration sanity if nothing specific matched
            visitor._subset.EnsurePrimaryKeysIncluded();

            return visitor._subset;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsFieldAccess(node) && (node.Expression == _parameter || IsChainedOnParameter(node.Expression)))
            {
                var (target, field) = ExtractField(node);
                _subset.Include(_template, target, field);
            }
            else if (IsRecordAccess(node) && (node.Expression == _parameter || IsChainedOnParameter(node.Expression)))
            {
                var (target, joinTarget) = ExtractJoinTarget(node);
                _subset.Include(_template, target, joinTarget, FieldSubset.InitialState.IncludeAll);
            }

            return base.VisitMember(node);
        }

        private bool IsFieldAccess(Expression expr)
        {
            if (expr is MemberExpression me && me.Member is FieldInfo fi)
                return fi.FieldType.IsSubclassOf(typeof(TField));
            return false;
        }

        private bool IsRecordAccess(Expression expr)
        {
            if (expr is MemberExpression me && me.Member is FieldInfo fi)
                return fi.FieldType.IsSubclassOf(typeof(Record));
            return false;
        }

        private (Record target, TField field) ExtractField(Expression expr)
        {
            if (expr is MemberExpression me && me.Member is FieldInfo fi && fi.FieldType.IsSubclassOf(typeof(TField)))
            {
                Record container = ResolveContainer(me.Expression);
                return (container, (TField)fi.GetValue(container));
            }
            throw new NotSupportedException($"Cannot extract TField from expression '{expr}'.");
        }

        private (Record target, Record joinTarget) ExtractJoinTarget(Expression expr)
        {
            if (expr is MemberExpression me && me.Member is FieldInfo fi && fi.FieldType.IsSubclassOf(typeof(Record)))
            {
                Record container = ResolveContainer(me.Expression);
                return (container, (Record)fi.GetValue(container));
            }
            throw new NotSupportedException($"Cannot extract join target from expression '{expr}'.");
        }

        private bool IsChainedOnParameter(Expression expr)
        {
            if (expr == _parameter) return true;
            if (expr is MemberExpression me) return IsChainedOnParameter(me.Expression);
            return false;
        }

        private Record ResolveContainer(Expression expr)
        {
            if (expr == _parameter) return _template;

            if (expr is MemberExpression me && me.Member is FieldInfo fi && fi.FieldType.IsSubclassOf(typeof(Record)))
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
