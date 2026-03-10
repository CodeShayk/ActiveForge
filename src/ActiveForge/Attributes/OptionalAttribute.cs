using System;

namespace ActiveForge.Attributes
{
    /// <summary>Marks a column as optional — it may not exist in the target database schema.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class OptionalAttribute : Attribute { }
}
