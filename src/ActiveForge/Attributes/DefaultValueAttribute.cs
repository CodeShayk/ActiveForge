using System;

namespace ActiveForge.Attributes
{
    /// <summary>Specifies a default value for a TField when the DataObject is constructed.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class DefaultValueAttribute : Attribute
    {
        public DefaultValueAttribute(object defaultValue) { Value = defaultValue; }
        public object Value { get; }
        public object GetDefaultValue() => Value;
    }
}
