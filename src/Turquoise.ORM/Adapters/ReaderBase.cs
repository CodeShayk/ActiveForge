using System;
using System.Data;

namespace Turquoise.ORM
{
    /// <summary>Wraps a provider-specific data reader.</summary>
    public abstract class ReaderBase : IDisposable
    {
        public abstract bool   Read();
        public abstract void   Close();
        public abstract void   Dispose();
        public abstract object GetValue(int ordinal);
        public abstract bool   IsDBNull(int ordinal);
        public abstract int    GetOrdinal(string columnName);
        public abstract int    FieldCount { get; }
        public abstract string GetName(int ordinal);
        public abstract IDataRecord Record { get; }

        // ── Convenience helpers ───────────────────────────────────────────────

        /// <summary>Returns the value at the given ordinal, or null if DBNull.</summary>
        public object ColumnValue(int ordinal)
            => IsDBNull(ordinal) ? null : GetValue(ordinal);

        /// <summary>Returns the value for the named column, or null if DBNull.</summary>
        public object ColumnValue(string name)
        {
            int ord = GetOrdinal(name);
            return IsDBNull(ord) ? null : GetValue(ord);
        }

        /// <summary>Returns the column ordinal for the given column name.</summary>
        public int ColumnOrdinal(string name) => GetOrdinal(name);

        /// <summary>Returns the number of columns in the current row.</summary>
        public int ColumnCount() => FieldCount;

        /// <summary>Returns the column name at the given ordinal.</summary>
        public string ColumnName(int ordinal) => GetName(ordinal);
    }
}
