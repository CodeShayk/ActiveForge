namespace ActiveForge.Query
{
    /// <summary>
    /// A leaf <see cref="QueryTerm"/> that generates a less-than-or-equal predicate of the
    /// form <c>alias.column &lt;= @IN_column&lt;n&gt;</c>.
    /// Useful for filtering rows where a numeric, date, or comparable field does not exceed
    /// a given threshold value (inclusive upper bound).
    /// </summary>
    public class LessOrEqualTerm : QueryTerm
    {
        /// <summary>
        /// Initialises a new <see cref="LessOrEqualTerm"/> for the specified field and value.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns the field. For joined queries this may be
        /// an embedded child <see cref="Record"/>, not the root.
        /// </param>
        /// <param name="field">The field whose column will appear on the left-hand side of <c>&lt;=</c>.</param>
        /// <param name="value">
        /// The inclusive upper-bound value. Passed through the field's <c>FieldMapper</c> and
        /// optional encryption layer before being bound to the command parameter.
        /// </param>
        public LessOrEqualTerm(Record target, TField field, object value) : base(target, field, value) { }

        /// <summary>
        /// Generates the SQL fragment <c>alias.QuotedColumn &lt;= @IN_column&lt;n&gt;</c>
        /// for use in a SELECT or UPDATE WHERE clause.
        /// The table alias prefix is omitted when the mapped node has no alias.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate a unique parameter name suffix; incremented once
        /// for the single parameter this term emits.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the less-than-or-equal predicate.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "<=" + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++));
        }

        /// <summary>
        /// Generates the SQL less-than-or-equal fragment for use inside a DELETE statement's
        /// WHERE clause. Uses the field's source (table) name rather than a query alias.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate a unique parameter name suffix; incremented once.
        /// </param>
        /// <returns>A less-than-or-equal predicate string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "<=" + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++);
        }
    }
}
