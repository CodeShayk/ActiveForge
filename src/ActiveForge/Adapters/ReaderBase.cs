using System;
using System.Data;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class that wraps a provider-specific forward-only data reader.
    /// Decouples the ORM engine from any concrete ADO.NET provider by exposing a
    /// uniform row-streaming API. Concrete subclasses
    /// (e.g. <c>SqlAdapterReader</c>, <c>NpgsqlAdapterReader</c>,
    /// <c>SQLiteAdapterReader</c>) delegate each operation to the underlying native
    /// data-reader object.
    /// <para>
    /// Convenience helpers (<see cref="ColumnValue(int)"/>, <see cref="ColumnValue(string)"/>,
    /// <see cref="ColumnOrdinal"/>, <see cref="ColumnCount"/>, <see cref="ColumnName"/>)
    /// are implemented here in terms of the abstract members and require no override.
    /// </para>
    /// </summary>
    public abstract class ReaderBase : IDisposable
    {
        /// <summary>
        /// Advances the reader to the next row.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if there is another row to read;
        /// <see langword="false"/> when all rows have been consumed.
        /// </returns>
        public abstract bool Read();

        /// <summary>
        /// Closes the reader and frees server-side cursor resources.
        /// After this call the reader must not be used to read further data.
        /// </summary>
        public abstract void Close();

        /// <summary>
        /// Releases all managed and unmanaged resources held by the reader, including
        /// closing the underlying native data-reader.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Returns the raw value stored in the specified column of the current row.
        /// The caller is responsible for checking <see cref="IsDBNull"/> before calling
        /// this method if a <see langword="null"/> database value is possible; the
        /// convenience wrapper <see cref="ColumnValue(int)"/> performs this check
        /// automatically.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column value as an <see cref="object"/>.</returns>
        public abstract object GetValue(int ordinal);

        /// <summary>
        /// Returns <see langword="true"/> when the value at the specified column ordinal is
        /// a database <c>NULL</c>; otherwise <see langword="false"/>.
        /// </summary>
        /// <param name="ordinal">The zero-based column index to test.</param>
        /// <returns><see langword="true"/> if the column value is <c>NULL</c>.</returns>
        public abstract bool IsDBNull(int ordinal);

        /// <summary>
        /// Returns the zero-based ordinal position of the column with the given name.
        /// </summary>
        /// <param name="columnName">The column name to look up (case sensitivity is provider-specific).</param>
        /// <returns>The zero-based column ordinal.</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown (by the underlying provider) if <paramref name="columnName"/> is not
        /// found in the result set.
        /// </exception>
        public abstract int GetOrdinal(string columnName);

        /// <summary>
        /// Gets the number of columns in the current result set.
        /// </summary>
        public abstract int FieldCount { get; }

        /// <summary>
        /// Returns the name of the column at the specified zero-based ordinal position.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column name as reported by the provider.</returns>
        public abstract string GetName(int ordinal);

        /// <summary>
        /// Exposes the underlying native reader as an <see cref="IDataRecord"/>, enabling
        /// callers to use typed accessor methods (e.g. <c>GetInt32</c>, <c>GetString</c>)
        /// directly on the provider object when performance is critical.
        /// </summary>
        public abstract IDataRecord Record { get; }

        // ── Convenience helpers ───────────────────────────────────────────────

        /// <summary>
        /// Returns the value at the given zero-based column ordinal, or
        /// <see langword="null"/> if the database value is <c>NULL</c>.
        /// This is a null-safe wrapper around <see cref="GetValue(int)"/>.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column value, or <see langword="null"/> for a database <c>NULL</c>.</returns>
        public object ColumnValue(int ordinal)
            => IsDBNull(ordinal) ? null : GetValue(ordinal);

        /// <summary>
        /// Returns the value for the named column, or <see langword="null"/> if the
        /// database value is <c>NULL</c>.
        /// Internally resolves the column name to an ordinal via <see cref="GetOrdinal"/>
        /// and then delegates to <see cref="ColumnValue(int)"/>.
        /// </summary>
        /// <param name="name">The column name (case sensitivity is provider-specific).</param>
        /// <returns>The column value, or <see langword="null"/> for a database <c>NULL</c>.</returns>
        public object ColumnValue(string name)
        {
            int ord = GetOrdinal(name);
            return IsDBNull(ord) ? null : GetValue(ord);
        }

        /// <summary>
        /// Returns the zero-based ordinal position of the named column.
        /// Equivalent to <see cref="GetOrdinal"/> but provided under the naming convention
        /// used elsewhere in the ORM.
        /// </summary>
        /// <param name="name">The column name to look up.</param>
        /// <returns>The zero-based column ordinal.</returns>
        public int ColumnOrdinal(string name) => GetOrdinal(name);

        /// <summary>
        /// Returns the total number of columns in the current result set.
        /// Equivalent to <see cref="FieldCount"/> but provided as a method for consistency
        /// with other <c>Column*</c> helpers.
        /// </summary>
        /// <returns>The number of columns in the current row.</returns>
        public int ColumnCount() => FieldCount;

        /// <summary>
        /// Returns the name of the column at the given zero-based ordinal position.
        /// Equivalent to <see cref="GetName"/> but provided under the naming convention
        /// used elsewhere in the ORM.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column name as reported by the provider.</returns>
        public string ColumnName(int ordinal) => GetName(ordinal);
    }
}
