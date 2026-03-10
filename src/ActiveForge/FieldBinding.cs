namespace ActiveForge
{
    /// <summary>
    /// Links a <see cref="TargetFieldInfo"/> to its position in the SQL JOIN tree via an
    /// <see cref="ObjectBindingMapNode"/>.  Also carries the SELECT alias used when two
    /// tables expose columns with the same name.
    /// </summary>
    public class FieldBinding
    {
        public TargetFieldInfo      Info        = null;
        public string               Alias       = "";
        public ObjectBindingMapNode MapNode     = null;
        /// <summary>True when this binding represents a polymorphic-type discriminator column.</summary>
        public bool                 Translation = false;
    }
}
