using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>Prevents trailing whitespace from being trimmed when reading a string column.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class NoTrimAttribute : Attribute { }
}
