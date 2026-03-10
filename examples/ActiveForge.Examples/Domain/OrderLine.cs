using ActiveForge;
using ActiveForge.Attributes;

namespace ActiveForge.Examples.Domain
{
    /// <summary>A single line on an order.  Maps to the [OrderLines] table.</summary>
    [Table("OrderLines")]
    public class OrderLine : IdentDataObject
    {
        [Column("OrderID")]
        public TForeignKey OrderID = new TForeignKey();

        [Column("ProductID")]
        public TForeignKey ProductID = new TForeignKey();

        [Column("Quantity")]
        public TInt Quantity = new TInt();

        [Column("UnitPrice")]
        public TDecimal UnitPrice = new TDecimal();

        public OrderLine() { }
        public OrderLine(DataConnection conn) : base(conn) { }
    }
}
