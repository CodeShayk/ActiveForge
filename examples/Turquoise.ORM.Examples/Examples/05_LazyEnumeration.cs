using System;
using Turquoise.ORM;
using Turquoise.ORM.Examples.Domain;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 05 — Lazy enumeration for large result sets.
    ///
    /// QueryAll() loads the entire result set into an ObjectCollection in memory.
    /// For large tables (thousands of rows) this can be expensive.
    ///
    /// LazyQueryAll&lt;T&gt;() instead returns an IEnumerable&lt;T&gt; that streams rows
    /// one at a time as you iterate.  The database connection is held open for the
    /// duration of the enumeration, so:
    ///   • Memory usage stays O(1) per row rather than O(n) total.
    ///   • You MUST iterate to completion (or Dispose the enumerator) to close the reader.
    ///   • Avoid performing further queries on the same connection inside the loop
    ///     unless you use a second connection.
    /// </summary>
    public static class LazyEnumeration
    {
        public static void Run(DataConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 05 — Lazy Enumeration");
            Console.WriteLine("═══════════════════════════════════════\n");

            // Insert some test products
            Console.WriteLine("Seeding 5 test products...");
            for (int i = 1; i <= 5; i++)
            {
                var p = new Product(conn);
                p.Name.SetValue($"Lazy Product {i}");
                p.Price.SetValue(i * 2.50m);
                p.InStock.SetValue(i % 2 == 0);
                p.Insert();
            }

            var template = new Product(conn);

            // ── QueryAll — loads all rows at once ──────────────────────────────────────

            Console.WriteLine("\nQueryAll (all rows in memory):");
            var all = conn.QueryAll(template, null, null, 0, null);
            Console.WriteLine($"  Loaded {all.Count} product(s) into ObjectCollection");

            // ── LazyQueryAll — streams rows ────────────────────────────────────────────

            Console.WriteLine("\nLazyQueryAll (streaming one at a time):");
            var sort = new OrderAscending(template, template.Price);
            int count = 0;
            decimal runningTotal = 0m;

            // LazyQueryAll returns IEnumerable<Product>
            // The underlying DataReader is held open until the foreach completes.
            foreach (Product product in conn.LazyQueryAll(template, null, sort, 0, null))
            {
                count++;
                runningTotal += (decimal)product.Price.GetValue();
                Console.WriteLine($"  [{count}] {product.Name} — £{product.Price}");

                // TIP: If you break early, the reader is NOT automatically closed.
                // Always iterate to completion or wrap in a try/finally with Dispose.
            }

            Console.WriteLine($"\n  Streamed {count} product(s), total value = £{runningTotal:F2}");

            // ── Lazy query with a WHERE clause ────────────────────────────────────────

            Console.WriteLine("\nLazyQueryAll with filter (InStock = true):");
            var inStockTerm = new EqualTerm(template, template.InStock, true);
            int inStockCount = 0;

            foreach (Product p in conn.LazyQueryAll(template, inStockTerm, sort, 0, null))
            {
                inStockCount++;
                Console.WriteLine($"  {p.Name} — in stock");
            }
            Console.WriteLine($"  {inStockCount} in-stock product(s) found");

            // ── Clean up ──────────────────────────────────────────────────────────────

            Console.WriteLine("\nCleaning up seeded products...");
            var cleanup = conn.QueryAll(template,
                new ContainsTerm(template, template.Name, "Lazy Product"), null, 0, null);
            foreach (DataObject obj in cleanup)
                ((Product)obj).Delete();
            Console.WriteLine($"  Deleted {cleanup.Count} test product(s)");
        }
    }
}
