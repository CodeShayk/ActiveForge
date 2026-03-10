namespace ActiveForge.Query
{
    /// <summary>
    /// A concrete <see cref="SortOrder"/> that sorts a single column in descending order,
    /// producing the SQL fragment <c>alias.QuotedColumn DESC</c>.
    /// Instantiate this class directly or use the <c>DataConnection.OrderDescending</c>
    /// factory method to build type-safe ORDER BY clauses.
    /// </summary>
    public class OrderDescending : SortOrder
    {
        /// <summary>
        /// Initialises a new <see cref="OrderDescending"/> sort specification for the
        /// given field on the given target object.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns <paramref name="field"/>. For joined queries
        /// this should be the embedded child <see cref="Record"/> that contains the field,
        /// not the root object.
        /// </param>
        /// <param name="field">
        /// The field whose mapped column will be placed in the ORDER BY clause.
        /// Stored in the protected <c>Field</c> member inherited from <see cref="SortOrder"/>.
        /// </param>
        public OrderDescending(Record target, TField field) : base(target, field) { }

        /// <summary>
        /// Generates the ORDER BY fragment <c>alias.QuotedColumn DESC</c>.
        /// The table alias prefix is omitted when the mapped node carries no alias.
        /// </summary>
        /// <param name="binding">
        /// The <see cref="RecordBinding"/> used to resolve <c>Field</c> and <c>Target</c>
        /// to a concrete <see cref="FieldBinding"/> (column name, alias, etc.).
        /// </param>
        /// <returns>
        /// A SQL string fragment suitable for inclusion in an ORDER BY clause,
        /// e.g. <c>p.CreatedAt DESC</c>.
        /// </returns>
        public override string GetSQL(RecordBinding binding)
        {
            FieldBinding check    = binding.GetFieldBinding(Target, Field);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " DESC";
        }
    }
}
