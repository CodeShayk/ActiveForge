namespace ActiveForge.Query
{
    /// <summary>Abstract base for ORDER BY clauses attached to a query.</summary>
    public abstract class SortOrder
    {
        protected TField     Field;
        protected DataObject Target;

        protected SortOrder(DataObject target, TField field)
        {
            Target = target;
            Field  = field;
        }

        public abstract string GetSQL(ObjectBinding binding);
    }
}
