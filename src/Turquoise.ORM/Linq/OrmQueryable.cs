using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Linq
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> wrapper for Turquoise ORM.
    /// Accumulates LINQ operators (Where, OrderBy, Take, Skip) and executes the query
    /// against the ORM when the sequence is enumerated.
    /// </summary>
    /// <typeparam name="T">A concrete <see cref="DataObject"/> subclass.</typeparam>
    public sealed class OrmQueryable<T> : IOrderedQueryable<T> where T : DataObject
    {
        // ── Query state ───────────────────────────────────────────────────────────────

        internal DataConnection Connection    { get; }
        internal T              Template      { get; }
        internal QueryTerm      WhereTerm     { get; private set; }
        internal SortOrder      SortOrder     { get; private set; }
        internal int            PageSize      { get; private set; } = 0;   // 0 = no limit
        internal int            SkipCount     { get; private set; } = 0;

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

        // ── Test-convenience overloads (no expression tracking needed) ─────────────────

        internal OrmQueryable<T> WithWhere(QueryTerm term)  => WithWhere(term, Expression);
        internal OrmQueryable<T> WithSort(SortOrder s, bool reset) => WithSort(s, reset, Expression);
        internal OrmQueryable<T> WithTake(int count) => WithTake(count, Expression);
        internal OrmQueryable<T> WithSkip(int count) => WithSkip(count, Expression);

        private OrmQueryable<T> Clone(Expression expression) => new OrmQueryable<T>(this, expression);

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

            if (SkipCount > 0 || PageSize > 0)
            {
                int start = SkipCount;
                int count = PageSize > 0 ? PageSize : int.MaxValue;
                ObjectCollection page = Connection.QueryPage(
                    Template, WhereTerm, SortOrder, start, count, null);
                foreach (DataObject obj in page) yield return (T)obj;
            }
            else
            {
                IEnumerable<T> lazy = Connection.LazyQueryAll<T>(
                    Template, WhereTerm, SortOrder, PageSize, null);
                foreach (T obj in lazy) yield return obj;
            }
        }
    }
}
