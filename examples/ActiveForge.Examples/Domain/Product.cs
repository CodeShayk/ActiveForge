using ActiveForge;
using ActiveForge.Attributes;

namespace ActiveForge.Examples.Domain
{
    /// <summary>
    /// A product in the shop catalogue.  Maps to the [Products] table.
    /// </summary>
    [Table("Products")]
    public class Product : IdentDataObject
    {
        [Column("Name")]
        public TString Name = new TString();

        [Column("Description")]
        public TString Description = new TString();

        [Column("Price")]
        public TDecimal Price = new TDecimal();

        /// <summary>Foreign key into the Categories table.</summary>
        [Column("CategoryID")]
        public TForeignKey CategoryID = new TForeignKey();

        [Column("InStock")]
        public TBool InStock = new TBool();

        [Column("CreatedAt")]
        public TDateTime CreatedAt = new TDateTime();

        // ── Constructors ──────────────────────────────────────────────────────────────

        public Product() { }
        public Product(DataConnection conn) : base(conn) { }
    }
}
