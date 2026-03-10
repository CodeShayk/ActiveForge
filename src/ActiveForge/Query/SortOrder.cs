namespace ActiveForge.Query
{
    /// <summary>
    /// Abstract base class for a single-column ORDER BY specification.
    /// Concrete subclasses (<see cref="OrderAscending"/> and <see cref="OrderDescending"/>)
    /// implement <see cref="GetSQL"/> to emit the appropriate direction keyword (<c>ASC</c>
    /// or <c>DESC</c>).
    /// <para>
    /// For multi-column ordering, combine multiple <see cref="SortOrder"/> instances using
    /// <c>CombinedSortOrder</c>, which renders them as a comma-separated ORDER BY list.
    /// </para>
    /// </summary>
    public abstract class SortOrder
    {
        /// <summary>
        /// The field whose mapped database column is used in the ORDER BY clause.
        /// Resolved to a concrete column name and alias at SQL-generation time via
        /// <see cref="RecordBinding.GetFieldBinding"/>.
        /// </summary>
        protected TField     Field;

        /// <summary>
        /// The <see cref="Record"/> that owns <see cref="Field"/>.
        /// For joined queries this is the embedded child <see cref="Record"/> that
        /// directly contains the field, not the query root — this ensures that the correct
        /// table alias is used when building the ORDER BY fragment.
        /// </summary>
        protected Record Target;

        /// <summary>
        /// Initialises a new <see cref="SortOrder"/> for the specified field and owning object.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns <paramref name="field"/>. For joined queries
        /// pass the embedded child object that directly contains the field.
        /// </param>
        /// <param name="field">The field to sort by.</param>
        protected SortOrder(Record target, TField field)
        {
            Target = target;
            Field  = field;
        }

        /// <summary>
        /// Returns the SQL ORDER BY fragment for this sort specification, including the
        /// optional table alias prefix and the direction keyword.
        /// </summary>
        /// <param name="binding">
        /// The <see cref="RecordBinding"/> that maps <see cref="Field"/> on <see cref="Target"/>
        /// to its concrete <see cref="FieldBinding"/> (column name, alias, etc.).
        /// </param>
        /// <returns>
        /// A SQL string fragment suitable for inclusion after <c>ORDER BY</c>,
        /// e.g. <c>p.Name ASC</c> or <c>p.CreatedAt DESC</c>.
        /// </returns>
        public abstract string GetSQL(RecordBinding binding);
    }
}
