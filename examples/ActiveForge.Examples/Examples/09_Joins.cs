using System;
using System.Collections.Generic;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Examples.Domain;
using ActiveForge.Query;

namespace ActiveForge.Examples.Examples
{
    // ── Join-aware entity definitions ─────────────────────────────────────────────
    //
    // These entity classes extend the Domain models by embedding related DataObjects,
    // which causes the ORM to emit JOIN SQL automatically.  They all map to the same
    // underlying tables as the Domain models and can be used alongside them.

    // ── Pattern 1: Naming-convention INNER JOIN ───────────────────────────────────
    //
    // Rule: if a class has:
    //   • a TForeignKey field named XID   (e.g. CategoryID)
    //   • a Record field named X      (e.g. Category) whose type name ends in X
    // the ORM creates:
    //   INNER JOIN Categories ON Products.CategoryID = Categories.ID
    //
    // No attribute required — the convention is detected at binding time.
    [Table("Products")]
    public class ProductWithCategory : IdentityRecord
    {
        [Column("Name")]        public TString     Name       = new TString();
        [Column("Price")]       public TDecimal    Price      = new TDecimal();
        [Column("InStock")]     public TBool       InStock    = new TBool();
        [Column("CategoryID")]  public TForeignKey CategoryID = new TForeignKey();

        // Embedding a Category here triggers the auto-join.
        // After a query, Category.Name etc. are populated from the joined row.
        public Category Category = new Category();

        public ProductWithCategory() { }
        public ProductWithCategory(DataConnection conn) : base(conn) { }
    }

    // ── Pattern 2: Explicit LEFT OUTER JOIN via [JoinSpec] ────────────────────────
    //
    // [JoinSpec] lets you override the join type (and optionally rename fields).
    // Here we use LeftOuterJoin so that products with a NULL CategoryID are
    // still returned (Category fields will be null/empty for those rows).
    [Table("Products")]
    [JoinSpec("CategoryID", "Category", "ID", JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
    public class ProductWithOptionalCategory : IdentityRecord
    {
        [Column("Name")]        public TString     Name       = new TString();
        [Column("Price")]       public TDecimal    Price      = new TDecimal();
        [Column("InStock")]     public TBool       InStock    = new TBool();
        [Column("CategoryID")]  public TForeignKey CategoryID = new TForeignKey();

        // [JoinSpec] above controls the join type; the field still triggers hydration.
        public Category Category = new Category();

        public ProductWithOptionalCategory() { }
        public ProductWithOptionalCategory(DataConnection conn) : base(conn) { }
    }

    // ── Pattern 3: Multiple FK joins on a single entity ───────────────────────────
    //
    // OrderLines has two foreign keys.  Embedding both Order and Product causes the
    // ORM to emit two joins in one query:
    //   INNER JOIN Orders   ON OrderLines.OrderID   = Orders.ID
    //   INNER JOIN Products ON OrderLines.ProductID = Products.ID
    //
    // Both are resolved by the naming convention (no attribute needed):
    //   OrderID   → prefix "Order"   → embedded field "Order"   whose type ends in "Order"
    //   ProductID → prefix "Product" → embedded field "Product" whose type ends in "Product"
    [Table("OrderLines")]
    public class OrderLineWithDetails : IdentityRecord
    {
        [Column("OrderID")]   public TForeignKey OrderID   = new TForeignKey();
        [Column("ProductID")] public TForeignKey ProductID = new TForeignKey();
        [Column("Quantity")]  public TInt        Quantity  = new TInt();
        [Column("UnitPrice")] public TDecimal    UnitPrice = new TDecimal();

        public Order   Order   = new Order();    // triggers join → Orders table
        public Product Product = new Product();  // triggers join → Products table

        public OrderLineWithDetails() { }
        public OrderLineWithDetails(DataConnection conn) : base(conn) { }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Example 09 — Joins.
    ///
    /// ActiveForge produces JOIN SQL by inspecting embedded Record fields on
    /// entity classes.  No raw SQL or fluent builder is required.
    ///
    /// Patterns demonstrated:
    ///   1. Auto INNER JOIN  — naming convention (TForeignKey XID + embedded X)
    ///   2. Filter on join   — EqualTerm / comparison on an embedded object's TField
    ///   3. LEFT OUTER JOIN  — [JoinSpec] attribute with LeftOuterJoin
    ///   4. Multi-FK joins   — two embedded DataObjects on one entity class
    ///   5. EXISTS sub-query — ExistsTerm&lt;T&gt; for has-many / semi-join filtering
    ///
    /// Requires: SQL Server with SetupSql.sql applied (seed data for Categories + Products).
    /// Orders and OrderLines are inserted and removed by this example.
    /// </summary>
    public static class JoinsExample
    {
        public static void Run(SqlServerConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 09 — Joins");
            Console.WriteLine("═══════════════════════════════════════\n");

            // ── Seed check ────────────────────────────────────────────────────────────

            // The joins examples for parts 1-3 rely on Categories and Products existing.
            // Run SetupSql.sql first if the table is empty.
            var pCheck = new Product(conn);
            if (conn.QueryCount(pCheck, null) == 0)
            {
                Console.WriteLine("  Products table is empty — run SetupSql.sql first.");
                return;
            }

            // ── 1. Auto INNER JOIN via naming convention ──────────────────────────────

            Console.WriteLine("── 1. Auto INNER JOIN (naming convention) ──");
            Console.WriteLine("   TForeignKey CategoryID + public Category Category");
            Console.WriteLine("   → INNER JOIN Categories ON Products.CategoryID = Categories.ID\n");

            var withCatTemplate = new ProductWithCategory(conn);
            var allWithCat = conn.QueryAll(withCatTemplate, null, null, 0, null);

            Console.WriteLine($"   Products (joined, {allWithCat.Count} rows):");
            foreach (ProductWithCategory p in allWithCat)
            {
                string name = (string)p.Name.GetValue();
                string cat  = p.Category.Name.IsNull() ? "(none)" : (string)p.Category.Name.GetValue();
                Console.WriteLine($"     {name,-30} | {cat}");
            }

            // ── 2. Filter on an embedded (joined) column ──────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 2. Filter on joined column ──");
            Console.WriteLine("   EqualTerm(template, template.Category.Name, \"Books\")");
            Console.WriteLine("   The ORM resolves Category.Name → Categories.Name in the WHERE clause.\n");

            // Use the embedded field reference directly in the term — the ORM maps it
            // to the Categories.Name column through the join binding.
            var booksTerm    = new EqualTerm(withCatTemplate, withCatTemplate.Category.Name, "Books");
            var books        = conn.QueryAll(withCatTemplate, booksTerm, null, 0, null);

            Console.WriteLine($"   Books ({books.Count}):");
            foreach (ProductWithCategory p in books)
                Console.WriteLine($"     {(string)p.Name.GetValue()} — ${(decimal)p.Price.GetValue():F2}");

            // Compose a joined-column filter with an own-column filter using &
            Console.WriteLine();
            Console.WriteLine("   Books AND in-stock (booksTerm & inStockTerm):");
            var inStockTerm  = new EqualTerm(withCatTemplate, withCatTemplate.InStock, true);
            var booksInStock = conn.QueryAll(withCatTemplate, booksTerm & inStockTerm, null, 0, null);
            foreach (ProductWithCategory p in booksInStock)
                Console.WriteLine($"     {(string)p.Name.GetValue()}");

            // ── 3. LEFT OUTER JOIN via [JoinSpec] ─────────────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 3. LEFT OUTER JOIN via [JoinSpec] ──");
            Console.WriteLine("   [JoinSpec(\"CategoryID\", \"Category\", \"ID\", LeftOuterJoin)]");
            Console.WriteLine("   Returns every product row; category fields are null if no match.\n");

            var outerTemplate = new ProductWithOptionalCategory(conn);
            var allProducts   = conn.QueryAll(outerTemplate, null, null, 0, null);

            Console.WriteLine($"   All products, left-outer-joined ({allProducts.Count} rows):");
            foreach (ProductWithOptionalCategory p in allProducts)
            {
                string name    = (string)p.Name.GetValue();
                string catName = p.Category.Name.IsNull() ? "<no category>" : (string)p.Category.Name.GetValue();
                Console.WriteLine($"     {name,-30} | {catName}");
            }

            // ── 4. Multi-FK joins ──────────────────────────────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 4. Multi-FK joins — OrderLines → Orders + Products ──");
            Console.WriteLine("   Two embedded DataObjects → two INNER JOINs in one query.\n");

            // Insert test data so there is something to join.
            // Using Domain entities (no embedded objects) for the inserts.
            var pAll  = conn.QueryAll(new Product(conn), null, null, 2, null);
            int prod1 = (int)((Product)pAll[0]).ID.GetValue();
            int prod2 = (int)((Product)pAll[1]).ID.GetValue();

            var ord1 = new Order(conn);
            ord1.CustomerName.SetValue("Alice (JoinTest)");
            ord1.OrderDate.SetValue(DateTime.UtcNow);
            ord1.TotalAmount.SetValue(119.98m);
            ord1.Status.SetValue("Shipped");
            ord1.Insert();

            var ord2 = new Order(conn);
            ord2.CustomerName.SetValue("Bob (JoinTest)");
            ord2.OrderDate.SetValue(DateTime.UtcNow);
            ord2.TotalAmount.SetValue(29.99m);
            ord2.Status.SetValue("Pending");
            ord2.Insert();

            int orderId1 = (int)ord1.ID.GetValue();
            int orderId2 = (int)ord2.ID.GetValue();

            var line1 = InsertLine(conn, orderId1, prod1, 1, (decimal)((Product)pAll[0]).Price.GetValue());
            var line2 = InsertLine(conn, orderId1, prod2, 3, (decimal)((Product)pAll[1]).Price.GetValue());
            var line3 = InsertLine(conn, orderId2, prod2, 1, (decimal)((Product)pAll[1]).Price.GetValue());

            var lineTemplate = new OrderLineWithDetails(conn);
            var lines        = conn.QueryAll(lineTemplate, null, null, 0, null);

            Console.WriteLine($"   Order lines ({lines.Count} rows):");
            foreach (OrderLineWithDetails line in lines)
            {
                string customer = line.Order.CustomerName.IsNull() ? "?" : (string)line.Order.CustomerName.GetValue();
                string product  = line.Product.Name.IsNull()       ? "?" : (string)line.Product.Name.GetValue();
                int    qty      = (int)line.Quantity.GetValue();
                decimal price   = (decimal)line.UnitPrice.GetValue();
                Console.WriteLine($"     {customer,-22} | {product,-25} | qty {qty,2} | ${price:F2}");
            }

            // ── 5. EXISTS sub-query (has-many / semi-join filter) ─────────────────────

            Console.WriteLine();
            Console.WriteLine("── 5. EXISTS sub-query ──");
            Console.WriteLine("   ExistsTerm<OrderLine>(order, lineObj, lineObj.OrderID, subQuery)");
            Console.WriteLine("   → WHERE EXISTS (SELECT 1 FROM OrderLines WHERE Quantity > 1");
            Console.WriteLine("                   AND OrderLines.OrderID = Orders.ID)\n");

            var orderTemplate = new Order(conn);
            var lineObj       = new OrderLine(conn);

            // The sub-query adds its own WHERE; ExistsTerm appends the correlation condition.
            var bigQty   = new GreaterThanTerm(lineObj, lineObj.Quantity, 1);
            var subQuery = new Query<OrderLine>(lineObj, conn).Where(bigQty);

            // Convenience constructor links outer Orders.ID ↔ inner OrderLines.OrderID.
            var existsTerm         = new ExistsTerm<OrderLine>(orderTemplate, lineObj, lineObj.OrderID, subQuery);
            var ordersWithBigLines = conn.QueryAll(orderTemplate, existsTerm, null, 0, null);

            int totalOrders = conn.QueryCount(orderTemplate, null);
            Console.WriteLine($"   Orders with at least one line qty > 1 : {ordersWithBigLines.Count} of {totalOrders}");
            foreach (Order o in ordersWithBigLines)
                Console.WriteLine($"     {(string)o.CustomerName.GetValue()} — {(string)o.Status.GetValue()}");

            // EXISTS with no sub-filter — "orders that have ANY line at all"
            var anyLine       = new ExistsTerm<OrderLine>(orderTemplate, lineObj, lineObj.OrderID,
                                    new Query<OrderLine>(lineObj, conn));
            var ordersWithAny = conn.QueryAll(orderTemplate, anyLine, null, 0, null);
            Console.WriteLine($"   Orders with any line at all            : {ordersWithAny.Count} of {totalOrders}");

            // ── Cleanup ───────────────────────────────────────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── Cleanup — removing test orders and lines ──");

            line1.Delete();
            line2.Delete();
            line3.Delete();
            ord1.Delete();
            ord2.Delete();

            Console.WriteLine("   Done.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static OrderLine InsertLine(
            DataConnection conn, int orderId, int productId, int qty, decimal price)
        {
            var line = new OrderLine(conn);
            line.OrderID.SetValue(orderId);
            line.ProductID.SetValue(productId);
            line.Quantity.SetValue(qty);
            line.UnitPrice.SetValue(price);
            line.Insert();
            return line;
        }
    }
}
