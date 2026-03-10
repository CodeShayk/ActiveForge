using ActiveForge;
using ActiveForge.Attributes;

namespace ActiveForge.MongoDB.Tests
{
    // ── Shared test entity definitions ────────────────────────────────────────────────

    [Table("products")]
    public class MongoTestProduct : IdentityRecord
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public MongoTestProduct() { }
        public MongoTestProduct(DataConnection conn) : base(conn) { }
    }

    [Table("orders")]
    public class MongoTestOrder : IdentityRecord
    {
        [Column("customer_name")] public TString  CustomerName = new TString();
        [Column("total")]         public TDecimal Total        = new TDecimal();
        [Column("active")]        public TBool    Active       = new TBool();

        public MongoTestOrder() { }
        public MongoTestOrder(DataConnection conn) : base(conn) { }
    }

    /// <summary>Record without an [Identity] field, for testing PK-less entities.</summary>
    [Table("logs")]
    public class MongoTestLog : Record
    {
        [Column("message")]  public TString Message  = new TString();
        [Column("severity")] public TInt    Severity = new TInt();

        public MongoTestLog() { }
        public MongoTestLog(DataConnection conn) : base(conn) { }
    }

    /// <summary>
    /// Order line with a FK to <see cref="MongoTestProduct"/>.
    /// Demonstrates $lookup join — the embedded <see cref="Product"/> field is populated
    /// from the "products" collection via <c>product_id → _id</c>.
    /// </summary>
    [Table("order_items")]
    public class MongoTestOrderItem : IdentityRecord
    {
        [Column("product_id")] public TForeignKey ProductId = new TForeignKey();
        [Column("quantity")]   public TInt        Quantity  = new TInt();

        // Embedded joined Record; join attribute wires FK explicitly.
        [Join("product_id", "_id", JoinAttribute.JoinTypeEnum.LeftOuterJoin)]
        public MongoTestProduct Product = new MongoTestProduct();

        public MongoTestOrderItem() { }
        public MongoTestOrderItem(DataConnection conn) : base(conn) { }
    }

    /// <summary>
    /// Order item using name-convention FK discovery (no explicit [Join] attribute).
    /// Convention: embedded field named "Product" → sibling field named "ProductID".
    /// </summary>
    [Table("order_items_conv")]
    public class MongoTestOrderItemConv : IdentityRecord
    {
        [Column("product_id")] public TForeignKey ProductID = new TForeignKey();
        [Column("quantity")]   public TInt        Quantity  = new TInt();

        // No [Join] attribute — builder finds ProductID via convention.
        public MongoTestProduct Product = new MongoTestProduct();

        public MongoTestOrderItemConv() { }
        public MongoTestOrderItemConv(DataConnection conn) : base(conn) { }
    }
}
