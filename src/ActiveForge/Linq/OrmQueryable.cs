using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ActiveForge.Query;

namespace ActiveForge.Linq
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> wrapper for Turquoise ORM.
    /// Accumulates LINQ operators (Where, OrderBy, Take, Skip) and join-type overrides,
    /// then executes the query against the ORM when the sequence is enumerated.
    /// </summary>
    /// <typeparam name="T">A concrete <see cref="DataObject"/> subclass.</typeparam>
    public sealed class OrmQueryable<T> : IOrderedQueryable<T> where T : DataObject
    {
        // ── Query state ───────────────────────────────────────────────────────────────

        internal DataConnection               Connection    { get; }
        internal T                            Template      { get; }
        internal QueryTerm                    WhereTerm     { get; private set; }
        internal SortOrder                    SortOrder     { get; private set; }
        internal int                          PageSize      { get; private set; } = 0;   // 0 = no limit
        internal int                          SkipCount     { get; private set; } = 0;
        internal IReadOnlyList<JoinOverride>  Joins         { get; private set; }

        // ── IQueryable ────────────────────────────────────────────────────────────────

        public Type           ElementType => typeof(T);
        public Expression     Expression  { get; private set; }
        public IQueryProvider Provider    { get; }

        // ── Construction ──────────────────────────────────────────────────────────────

        internal OrmQueryable(DataConnection connection, T template)
        {
            Connection = connection;   // null allowed; validated on execution
            Template   = template ?? throw new ArgumentNullException(nameof(template));
            Expression = Expression.Constant(this);
            Provider   = new OrmQueryProvider<T>(this);
        }

        /// <summary>Used internally by <see cref="OrmQueryProvider{T}"/> to clone state.</summary>
        private OrmQueryable(OrmQueryable<T> source, Expression expression)
        {
            Connection = source.Connection;
            Template   = source.Template;
            WhereTerm  = source.WhereTerm;
            SortOrder  = source.SortOrder;
            PageSize   = source.PageSize;
            SkipCount  = source.SkipCount;
            Joins      = source.Joins;
            Expression = expression;
            Provider   = source.Provider;
        }

        // ── Mutation helpers (called by the provider) ─────────────────────────────────

        /// <param name="expression">
        /// The LINQ method-call expression that produced this change; stored as the new
        /// <see cref="Expression"/> so subsequent chain links can recursively rebuild state.
        /// </param>
        internal OrmQueryable<T> WithWhere(QueryTerm term, Expression expression)
        {
            var next = Clone(expression);
            next.WhereTerm = next.WhereTerm != null ? next.WhereTerm & term : term;
            return next;
        }

        internal OrmQueryable<T> WithSort(SortOrder sort, bool reset, Expression expression)
        {
            var next = Clone(expression);
            next.SortOrder = reset ? sort : (next.SortOrder != null ? new CombinedSortOrder(next.SortOrder, sort) : sort);
            return next;
        }

        internal OrmQueryable<T> WithTake(int count, Expression expression)
        {
            var next = Clone(expression);
            next.PageSize = count;
            return next;
        }

        internal OrmQueryable<T> WithSkip(int count, Expression expression)
        {
            var next = Clone(expression);
            next.SkipCount = count;
            return next;
        }

        /// <summary>
        /// Adds or replaces a join-type override for the embedded DataObject identified by
        /// <paramref name="joinOverride"/>.<see cref="JoinOverride.TargetType"/>.
        /// If an override for the same type already exists it is replaced.
        /// </summary>
        internal OrmQueryable<T> WithJoin(JoinOverride joinOverride, Expression expression)
        {
            var next = Clone(expression);
            var list = next.Joins == null
                ? new List<JoinOverride>()
                : new List<JoinOverride>(next.Joins);

            int idx = list.FindIndex(j => j.TargetType == joinOverride.TargetType);
            if (idx >= 0) list[idx] = joinOverride; else list.Add(joinOverride);

            next.Joins = list.AsReadOnly();
            return next;
        }

        // ── Test-convenience overloads (no expression tracking needed) ─────────────────

        internal OrmQueryable<T> WithWhere(QueryTerm term)               => WithWhere(term, Expression);
        internal OrmQueryable<T> WithSort(SortOrder s, bool reset)       => WithSort(s, reset, Expression);
        internal OrmQueryable<T> WithTake(int count)                     => WithTake(count, Expression);
        internal OrmQueryable<T> WithSkip(int count)                     => WithSkip(count, Expression);
        internal OrmQueryable<T> WithJoin(JoinOverride joinOverride)     => WithJoin(joinOverride, Expression);

        private OrmQueryable<T> Clone(Expression expression) => new OrmQueryable<T>(this, expression);

        // ── Fluent join-type overrides ────────────────────────────────────────────────

        /// <summary>
        /// Overrides the join type for the embedded <typeparamref name="TJoined"/>
        /// DataObject to <b>INNER JOIN</b> for this query, replacing any class-level
        /// <c>[JoinSpec]</c> or convention-based join type.
        /// </summary>
        /// <typeparam name="TJoined">
        /// The <see cref="DataObject"/> subclass embedded in <typeparamref name="T"/>
        /// whose join type to override.
        /// </typeparam>
        public OrmQueryable<T> InnerJoin<TJoined>() where TJoined : DataObject
        {
            var next = WithJoin(new JoinOverride(typeof(TJoined), JoinSpecification.JoinTypeEnum.InnerJoin));
            // Reset the expression to a self-referencing constant so that LINQ operators
            // chained after this call (Where, OrderBy, etc.) can find the join overrides
            // when the provider traverses the expression tree.
            next.Expression = Expression.Constant(next);
            return next;
        }

        /// <summary>
        /// Overrides the join type for the embedded <typeparamref name="TJoined"/>
        /// DataObject to <b>LEFT OUTER JOIN</b> for this query, replacing any class-level
        /// <c>[JoinSpec]</c> or convention-based join type.
        /// Rows in <typeparamref name="T"/> without a matching <typeparamref name="TJoined"/>
        /// record are still returned; joined fields will be null/default.
        /// </summary>
        /// <typeparam name="TJoined">
        /// The <see cref="DataObject"/> subclass embedded in <typeparamref name="T"/>
        /// whose join type to override.
        /// </typeparam>
        public OrmQueryable<T> LeftOuterJoin<TJoined>() where TJoined : DataObject
        {
            var next = WithJoin(new JoinOverride(typeof(TJoined), JoinSpecification.JoinTypeEnum.LeftOuterJoin));
            // Same expression reset as InnerJoin — see comment there.
            next.Expression = Expression.Constant(next);
            return next;
        }

        // ── Execution ─────────────────────────────────────────────────────────────────

        public IEnumerator<T> GetEnumerator()
        {
            IEnumerable<T> result = Execute();
            return result.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<T> Execute()
        {
            if (Connection == null)
                throw new InvalidOperationException(
                    "Cannot enumerate OrmQueryable<T> without a DataConnection.");

            bool hasJoins = Joins != null && Joins.Count > 0;

            if (SkipCount > 0 || PageSize > 0)
            {
                int start = SkipCount;
                int count = PageSize > 0 ? PageSize : int.MaxValue;
                ObjectCollection page = hasJoins
                    ? Connection.QueryPage(Template, WhereTerm, SortOrder, start, count, null, Joins)
                    : Connection.QueryPage(Template, WhereTerm, SortOrder, start, count, null);
                foreach (DataObject obj in page) yield return (T)obj;
            }
            else
            {
                IEnumerable<T> lazy = hasJoins
                    ? Connection.LazyQueryAll<T>(Template, WhereTerm, SortOrder, PageSize, null, Joins)
                    : Connection.LazyQueryAll<T>(Template, WhereTerm, SortOrder, PageSize, null);
                foreach (T obj in lazy) yield return obj;
            }
        }
    }
}
