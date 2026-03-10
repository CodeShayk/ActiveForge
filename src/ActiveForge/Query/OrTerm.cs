namespace ActiveForge.Query
{
    /// <summary>
    /// A composite <see cref="QueryTerm"/> that combines two child terms with a SQL
    /// <c>OR</c> operator, producing the fragment <c>(&lt;term1&gt; OR &lt;term2&gt;)</c>.
    /// Both child terms are fully evaluated: their SQL is generated and their parameters
    /// are bound independently.
    /// </summary>
    public class OrTerm : QueryTerm
    {
        /// <summary>The left-hand child term of the OR expression.</summary>
        private QueryTerm _term1;

        /// <summary>The right-hand child term of the OR expression.</summary>
        private QueryTerm _term2;

        /// <summary>
        /// Initialises a new <see cref="OrTerm"/> that combines <paramref name="term1"/>
        /// and <paramref name="term2"/> with SQL <c>OR</c>.
        /// </summary>
        /// <param name="term1">The left-hand predicate.</param>
        /// <param name="term2">The right-hand predicate.</param>
        public OrTerm(QueryTerm term1, QueryTerm term2) : base()
        {
            _term1 = term1;
            _term2 = term2;
        }

        /// <summary>
        /// Returns <see langword="true"/> if either child term references a
        /// <see cref="LookupRecord"/> rooted at <paramref name="rootObject"/>.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        public override bool IncludesLookupDataObject(Record rootObject)
            => _term1.IncludesLookupDataObject(rootObject) || _term2.IncludesLookupDataObject(rootObject);

        /// <summary>
        /// Generates the SQL fragment <c>(&lt;term1 SQL&gt; OR &lt;term2 SQL&gt;)</c>.
        /// Each child term increments <paramref name="termNumber"/> independently so that
        /// parameter names remain unique across the full query.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented by
        /// each child term that emits a parameter placeholder.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the parenthesised OR clause.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
            => new QueryFragment("(" + _term1.GetSQL(binding, ref termNumber) + " OR " + _term2.GetSQL(binding, ref termNumber) + ")");

        /// <summary>
        /// Generates the SQL fragment used inside a DELETE statement's WHERE clause,
        /// combining both child terms with <c>OR</c>.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented by
        /// each child term.
        /// </param>
        /// <returns>A parenthesised OR clause string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
            => "(" + _term1.GetDeleteSQL(binding, ref termNumber) + " OR " + _term2.GetDeleteSQL(binding, ref termNumber) + ")";

        /// <summary>
        /// Binds the parameters of both child terms to <paramref name="command"/> in
        /// left-to-right order, mirroring the parameter order produced by
        /// <see cref="GetSQL"/>.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance whose field values are used.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="command">The <see cref="CommandBase"/> to which parameters are added.</param>
        /// <param name="termNumber">
        /// Running counter that must match the value used when <see cref="GetSQL"/> was called.
        /// </param>
        public override void BindParameters(Record obj, RecordBinding binding, CommandBase command, ref int termNumber)
        {
            _term1.BindParameters(obj, binding, command, ref termNumber);
            _term2.BindParameters(obj, binding, command, ref termNumber);
        }

        /// <summary>
        /// Returns a human-readable representation of both child terms' values,
        /// formatted as <c>[term1]or[term2]</c>. Used for diagnostics only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance to read values from.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        public override string GetQueryValues(Record obj, RecordBinding binding)
            => "[" + _term1.GetQueryValues(obj, binding) + "]or[" + _term2.GetQueryValues(obj, binding) + "]";

        /// <summary>
        /// Ensures that all fields referenced by both child terms are included in
        /// <paramref name="fieldSubset"/>, which controls which columns are fetched.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="fieldSubset">
        /// The current <see cref="FieldSubset"/>; returned with any additional fields appended.
        /// </param>
        /// <returns>The updated <see cref="FieldSubset"/> including fields from both child terms.</returns>
        public override FieldSubset IncludeInFieldSubset(Record rootObject, RecordBinding binding, FieldSubset fieldSubset)
        {
            fieldSubset = _term1.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            fieldSubset = _term2.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            return fieldSubset;
        }

        /// <summary>
        /// Appends debug parameter information from both child terms to
        /// <paramref name="result"/>. Used for diagnostic and logging output only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance to read values from.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that maps fields to columns.</param>
        /// <param name="n">Running count of parameters appended so far; incremented by each child.</param>
        /// <param name="result">Accumulator string to which parameter descriptions are appended.</param>
        public override void GetParameterDebugInfo(Record obj, RecordBinding binding, ref int n, ref string result)
        {
            _term1.GetParameterDebugInfo(obj, binding, ref n, ref result);
            _term2.GetParameterDebugInfo(obj, binding, ref n, ref result);
        }
    }
}
