using System;

namespace ActiveForge.Attributes
{
    /// <summary>Marks the primary key field of a DataObject.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class IdentityAttribute : Attribute { }
}
