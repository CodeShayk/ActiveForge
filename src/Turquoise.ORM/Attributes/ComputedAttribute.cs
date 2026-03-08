using System;

namespace Turquoise.ORM.Attributes
{
    /// <summary>
    /// Applied to a class to mark it as introducing a new (derived) DB table
    /// in a multi-table inheritance hierarchy.  The class still physically inherits
    /// from its parent but its own fields are stored in a separate table joined on ID.
    /// Corresponds to the legacy <c>DBDerivedAttribute</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ComputedAttribute : Attribute { }
}
