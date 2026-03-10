namespace ActiveForge.Query
{
    /// <summary>
    /// A leaf <see cref="QueryTerm"/> that generates a LIKE predicate of the form
    /// <c>alias.column LIKE @IN_column&lt;n&gt;</c>.
    /// Unlike <see cref="ContainsTerm"/>, this class passes the value to the database
    /// exactly as supplied — the caller is responsible for including any <c>%</c> or
    /// <c>_</c> wildcard characters in the value.
    /// </summary>
    public class LikeTerm : QueryTerm
    {
        /// <summary>
        /// Initialises a new <see cref="LikeTerm"/> for a generic <see cref="TField"/>.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns the field. For joined queries this may be
        /// an embedded child <see cref="Record"/>, not the root.
        /// </param>
        /// <param name="field">The field whose column will appear on the left-hand side of <c>LIKE</c>.</param>
        /// <param name="value">
        /// The LIKE pattern to match, including any wildcard characters (e.g. <c>"Foo%"</c>,
        /// <c>"%bar%"</c>). The value is bound as-is without modification.
        /// </param>
        public LikeTerm(Record target, TField field, object value)   : base(target, field, value) { }

        /// <summary>
        /// Initialises a new <see cref="LikeTerm"/> for a <see cref="TString"/> field,
        /// allowing the overload to disambiguate between <c>TField</c> and <c>TString</c>
        /// when both implicit conversions are available.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns the field.</param>
        /// <param name="field">The string field whose column will appear on the left-hand side of <c>LIKE</c>.</param>
        /// <param name="value">The LIKE pattern to match; passed to the database unchanged.</param>
        public LikeTerm(Record target, TString field, object value)  : base(target, field, value) { }

        /// <summary>
        /// Generates the SQL fragment <c>alias.QuotedColumn LIKE @IN_column&lt;n&gt;</c>
        /// for use in a SELECT or UPDATE WHERE clause.
        /// The table alias prefix is omitted when the mapped node has no alias.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate a unique parameter name suffix; incremented once
        /// for the single parameter this term emits.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the LIKE predicate.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " LIKE " + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++));
        }

        /// <summary>
        /// Generates the SQL LIKE fragment for use inside a DELETE statement's WHERE clause.
        /// Uses the field's source (table) name rather than a query alias.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate a unique parameter name suffix; incremented once.
        /// </param>
        /// <returns>A LIKE predicate string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " LIKE " + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++);
        }
    }
}
