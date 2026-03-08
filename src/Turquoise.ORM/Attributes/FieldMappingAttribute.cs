using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>Associates a custom IFieldMapper with a column for value transformation on read/write.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class FieldMappingAttribute : Attribute
    {
        public FieldMappingAttribute(Type mapperType) { MapperType = mapperType; }
        public Type MapperType { get; }
    }
}
