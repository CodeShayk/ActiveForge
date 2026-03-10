using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Marks a Record class as representing a specific database table in an inheritance hierarchy.
    /// Each class in a multi-table inheritance chain should carry this attribute with its own table name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class BaseTableAttribute : Attribute
    {
        public BaseTableAttribute(string sourceName) { SourceName = sourceName; }
        public string SourceName { get; }
        public string GetSourceName() => SourceName;
    }
}
