using Turquoise.ORM;
using Turquoise.ORM.Attributes;

namespace Turquoise.ORM.MongoDB.Tests
{
    // ── Shared test entity definitions ────────────────────────────────────────────────

    [Table("products")]
    public class MongoTestProduct : IdentDataObject
    {
        [Column("name")]    public TString  Name    = new TString();
        [Column("price")]   public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool   InStock = new TBool();

        public MongoTestProduct() { }
        public MongoTestProduct(DataConnection conn) : base(conn) { }
    }

    [Table("orders")]
    public class MongoTestOrder : IdentDataObject
    {
        [Column("customer_name")] public TString  CustomerName = new TString();
        [Column("total")]         public TDecimal Total        = new TDecimal();
        [Column("active")]        public TBool    Active       = new TBool();

        public MongoTestOrder() { }
        public MongoTestOrder(DataConnection conn) : base(conn) { }
    }

    /// <summary>DataObject without an [Identity] field, for testing PK-less entities.</summary>
    [Table("logs")]
    public class MongoTestLog : DataObject
    {
        [Column("message")]  public TString  Message   = new TString();
        [Column("severity")] public TInt     Severity  = new TInt();

        public MongoTestLog() { }
        public MongoTestLog(DataConnection conn) : base(conn) { }
    }
}
