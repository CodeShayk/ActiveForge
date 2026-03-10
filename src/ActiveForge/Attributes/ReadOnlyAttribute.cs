using System;

namespace ActiveForge.Attributes
{
    /// <summary>Marks a column as read-only — it is SELECTed but never written.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ReadOnlyAttribute : Attribute { }
}
