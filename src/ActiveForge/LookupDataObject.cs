namespace ActiveForge
{
    /// <summary>
    /// Base class for lookup/reference data objects whose values are cached in memory
    /// and retrieved once rather than joined on every query.
    /// Subclasses represent static or slowly-changing reference tables (e.g. status codes, types).
    /// </summary>
    public abstract class LookupDataObject : IdentDataObject
    {
        private DataConnection _connection;

        protected LookupDataObject() { }

        protected LookupDataObject(DataConnection target) : base(target)
        {
            _connection = target;
        }

        /// <summary>Returns the DataConnection associated with this lookup object.</summary>
        public DataConnection GetConnection() => _connection ?? Target;

        /// <summary>
        /// Primes the in-memory cache for this lookup type and optionally queries for a specific value.
        /// Override in subclasses to implement caching logic.
        /// </summary>
        /// <param name="term">Optional query term to filter cached values.</param>
        /// <param name="sortOrder">Optional sort order for cached values.</param>
        /// <param name="cacheVersion">Cache version token (0 = unconditional refresh).</param>
        public virtual void PrimeAndQueryCache(Query.QueryTerm term, Query.SortOrder sortOrder, int cacheVersion)
        {
            // Default: no-op. Subclasses override to populate a static cache.
        }

        public override string GetDBBaseClassName() => GetType().Name;
    }
}
