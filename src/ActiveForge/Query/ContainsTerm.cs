namespace ActiveForge.Query
{
    /// <summary>
    /// A leaf <see cref="QueryTerm"/> that generates a substring-match predicate of the form
    /// <c>alias.column LIKE @IN_column&lt;n&gt;</c> where the bound parameter value is
    /// automatically wrapped in <c>%</c> wildcards (i.e. <c>%value%</c>).
    /// Use <see cref="LikeTerm"/> instead when you need to supply your own wildcard pattern.
    /// </summary>
    public class ContainsTerm : QueryTerm
    {
        /// <summary>
        /// Initialises a new <see cref="ContainsTerm"/> for a generic <see cref="TField"/>.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns the field. For joined queries this may be
        /// an embedded child <see cref="Record"/>, not the root.
        /// </param>
        /// <param name="field">The field whose column will appear on the left-hand side of <c>LIKE</c>.</param>
        /// <param name="value">
        /// The substring to search for. The value is wrapped in <c>%</c> characters automatically
        /// by <see cref="GetBindValue(TargetFieldInfo, Record, object)"/> before being bound.
        /// </param>
        public ContainsTerm(Record target, TField field, object value)   : base(target, field, value) { }

        /// <summary>
        /// Initialises a new <see cref="ContainsTerm"/> for a <see cref="TString"/> field,
        /// allowing the overload to disambiguate between <c>TField</c> and <c>TString</c>
        /// when both implicit conversions are available.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns the field.</param>
        /// <param name="field">The string field whose column will appear on the left-hand side of <c>LIKE</c>.</param>
        /// <param name="value">The substring to search for; wrapped in <c>%</c> automatically.</param>
        public ContainsTerm(Record target, TString field, object value)  : base(target, field, value) { }

        /// <summary>
        /// Generates the SQL fragment <c>alias.QuotedColumn LIKE @IN_column&lt;n&gt;</c>
        /// for use in a SELECT or UPDATE WHERE clause.
        /// The bound parameter receives the value with <c>%</c> wildcards prepended and appended.
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

        /// <summary>
        /// Overrides the base value-conversion to wrap the converted value in <c>%</c>
        /// wildcard characters, producing the pattern <c>%value%</c> that is then bound
        /// to the command parameter.
        /// </summary>
        /// <param name="info">Metadata describing the target column, including any <c>FieldMapper</c> or encryption.</param>
        /// <param name="obj">The <see cref="Record"/> passed to the base conversion for context.</param>
        /// <param name="valueToConvert">The raw value to convert before wrapping.</param>
        /// <returns>The converted value surrounded by <c>%</c> wildcard characters.</returns>
        protected override object GetBindValue(TargetFieldInfo info, Record obj, object valueToConvert)
        {
            object result = base.GetBindValue(info, obj, valueToConvert);
            return "%" + result + "%";
        }
    }
}
