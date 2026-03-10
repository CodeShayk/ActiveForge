using ActiveForge.Attributes;

namespace ActiveForge
{
    /// <summary>
    /// Base class for database-persisted objects that have a single integer identity primary key.
    /// Provides the <see cref="ID"/> field, marked with <see cref="IdentityAttribute"/> and
    /// <see cref="ColumnAttribute"/>, and helper methods for identity comparison.
    /// </summary>
    public class IdentDataObject : DataObject
    {
        [Column("ID")]
        [Identity]
        [Generator("")]
        public TPrimaryKey ID = new TPrimaryKey();

        public IdentDataObject() { }

        public IdentDataObject(DataConnection target) : base(target) { }

        public IdentDataObject(DataConnection target, DataObject copyFrom) : base(target, copyFrom) { }

        public override string GetDBBaseClassName() => GetType().Name;
    }
}
