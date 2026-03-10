using System;

namespace ActiveForge.Linq
{
    /// <summary>
    /// Extends <see cref="DataConnection"/> with <c>Query&lt;T&gt;()</c> — the entry point
    /// for LINQ-style queries in ActiveForge ORM.
    ///
    /// <para>Usage:</para>
    /// <code>
    /// var products = conn.Query&lt;Product&gt;()
    ///     .Where(p => p.IsActive.Value == true &amp;&amp; p.Price.Value &gt; 10m)
    ///     .OrderBy(p => p.Name)
    ///     .Take(20)
    ///     .ToList();
    /// </code>
    ///
    /// <para>Join-type overrides:</para>
    /// <code>
    /// var products = conn.Query&lt;ProductWithCategory&gt;()
    ///     .LeftOuterJoin&lt;Category&gt;()          // override class-level INNER JOIN
    ///     .Where(p => p.Category.Name == "Books")
    ///     .ToList();
    /// </code>
    /// </summary>
    public static class DataConnectionExtensions
    {
        /// <summary>
        /// Returns an <see cref="OrmQueryable{T}"/> for <typeparamref name="T"/> backed by
        /// <paramref name="connection"/>. Compose LINQ operators (Where, OrderBy, Take, Skip)
        /// and optional join-type overrides (<see cref="OrmQueryable{T}.InnerJoin{TJoined}"/>,
        /// <see cref="OrmQueryable{T}.LeftOuterJoin{TJoined}"/>) before iterating.
        /// </summary>
        /// <typeparam name="T">A concrete <see cref="Record"/> subclass.</typeparam>
        /// <param name="connection">The open database connection to query against.</param>
        public static OrmQueryable<T> Query<T>(this DataConnection connection) where T : Record
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            T template = (T)connection.Create(typeof(T));
            return new OrmQueryable<T>(connection, template);
        }

        /// <summary>
        /// Overload that accepts a pre-constructed template object.
        /// Useful when the entity type requires a non-default factory or owner.
        /// </summary>
        public static OrmQueryable<T> Query<T>(this DataConnection connection, T template) where T : Record
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (template   == null) throw new ArgumentNullException(nameof(template));

            return new OrmQueryable<T>(connection, template);
        }
    }
}
