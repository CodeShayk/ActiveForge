using ActiveForge.Query;

namespace ActiveForge.Linq
{
    /// <summary>
    /// Composes two <see cref="SortOrder"/> instances into a single SQL ORDER BY clause
    /// (primary, secondary). Used by <see cref="OrmQueryable{T}"/> when ThenBy is appended.
    /// </summary>
    internal sealed class CombinedSortOrder : SortOrder
    {
        private readonly SortOrder _primary;
        private readonly SortOrder _secondary;

        internal CombinedSortOrder(SortOrder primary, SortOrder secondary)
            : base(null, null)          // base fields unused — we delegate to children
        {
            _primary   = primary;
            _secondary = secondary;
        }

        public override string GetSQL(RecordBinding binding)
            => _primary.GetSQL(binding) + ", " + _secondary.GetSQL(binding);
    }
}
