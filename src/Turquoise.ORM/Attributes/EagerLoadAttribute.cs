using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>
    /// Controls whether an embedded DataObject field is loaded eagerly (true, default)
    /// or must be explicitly included in a FieldSubset.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class EagerLoadAttribute : Attribute
    {
        public EagerLoadAttribute(bool load) { Load = load; }
        public bool Load { get; }
    }
}
