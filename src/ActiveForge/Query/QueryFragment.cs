using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ActiveForge.Query
{
    /// <summary>
    /// Represents a SQL text fragment together with optional setup and cleanup SQL
    /// statements that must be executed before and after the main statement respectively.
    /// <para>
    /// <see cref="QueryFragment"/> is a value type (struct) so that it can be passed
    /// by value on the call stack without heap allocation for the common case where no
    /// setup or cleanup SQL is needed.
    /// </para>
    /// <para>
    /// The <c>+</c> operator concatenates two fragments, merging their setup and cleanup
    /// lists while deduplicating entries by <see cref="QueryIdentifier"/>. An implicit
    /// conversion from <see cref="string"/> is provided for concise inline construction.
    /// </para>
    /// </summary>
    public struct QueryFragment
    {
        /// <summary>
        /// The primary SQL text of this fragment, e.g. a predicate clause, a column list,
        /// or a complete statement. May be <see langword="null"/> if this fragment was
        /// default-initialised.
        /// </summary>
        public string SQL            { get; set; }

        /// <summary>
        /// Optional identifier used to deduplicate setup and cleanup SQL entries when two
        /// fragments are combined with <c>+</c>. When two entries share the same non-null,
        /// non-empty <see cref="QueryIdentifier"/> only the first is retained in the merged
        /// list. Leave <see langword="null"/> or empty to always include the entry.
        /// </summary>
        public string QueryIdentifier { get; set; }

        /// <summary>List of setup fragments to execute before the main statement; lazily allocated.</summary>
        private List<QueryFragment> _setUp;

        /// <summary>List of cleanup fragments to execute after the main statement; lazily allocated.</summary>
        private List<QueryFragment> _cleanUp;

        /// <summary>
        /// Initialises a new <see cref="QueryFragment"/> with the specified SQL text.
        /// No setup or cleanup SQL is associated.
        /// </summary>
        /// <param name="sql">The SQL text for this fragment.</param>
        public QueryFragment(string sql) : this()
        {
            SQL = sql;
        }

        /// <summary>
        /// Initialises a new <see cref="QueryFragment"/> with SQL text and a deduplication
        /// identifier. The identifier is used when merging fragments via the <c>+</c>
        /// operator to prevent identical setup/cleanup statements from being emitted more
        /// than once.
        /// </summary>
        /// <param name="sql">The SQL text for this fragment.</param>
        /// <param name="queryIdentifier">
        /// A unique string key for this fragment; two fragments with the same non-empty key
        /// are considered duplicates during merging.
        /// </param>
        public QueryFragment(string sql, string queryIdentifier) : this()
        {
            SQL             = sql;
            QueryIdentifier = queryIdentifier;
        }

        /// <summary>
        /// Copy constructor. Creates a deep copy of <paramref name="fragment"/>, duplicating
        /// its setup and cleanup lists so that modifications to the copy do not affect the
        /// original.
        /// </summary>
        /// <param name="fragment">The <see cref="QueryFragment"/> to copy.</param>
        public QueryFragment(QueryFragment fragment) : this()
        {
            SQL             = fragment.SQL;
            QueryIdentifier = fragment.QueryIdentifier;
            _setUp          = fragment._setUp  != null ? new List<QueryFragment>(fragment._setUp)  : null;
            _cleanUp        = fragment._cleanUp != null ? new List<QueryFragment>(fragment._cleanUp) : null;
        }

        // ── Factory helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a single <see cref="QueryFragment"/> whose <see cref="SQL"/> is the
        /// concatenation of all <see cref="SQL"/> values in <paramref name="fragments"/>,
        /// with their setup and cleanup lists merged and deduplicated.
        /// </summary>
        /// <param name="fragments">The ordered list of fragments to combine.</param>
        /// <returns>A new <see cref="QueryFragment"/> containing the merged result.</returns>
        public static QueryFragment CreateQueryFragment(List<QueryFragment> fragments)
            => CreateQueryFragment(fragments, null);

        /// <summary>
        /// Creates a single <see cref="QueryFragment"/> whose <see cref="SQL"/> is the
        /// concatenation of all <see cref="SQL"/> values in <paramref name="fragments"/>,
        /// optionally prepending <paramref name="prefix"/> before each item's SQL.
        /// Setup and cleanup lists are merged and deduplicated across all items.
        /// </summary>
        /// <param name="fragments">The ordered list of fragments to combine.</param>
        /// <param name="prefix">
        /// An optional string prepended before the SQL of each fragment (e.g. a separator
        /// or keyword). Pass <see langword="null"/> or empty to omit.
        /// </param>
        /// <returns>A new <see cref="QueryFragment"/> containing the merged result.</returns>
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

        /// <summary>
        /// Enumerates the setup SQL fragments associated with this fragment.
        /// These statements must be executed in order before the main SQL statement.
        /// Returns an empty sequence when no setup SQL has been registered.
        /// </summary>
        public IEnumerable<QueryFragment> SetUpSQLItems
        {
            get
            {
                if (_setUp == null) yield break;
                foreach (var item in _setUp) yield return item;
            }
        }

        /// <summary>
        /// Enumerates the cleanup SQL fragments associated with this fragment.
        /// These statements must be executed in order after the main SQL statement,
        /// typically to drop temporary tables or release resources.
        /// Returns an empty sequence when no cleanup SQL has been registered.
        /// </summary>
        public IEnumerable<QueryFragment> CleanUpSQLItems
        {
            get
            {
                if (_cleanUp == null) yield break;
                foreach (var item in _cleanUp) yield return item;
            }
        }

        // ── Operators ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Concatenates the SQL of <paramref name="left"/> and <paramref name="right"/>,
        /// merging their setup and cleanup lists. Entries in the right operand that share
        /// a <see cref="QueryIdentifier"/> with an existing left entry are deduplicated
        /// (the left entry is kept).
        /// </summary>
        /// <param name="left">The left-hand fragment.</param>
        /// <param name="right">The right-hand fragment whose SQL is appended.</param>
        /// <returns>A new <see cref="QueryFragment"/> containing the combined result.</returns>
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

        /// <summary>
        /// Appends the raw SQL string <paramref name="sql"/> to this fragment's
        /// <see cref="SQL"/> without affecting setup or cleanup lists.
        /// </summary>
        /// <param name="left">The left-hand fragment.</param>
        /// <param name="sql">The SQL text to append.</param>
        /// <returns>A new <see cref="QueryFragment"/> with the concatenated SQL.</returns>
        public static QueryFragment operator +(QueryFragment left, string sql)
        {
            var result = new QueryFragment(left);
            result.SQL += sql;
            return result;
        }

        /// <summary>
        /// Implicitly converts a plain SQL string to a <see cref="QueryFragment"/> with no
        /// setup or cleanup SQL.
        /// </summary>
        /// <param name="sql">The SQL text.</param>
        public static implicit operator QueryFragment(string sql) => new QueryFragment(sql);

        // ── Merge helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Concatenates all setup SQL fragments belonging to this fragment into a single
        /// space-separated string. Returns an empty string when no setup SQL is present.
        /// </summary>
        /// <returns>A space-separated string of all setup SQL statements.</returns>
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

        /// <summary>
        /// Merges this fragment's setup SQL with those of <paramref name="second"/>,
        /// then concatenates all setup SQL into a single space-separated string.
        /// </summary>
        /// <param name="second">A second fragment whose setup SQL is included.</param>
        /// <returns>A space-separated string of all merged setup SQL statements.</returns>
        public string MergeSetUpQueryFragments(QueryFragment second)
            => (this + second).MergeSetUpQueryFragments();

        /// <summary>
        /// Concatenates all cleanup SQL fragments belonging to this fragment into a single
        /// space-separated string. Returns an empty string when no cleanup SQL is present.
        /// </summary>
        /// <returns>A space-separated string of all cleanup SQL statements.</returns>
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

        /// <summary>
        /// Merges this fragment's cleanup SQL with those of <paramref name="second"/>,
        /// then concatenates all cleanup SQL into a single space-separated string.
        /// </summary>
        /// <param name="second">A second fragment whose cleanup SQL is included.</param>
        /// <returns>A space-separated string of all merged cleanup SQL statements.</returns>
        public string MergeCleanUpQueryFragments(QueryFragment second)
            => (this + second).MergeCleanUpQueryFragments();

        /// <summary>
        /// Appends each item in <paramref name="items"/> to this fragment's setup SQL list,
        /// skipping any item whose <see cref="QueryIdentifier"/> already exists in the list.
        /// </summary>
        /// <param name="items">The setup fragments to add.</param>
        public void SetUpSQLAddRange(IEnumerable<QueryFragment> items)
        {
            _setUp ??= new List<QueryFragment>();
            foreach (var item in items)
            {
                if (!_setUp.Any(e => e.QueryIdentifier == item.QueryIdentifier))
                    _setUp.Add(item);
            }
        }

        /// <summary>
        /// Appends each item in <paramref name="items"/> to this fragment's cleanup SQL list,
        /// skipping any item whose <see cref="QueryIdentifier"/> already exists in the list.
        /// </summary>
        /// <param name="items">The cleanup fragments to add.</param>
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

        /// <summary>
        /// Gets the character length of <see cref="SQL"/>, or 0 if <see cref="SQL"/> is
        /// <see langword="null"/>.
        /// </summary>
        public int    Length => SQL?.Length ?? 0;

        /// <summary>
        /// Returns <see cref="SQL"/> with leading and trailing whitespace removed,
        /// or an empty string if <see cref="SQL"/> is <see langword="null"/>.
        /// </summary>
        public string Trim() => SQL?.Trim() ?? "";

        /// <summary>
        /// Returns <see cref="SQL"/>, or an empty string if <see cref="SQL"/> is
        /// <see langword="null"/>.
        /// </summary>
        public override string ToString() => SQL ?? "";
    }
}
