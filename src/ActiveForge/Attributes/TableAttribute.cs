using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Specifies the database table or view name for a Record class.
    /// When omitted the class name is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class TableAttribute : Attribute
    {
        public TableAttribute(string sourceName) { SourceName = sourceName; }
        public string SourceName { get; }
        public string GetSourceName() => SourceName;
    }
}
