namespace ActiveForge.Query
{
    /// <summary>
    /// A leaf <see cref="QueryTerm"/> that generates a null-check predicate of the form
    /// <c>alias.column IS NULL</c>.
    /// No parameter is bound because the predicate is self-contained in SQL.
    /// To test for <c>IS NOT NULL</c>, wrap this term with a <see cref="NotTerm"/>:
    /// <code>!new IsNullTerm(target, field)</code>
    /// which produces <c>NOT (alias.column IS NULL)</c>.
    /// </summary>
    public class IsNullTerm : QueryTerm
    {
        /// <summary>
        /// Initialises a new <see cref="IsNullTerm"/> that checks whether
        /// <paramref name="field"/> maps to a <c>NULL</c> database value.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns the field. For joined queries this may be
        /// an embedded child <see cref="Record"/>, not the root.
        /// </param>
        /// <param name="field">The field whose column will be tested with <c>IS NULL</c>.</param>
        public IsNullTerm(Record target, TField field) : base(target, field, null) { }

        /// <summary>
        /// Generates the SQL fragment <c>alias.QuotedColumn IS NULL</c>
        /// for use in a SELECT or UPDATE WHERE clause.
        /// The table alias prefix is omitted when the mapped node has no alias.
        /// No parameter placeholder is emitted.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used for parameter name generation by other terms; not incremented
        /// by this term because no parameter is emitted.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the IS NULL predicate.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL");
        }

        /// <summary>
        /// Generates the SQL IS NULL fragment for use inside a DELETE statement's WHERE clause.
        /// Uses the field's source (table) name rather than a query alias.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter; not incremented because no parameter is emitted.
        /// </param>
        /// <returns>An IS NULL predicate string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL";
        }

        /// <summary>
        /// No-op: <see cref="IsNullTerm"/> emits no parameters, so there is nothing to bind.
        /// </summary>
        /// <param name="obj">Unused.</param>
        /// <param name="binding">Unused.</param>
        /// <param name="command">Unused.</param>
        /// <param name="termNumber">Unchanged on return.</param>
        public override void BindParameters(Record obj, RecordBinding binding, CommandBase command, ref int termNumber) { }

        /// <summary>
        /// No-op: <see cref="IsNullTerm"/> has no parameter values to report.
        /// </summary>
        /// <param name="obj">Unused.</param>
        /// <param name="binding">Unused.</param>
        /// <param name="n">Unchanged on return.</param>
        /// <param name="result">Unchanged on return.</param>
        public override void GetParameterDebugInfo(Record obj, RecordBinding binding, ref int n, ref string result) { }

        /// <summary>
        /// Returns a human-readable representation of this term's predicate,
        /// formatted as <c>alias.QuotedColumn IS NULL</c>. Used for diagnostics only.
        /// </summary>
        /// <param name="obj">Unused; present for interface consistency.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        public override string GetQueryValues(Record obj, RecordBinding binding)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL";
        }
    }
}
