using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Maps a TField member to a database column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string columnName) { ColumnName = columnName; }
        public string ColumnName { get; }
        public string GetFieldName() => ColumnName;
    }
}
