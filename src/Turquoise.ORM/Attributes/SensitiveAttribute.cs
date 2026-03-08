using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>
    /// Marks a field as sensitive (e.g. passwords, PII).
    /// Sensitive field values are masked in diagnostic/log output.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class SensitiveAttribute : Attribute { }
}
