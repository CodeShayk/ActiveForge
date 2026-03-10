using System;

namespace ActiveForge.Attributes
{
    /// <summary>Indicates that a Record maps to a table-valued function rather than a table/view.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FunctionAttribute : Attribute { }
}
