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
    /// <see cref="IQueryProvider"/> implementation for <see cref="OrmQueryable{T}"/>.
    /// Translates standard LINQ method calls (Where, OrderBy, ThenBy, Take, Skip) into
    /// ActiveForge <see cref="QueryTerm"/> and <see cref="SortOrder"/> objects, then defers
    /// execution to <see cref="OrmQueryable{T}"/>.
    /// </summary>
    internal sealed class OrmQueryProvider<T> : IQueryProvider where T : Record
    {
        private readonly OrmQueryable<T> _root;

        internal OrmQueryProvider(OrmQueryable<T> root)
        {
            _root = root;
        }

        // ── IQueryProvider ────────────────────────────────────────────────────────────

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            if (typeof(TElement) == typeof(T))
            {
                // TElement == T at runtime; the cast is safe.
                return (IQueryable<TElement>)(object)ApplyExpression(expression);
            }

            return new QueryableProjection<T, TElement>(this, expression);
        }

        public IQueryable CreateQuery(Expression expression) => ApplyExpression(expression);

        public TResult Execute<TResult>(Expression expression)
        {
            // Scalar execution (e.g. Count(), First(), etc.) — basic support.
            object result = Execute(expression);
            return (TResult)result;
        }

        public object Execute(Expression expression)
        {
            if (expression is MethodCallExpression mc && mc.Method.DeclaringType == typeof(Queryable))
            {
                string methodName = mc.Method.Name;

                if (methodName == "Select")
                {
                    OrmQueryable<T> source = ApplyExpression(mc.Arguments[0]);
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);

                    Type tResult = mc.Method.GetGenericArguments()[1];

                    FieldSubset subset = ExpressionToSelectVisitor.ExtractFieldSubset(lambda, source.Template);
                    source = source.WithFieldSubset(subset, mc);

                    IEnumerable<T> records = source;
                    Delegate compiled = lambda.Compile();
                    
                    var selectMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == "Select" && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                        .MakeGenericMethod(typeof(T), tResult);

                    return selectMethod.Invoke(null, new object[] { records, compiled });
                }

                if (methodName == "Count" || methodName == "LongCount" || methodName == "Any" || 
                    methodName == "First" || methodName == "FirstOrDefault" || 
                    methodName == "Single" || methodName == "SingleOrDefault")
                {
                    OrmQueryable<T> source = ApplyExpression(mc.Arguments[0]);

                    if (mc.Arguments.Count == 2)
                    {
                        LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                        QueryTerm term = ExpressionToQueryTermVisitor.Translate(lambda, source.Template);
                        source = source.WithWhere(term, mc);
                    }

                    switch (methodName)
                    {
                        case "Count":           return source.ExecuteCount();
                        case "LongCount":       return (long)source.ExecuteCount();
                        case "Any":             return source.ExecuteAny();
                        case "First":           return source.ExecuteFirst(orDefault: false);
                        case "FirstOrDefault":  return source.ExecuteFirst(orDefault: true);
                        case "Single":          return source.ExecuteSingle(orDefault: false);
                        case "SingleOrDefault": return source.ExecuteSingle(orDefault: true);
                    }
                }
            }

            // Walk the expression and return a materialised list.
            OrmQueryable<T> queryable = ApplyExpression(expression);
            return new List<T>(queryable);
        }

        // ── Expression interpretation ─────────────────────────────────────────────────

        private OrmQueryable<T> ApplyExpression(Expression expression)
        {
            if (expression is MethodCallExpression mc)
                return ApplyMethodCall(mc);

            // Constant (the root queryable itself).
            if (expression is ConstantExpression ce && ce.Value is OrmQueryable<T> q)
                return q;

            return _root;
        }

        private OrmQueryable<T> ApplyMethodCall(MethodCallExpression mc)
        {
            // Recursively process the source (left-hand) argument first.
            OrmQueryable<T> source = ApplyExpression(mc.Arguments[0]);

            string methodName = mc.Method.Name;

            switch (methodName)
            {
                case "Where":
                {
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                    QueryTerm term = ExpressionToQueryTermVisitor.Translate(lambda, source.Template);
                    return source.WithWhere(term, mc);
                }

                case "OrderBy":
                {
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                    SortOrder sort = ExpressionToSortVisitor.TranslateAscending(lambda, source.Template);
                    return source.WithSort(sort, reset: true, mc);
                }

                case "OrderByDescending":
                {
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                    SortOrder sort = ExpressionToSortVisitor.TranslateDescending(lambda, source.Template);
                    return source.WithSort(sort, reset: true, mc);
                }

                case "ThenBy":
                {
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                    SortOrder sort = ExpressionToSortVisitor.TranslateAscending(lambda, source.Template);
                    return source.WithSort(sort, reset: false, mc);
                }

                case "ThenByDescending":
                {
                    LambdaExpression lambda = UnquoteLambda(mc.Arguments[1]);
                    SortOrder sort = ExpressionToSortVisitor.TranslateDescending(lambda, source.Template);
                    return source.WithSort(sort, reset: false, mc);
                }

                case "Take":
                {
                    int count = (int)EvaluateConstant(mc.Arguments[1]);
                    return source.WithTake(count, mc);
                }

                case "Skip":
                {
                    int count = (int)EvaluateConstant(mc.Arguments[1]);
                    return source.WithSkip(count, mc);
                }

                default:
                    throw new NotSupportedException(
                        $"LINQ method '{methodName}' is not supported by OrmQueryProvider. " +
                        "Supported: Where, OrderBy, OrderByDescending, ThenBy, ThenByDescending, Take, Skip.");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static LambdaExpression UnquoteLambda(Expression arg)
        {
            if (arg is UnaryExpression ue && ue.NodeType == ExpressionType.Quote)
                return (LambdaExpression)ue.Operand;
            if (arg is LambdaExpression le)
                return le;
            throw new NotSupportedException($"Expected lambda expression, got {arg.GetType().Name}.");
        }

        private static object EvaluateConstant(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            // Compile and evaluate for captured variables.
            return Expression.Lambda(expr).Compile().DynamicInvoke();
        }
    }

    internal sealed class QueryableProjection<TSource, TResult> : IOrderedQueryable<TResult> where TSource : Record
    {
        private readonly Expression _expression;
        private readonly OrmQueryProvider<TSource> _provider;

        public QueryableProjection(OrmQueryProvider<TSource> provider, Expression expression)
        {
            _provider = provider;
            _expression = expression;
        }

        public Type ElementType => typeof(TResult);
        public Expression Expression => _expression;
        public IQueryProvider Provider => _provider;

        public IEnumerator<TResult> GetEnumerator()
        {
            return ((IEnumerable<TResult>)_provider.Execute(_expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
