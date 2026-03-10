using System;

namespace ActiveForge.Attributes
{
    /// <summary>Marks the primary key field of a Record.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class IdentityAttribute : Attribute { }
}
