using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Turquoise.ORM.Query
{
    /// <summary>
    /// Immutable-ish struct that wraps a SQL string fragment together with optional
    /// setup/cleanup SQL that must be executed before/after the main statement.
    /// </summary>
    public struct QueryFragment
    {
        public string SQL            { get; set; }
        public string QueryIdentifier { get; set; }

        private List<QueryFragment> _setUp;
        private List<QueryFragment> _cleanUp;

        public QueryFragment(string sql) : this()
        {
            SQL = sql;
        }

        public QueryFragment(string sql, string queryIdentifier) : this()
        {
            SQL             = sql;
            QueryIdentifier = queryIdentifier;
        }

        public QueryFragment(QueryFragment fragment) : this()
        {
            SQL             = fragment.SQL;
            QueryIdentifier = fragment.QueryIdentifier;
            _setUp          = fragment._setUp  != null ? new List<QueryFragment>(fragment._setUp)  : null;
            _cleanUp        = fragment._cleanUp != null ? new List<QueryFragment>(fragment._cleanUp) : null;
        }

        // ── Factory helpers ──────────────────────────────────────────────────────────

        public static QueryFragment CreateQueryFragment(List<QueryFragment> fragments)
            => CreateQueryFragment(fragments, null);

        public static QueryFragment CreateQueryFragment(List<QueryFragment> fragments, string prefix)
        {
            var builder  = new StringBuilder(512);
            var setUp    = new List<QueryFragment>();
            var cleanUp  = new List<QueryFragment>();

            foreach (var item in fragments)
            {
                if (!string.IsNullOrEmpty(prefix))
                    builder.Append(prefix);
                builder.Append(item.SQL);
                if (item._setUp   != null) setUp.AddRange(item._setUp);
                if (item._cleanUp != null) cleanUp.AddRange(item._cleanUp);
            }

            var result   = new QueryFragment(builder.ToString());
            result._setUp    = setUp;
            result._cleanUp  = cleanUp;
            return result;
        }

        // ── Iteration ────────────────────────────────────────────────────────────────

        public IEnumerable<QueryFragment> SetUpSQLItems
        {
            get
            {
                if (_setUp == null) yield break;
                foreach (var item in _setUp) yield return item;
            }
        }

        public IEnumerable<QueryFragment> CleanUpSQLItems
        {
            get
            {
                if (_cleanUp == null) yield break;
                foreach (var item in _cleanUp) yield return item;
            }
        }

        // ── Operators ────────────────────────────────────────────────────────────────

        public static QueryFragment operator +(QueryFragment left, QueryFragment right)
        {
            var result = new QueryFragment(left);
            result.SQL += right.SQL;

            if (right._setUp != null)
            {
                result._setUp ??= new List<QueryFragment>();
                foreach (var item in right._setUp)
                {
                    if (string.IsNullOrEmpty(item.QueryIdentifier) ||
                        !result._setUp.Any(e => e.QueryIdentifier == item.QueryIdentifier))
                        result._setUp.Add(item);
                }
            }

            if (right._cleanUp != null)
            {
                result._cleanUp ??= new List<QueryFragment>();
                foreach (var item in right._cleanUp)
                {
                    if (string.IsNullOrEmpty(item.QueryIdentifier) ||
                        !result._cleanUp.Any(e => e.QueryIdentifier == item.QueryIdentifier))
                        result._cleanUp.Add(item);
                }
            }

            return result;
        }

        public static QueryFragment operator +(QueryFragment left, string sql)
        {
            var result = new QueryFragment(left);
            result.SQL += sql;
            return result;
        }

        public static implicit operator QueryFragment(string sql) => new QueryFragment(sql);

        // ── Merge helpers ────────────────────────────────────────────────────────────

        public string MergeSetUpQueryFragments()
        {
            if (_setUp == null) return "";
            var sb = new StringBuilder();
            foreach (var f in _setUp)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(f.Trim());
            }
            return sb.ToString();
        }

        public string MergeSetUpQueryFragments(QueryFragment second)
            => (this + second).MergeSetUpQueryFragments();

        public string MergeCleanUpQueryFragments()
        {
            if (_cleanUp == null) return "";
            var sb = new StringBuilder();
            foreach (var f in _cleanUp)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(f.Trim());
            }
            return sb.ToString();
        }

        public string MergeCleanUpQueryFragments(QueryFragment second)
            => (this + second).MergeCleanUpQueryFragments();

        public void SetUpSQLAddRange(IEnumerable<QueryFragment> items)
        {
            _setUp ??= new List<QueryFragment>();
            foreach (var item in items)
            {
                if (!_setUp.Any(e => e.QueryIdentifier == item.QueryIdentifier))
                    _setUp.Add(item);
            }
        }

        public void CleanUpSQLAddRange(IEnumerable<QueryFragment> items)
        {
            _cleanUp ??= new List<QueryFragment>();
            foreach (var item in items)
            {
                if (!_cleanUp.Any(e => e.QueryIdentifier == item.QueryIdentifier))
                    _cleanUp.Add(item);
            }
        }

        // ── Misc ─────────────────────────────────────────────────────────────────────

        public int    Length => SQL?.Length ?? 0;
        public string Trim() => SQL?.Trim() ?? "";
        public override string ToString() => SQL ?? "";
    }
}
