using System;
using System.Linq;

namespace Turquoise.ORM.Linq
{
    /// <summary>
    /// Extends <see cref="DataConnection"/> with <c>Query&lt;T&gt;()</c> — the entry point
    /// for LINQ-style queries in Turquoise ORM.
    ///
    /// <para>Usage:</para>
    /// <code>
    /// var products = conn.Query&lt;Product&gt;()
    ///     .Where(p => p.IsActive.Value == true &amp;&amp; p.Price.Value &gt; 10m)
    ///     .OrderBy(p => p.Name)
    ///     .Take(20)
    ///     .ToList();
    /// </code>
    /// </summary>
    public static class DataConnectionExtensions
    {
        /// <summary>
        /// Returns an <see cref="IQueryable{T}"/> for <typeparamref name="T"/> backed by
        /// <paramref name="connection"/>. Compose LINQ operators (Where, OrderBy, Take, Skip)
        /// before iterating — each operator is translated to the equivalent
        /// <see cref="Turquoise.ORM.Query.QueryTerm"/> and executed in a single ORM query.
        /// </summary>
        /// <typeparam name="T">A concrete <see cref="DataObject"/> subclass.</typeparam>
        /// <param name="connection">The open database connection to query against.</param>
        public static IQueryable<T> Query<T>(this DataConnection connection) where T : DataObject
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            T template = (T)connection.Create(typeof(T));
            return new OrmQueryable<T>(connection, template);
        }

        /// <summary>
        /// Overload that accepts a pre-constructed template object.
        /// Useful when the entity type requires a non-default factory or owner.
        /// </summary>
        public static IQueryable<T> Query<T>(this DataConnection connection, T template) where T : DataObject
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (template   == null) throw new ArgumentNullException(nameof(template));

            return new OrmQueryable<T>(connection, template);
        }
    }
}
