using System.Collections.Generic;

namespace ActiveForge.Query
{
    /// <summary>
    /// Typed query builder for <typeparamref name="T"/> that encapsulates a QueryTerm,
    /// SortOrder, pagination settings, and field subsets. Used primarily as the sub-query
    /// argument in <see cref="ExistsTerm{T}"/>.
    /// </summary>
    public class Query<T> where T : Record
    {
        private readonly T                              _obj;
        private readonly DataConnection                 _connection;
        private QueryTerm                               _term;
        private SortOrder                               _sortOrder;
        private int                                     _start;
        private int                                     _count;
        private FieldSubset                             _fieldSubset;
        private System.Type[]                           _concreteTypes;
        private Dictionary<System.Type, FieldSubset>    _concreteTypeFieldSubsets;

        public Query(T obj, DataConnection connection)
        {
            _obj        = obj;
            _connection = connection;
        }

        public Query<T> Where(QueryTerm term)            { _term                     = term;   return this; }
        public Query<T> OrderBy(SortOrder sortOrder)     { _sortOrder                 = sortOrder; return this; }
        public Query<T> Skip(int start)                  { _start                    = start;  return this; }
        public Query<T> Take(int count)                  { _count                    = count;  return this; }
        public Query<T> Select(FieldSubset subset)       { _fieldSubset              = subset; return this; }
        public Query<T> Types(System.Type[] types)       { _concreteTypes            = types;  return this; }

        // ── Query execution ───────────────────────────────────────────────────────────

        public RecordCollection QueryAll()
            => _connection.QueryAll(_obj, _term, _sortOrder, _count, _concreteTypes, _fieldSubset, _concreteTypeFieldSubsets);

        public RecordCollection QueryPage()
            => _connection.QueryPage(_obj, _term, _sortOrder, _start, _count, _fieldSubset, _concreteTypes, _concreteTypeFieldSubsets);

        public int QueryCount()
            => _connection.QueryCount(_obj, _term, _concreteTypes, _fieldSubset);

        // ── EXISTS sub-query support ──────────────────────────────────────────────────

        /// <summary>
        /// Generates the inner SELECT SQL for an EXISTS predicate.
        /// Delegates to the connection's dialect-aware query engine.
        /// </summary>
        public QueryFragment GenerateExistsSQLQuery(
            string outerAlias, string outerFieldName, TField existsLinkField, ref int termNumber)
        {
            return _connection.GenerateExistsSQLQuery(
                _obj, outerAlias, outerFieldName, existsLinkField,
                ref termNumber, _term, _sortOrder, _start, _count,
                _fieldSubset, _concreteTypes, _concreteTypeFieldSubsets);
        }

        /// <summary>Binds the query parameters of this sub-query to the given command.</summary>
        public void BindParameters(BaseCommand cmd, ref int termNumber)
        {
            if (_term == null) return;
            bool includeLookups = _obj.ShouldIncludeLookupDataObjectsInBinding(_term, _sortOrder);
            var binding         = _obj.GetBinding(_connection, true, true, _concreteTypes, includeLookups);
            _term.BindParameters(_obj, binding, cmd, ref termNumber);
        }
    }
}
