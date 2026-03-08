using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>
    /// Specifies the generator (sequence) used to produce values for an identity field.
    /// Pass an empty string for databases that use IDENTITY / AUTOINCREMENT columns.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class GeneratorAttribute : Attribute
    {
        public GeneratorAttribute(string generatorName) { GeneratorName = generatorName; }
        public string GeneratorName { get; }
    }
}
