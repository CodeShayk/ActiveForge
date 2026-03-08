using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>Indicates that the column value should be compressed before storage.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class CompressibleAttribute : Attribute { }
}
