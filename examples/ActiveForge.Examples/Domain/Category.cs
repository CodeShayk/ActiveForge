using ActiveForge;
using ActiveForge.Attributes;

namespace ActiveForge.Examples.Domain
{
    /// <summary>
    /// Product category.  Maps to the [Categories] table.
    /// Extends <see cref="IdentityRecord"/> so it automatically has an integer
    /// primary key called <c>ID</c>.
    /// </summary>
    [Table("Categories")]
    public class Category : IdentityRecord
    {
        [Column("Name")]
        public TString Name = new TString();

        [Column("Description")]
        public TString Description = new TString();

        // ── Constructors ──────────────────────────────────────────────────────────────

        /// <summary>Default constructor — no connection attached.</summary>
        public Category() { }

        /// <summary>Construct and bind to a live connection.</summary>
        public Category(DataConnection conn) : base(conn) { }
    }
}
