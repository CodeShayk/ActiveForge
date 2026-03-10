using ActiveForge.Attributes;

namespace ActiveForge
{
    /// <summary>
    /// Base class for database-persisted objects that have a single integer identity primary key.
    /// Provides the <see cref="ID"/> field, marked with <see cref="IdentityAttribute"/> and
    /// <see cref="ColumnAttribute"/>, and helper methods for identity comparison.
    /// </summary>
    public abstract class IdentityRecord : Record
    {
        [Column("ID")]
        [Identity]
        [Generator("")]
        public TPrimaryKey ID = new TPrimaryKey();

        public IdentityRecord() { }

        public IdentityRecord(DataConnection target) : base(target) { }

        public IdentityRecord(DataConnection target, Record copyFrom) : base(target, copyFrom) { }

        public override string GetDBBaseClassName() => GetType().Name;
    }
}
