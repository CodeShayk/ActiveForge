using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>Specifies the positional index of a field when used as a stored procedure or function parameter.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ParameterPositionAttribute : Attribute
    {
        public ParameterPositionAttribute(int position) { Position = position; }
        public int Position { get; }
    }
}
