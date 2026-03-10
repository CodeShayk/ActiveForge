using ActiveForge;
using ActiveForge.Attributes;

namespace ActiveForge.Examples.Domain
{
    /// <summary>A customer order.  Maps to the [Orders] table.</summary>
    [Table("Orders")]
    public class Order : IdentityRecord
    {
        [Column("CustomerName")]
        public TString CustomerName = new TString();

        [Column("OrderDate")]
        public TDateTime OrderDate = new TDateTime();

        [Column("TotalAmount")]
        public TDecimal TotalAmount = new TDecimal();

        [Column("Status")]
        public TString Status = new TString();

        public Order() { }
        public Order(DataConnection conn) : base(conn) { }
    }
}
