using System;

namespace ActiveForge.Attributes
{
    /// <summary>Prevents the field from being pre-loaded in the default SELECT.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class NoPreloadAttribute : Attribute { }
}
