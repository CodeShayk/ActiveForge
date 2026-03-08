using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>Human-readable description for a field, used in validation messages and UI hints.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DescriptionAttribute : Attribute
    {
        public DescriptionAttribute(string description) { Description = description; }
        public string Description { get; }
    }
}
