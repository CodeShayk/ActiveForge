namespace ActiveForge.Query
{
    /// <summary>
    /// A unary <see cref="QueryTerm"/> that negates a single child term using the SQL
    /// <c>NOT</c> operator, producing the fragment <c>NOT (&lt;term&gt;)</c>.
    /// Parameter binding and field-subset inclusion are fully delegated to the child term.
    /// </summary>
    public class NotTerm : QueryTerm
    {
        /// <summary>The child term whose SQL output will be wrapped in <c>NOT (...)</c>.</summary>
        protected QueryTerm Term1;

        /// <summary>
        /// Initialises a new <see cref="NotTerm"/> that negates <paramref name="term1"/>.
        /// </summary>
        /// <param name="term1">The predicate to negate.</param>
        public NotTerm(QueryTerm term1) : base()
        {
            Term1 = term1;
        }

        /// <summary>
        /// Generates the SQL fragment <c>NOT (&lt;term SQL&gt;)</c>.
        /// <paramref name="termNumber"/> is forwarded to the child term so that parameter
        /// names remain unique across the full query.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented by
        /// the child term for each parameter placeholder it emits.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the negated clause.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
            => new QueryFragment("NOT (" + Term1.GetSQL(binding, ref termNumber) + ")");

        /// <summary>
        /// Generates the SQL fragment used inside a DELETE statement's WHERE clause,
        /// wrapping the child term's output in <c>NOT (...)</c>.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented by
        /// the child term.
        /// </param>
        /// <returns>A negated clause string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
            => "NOT (" + Term1.GetDeleteSQL(binding, ref termNumber) + ")";

        /// <summary>
        /// Delegates parameter binding to the child term unchanged, since <c>NOT</c> does
        /// not introduce any additional parameters.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance whose field values are used.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="command">The <see cref="CommandBase"/> to which parameters are added.</param>
        /// <param name="termNumber">
        /// Running counter that must match the value used when <see cref="GetSQL"/> was called.
        /// </param>
        public override void BindParameters(Record obj, RecordBinding binding, CommandBase command, ref int termNumber)
            => Term1.BindParameters(obj, binding, command, ref termNumber);

        /// <summary>
        /// Returns the child term's query-values string unchanged. The <c>NOT</c> wrapper
        /// does not add any additional value representations. Used for diagnostics only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance to read values from.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        public override string GetQueryValues(Record obj, RecordBinding binding)
            => Term1.GetQueryValues(obj, binding);

        /// <summary>
        /// Returns <see langword="true"/> if the child term references a
        /// <see cref="LookupRecord"/> rooted at <paramref name="rootObject"/>.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        public override bool IncludesLookupDataObject(Record rootObject)
            => Term1.IncludesLookupDataObject(rootObject);

        /// <summary>
        /// Delegates field-subset inclusion to the child term so that any columns
        /// referenced by the negated predicate are still fetched.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="fieldSubset">
        /// The current <see cref="FieldSubset"/>; returned with any additional fields the
        /// child term requires.
        /// </param>
        /// <returns>The updated <see cref="FieldSubset"/>.</returns>
        public override FieldSubset IncludeInFieldSubset(Record rootObject, RecordBinding binding, FieldSubset fieldSubset)
            => Term1.IncludeInFieldSubset(rootObject, binding, fieldSubset);

        /// <summary>
        /// Delegates debug parameter information collection to the child term.
        /// Used for diagnostic and logging output only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance to read values from.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="n">Running count of parameters appended so far; incremented by the child.</param>
        /// <param name="result">Accumulator string to which parameter descriptions are appended.</param>
        public override void GetParameterDebugInfo(Record obj, RecordBinding binding, ref int n, ref string result)
            => Term1.GetParameterDebugInfo(obj, binding, ref n, ref result);
    }
}
