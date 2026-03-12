using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    /// <summary>
    /// SQL Server implementation of <see cref="BaseReader"/> backed by
    /// <see cref="SqlDataReader"/> from <c>Microsoft.Data.SqlClient</c>.
    /// <para>
    /// Each method delegates directly to the corresponding member on the wrapped
    /// <see cref="SqlDataReader"/>. The reader is created by
    /// <see cref="SqlAdapterCommand.ExecuteReader"/> or
    /// <see cref="SqlAdapterCommand.ExecuteSequentialReader"/> and must not be created
    /// directly by application code.
    /// </para>
    /// </summary>
    public class SqlAdapterReader : BaseReader
    {
        /// <summary>The underlying SQL Server data reader managed by this adapter.</summary>
        private readonly SqlDataReader _reader;

        /// <summary>
        /// Initialises a new <see cref="SqlAdapterReader"/> wrapping the supplied
        /// <see cref="SqlDataReader"/>. The reader must already be open and positioned
        /// before the first row.
        /// </summary>
        /// <param name="reader">The <see cref="SqlDataReader"/> to wrap.</param>
        public SqlAdapterReader(SqlDataReader reader) { _reader = reader; }

        /// <summary>
        /// Advances the reader to the next row in the result set.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if another row is available;
        /// <see langword="false"/> when all rows have been consumed.
        /// </returns>
        public override bool Read() => _reader.Read();

        /// <summary>
        /// Closes the underlying <see cref="SqlDataReader"/> and releases server-side
        /// cursor resources. The reader must not be used after this call.
        /// </summary>
        public override void Close() => _reader.Close();

        /// <summary>
        /// Disposes the underlying <see cref="SqlDataReader"/>, releasing all managed and
        /// unmanaged resources.
        /// </summary>
        public override void Dispose() => _reader.Dispose();

        /// <summary>
        /// Returns the raw value of the column at the specified zero-based ordinal for the
        /// current row. Does not perform a null check; use <see cref="BaseReader.ColumnValue(int)"/>
        /// for null-safe access.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column value as an <see cref="object"/>.</returns>
        public override object GetValue(int ordinal) => _reader.GetValue(ordinal);

        /// <summary>
        /// Returns <see langword="true"/> when the column at the specified zero-based
        /// ordinal contains a database <c>NULL</c> value.
        /// </summary>
        /// <param name="ordinal">The zero-based column index to test.</param>
        /// <returns><see langword="true"/> if the column value is <c>NULL</c>.</returns>
        public override bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);

        /// <summary>
        /// Returns the zero-based ordinal position of the named column in the result set.
        /// </summary>
        /// <param name="name">The column name (case-insensitive for SQL Server).</param>
        /// <returns>The zero-based column ordinal.</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown by the underlying provider when <paramref name="name"/> is not found.
        /// </exception>
        public override int GetOrdinal(string name) => _reader.GetOrdinal(name);

        /// <summary>
        /// Gets the number of columns in the current result set, as reported by the
        /// underlying <see cref="SqlDataReader"/>.
        /// </summary>
        public override int FieldCount => _reader.FieldCount;

        /// <summary>
        /// Returns the name of the column at the specified zero-based ordinal position, as
        /// reported by the underlying <see cref="SqlDataReader"/>.
        /// </summary>
        /// <param name="ordinal">The zero-based column index.</param>
        /// <returns>The column name string.</returns>
        public override string GetName(int ordinal) => _reader.GetName(ordinal);

        /// <summary>
        /// Exposes the underlying <see cref="SqlDataReader"/> as an <see cref="IDataRecord"/>,
        /// enabling callers to use typed accessor methods (e.g. <c>GetInt32</c>,
        /// <c>GetString</c>) directly on the provider object.
        /// </summary>
        public override IDataRecord Record => _reader;

        /// <summary>
        /// Exposes the underlying <see cref="SqlDataReader"/> directly for SQL Server–
        /// specific code paths that require access to members not covered by the
        /// <see cref="BaseReader"/> API (e.g. asynchronous read methods).
        /// </summary>
        /// <returns>The native <see cref="SqlDataReader"/> managed by this adapter.</returns>
        public SqlDataReader GetNativeReader() => _reader;
    }
}
