using System;
using Turquoise.ORM;
using Turquoise.ORM.Examples.Domain;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 04 — Field subsets (partial fetches).
    ///
    /// A FieldSubset controls which columns are included in SELECT or UPDATE.
    /// Use it to:
    ///   • Reduce bandwidth when you only need a few columns from a wide table.
    ///   • Update only specific columns without touching the rest.
    ///   • Exclude large blob/text columns from list queries.
    ///
    /// The connection provides factory methods for creating FieldSubset instances:
    ///   conn.FieldSubset(obj, FieldSubset.InitialState.IncludeAll)  — start with all
    ///   conn.FieldSubset(obj, FieldSubset.InitialState.ExcludeAll)  — start with none
    ///   conn.FieldSubset(obj, FieldSubset.InitialState.Default)     — ORM default
    ///   conn.DefaultFieldSubset(obj)                                — shorthand for Default
    /// </summary>
    public static class FieldSubsets
    {
        public static void Run(DataConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 04 — Field Subsets");
            Console.WriteLine("═══════════════════════════════════════\n");

            // Insert a product to work with
            var setup = new Product(conn);
            setup.Name.SetValue("Subset Test Product");
            setup.Description.SetValue("A long description that we might want to skip.");
            setup.Price.SetValue(29.99m);
            setup.CategoryID.SetValue(1);
            setup.InStock.SetValue(true);
            setup.CreatedAt.SetValue(DateTime.UtcNow);
            setup.Insert();
            int id = (int)setup.ID.GetValue();
            Console.WriteLine($"Setup: inserted product {id}");

            // ── Default full-fetch ─────────────────────────────────────────────────────

            var full = new Product(conn);
            full.ID.SetValue(id);
            full.Read();
            Console.WriteLine($"\nFull read — Name='{full.Name}', Description='{full.Description}', Price={full.Price}");

            // ── Partial fetch: only Name and Price ────────────────────────────────────

            // ExcludeAll starts with nothing included, then we selectively add fields.
            // conn.FieldSubset(obj, enclosing, field) creates a subset containing just that field.
            // The + operator unions subsets.
            var nameSubset  = conn.FieldSubset(full, full, full.Name);
            var priceSubset = conn.FieldSubset(full, full, full.Price);
            var partial     = nameSubset + priceSubset;

            var partialResult = new Product(conn);
            partialResult.ID.SetValue(id);
            conn.Read(partialResult, partial);

            Console.WriteLine($"\nPartial read (Name + Price only):");
            Console.WriteLine($"  Name='{partialResult.Name}'");
            Console.WriteLine($"  Price={partialResult.Price}");
            Console.WriteLine($"  Description loaded: {!partialResult.Description.IsNull()} (should be false/null)");

            // ── Using field subsets in queries ────────────────────────────────────────

            // Pass a FieldSubset to QueryAll to limit returned columns.
            Console.WriteLine("\nQuery with subset (Name only):");
            var nameOnly = conn.FieldSubset(setup, setup, setup.Name);
            var results  = conn.QueryAll(setup, null, null, 0, nameOnly);
            foreach (Product p in results)
                Console.WriteLine($"  Name='{p.Name}', Price loaded: {!p.Price.IsNull()}");

            // ── Partial update ────────────────────────────────────────────────────────

            // Update only the Price without touching other columns.
            // Use UpdateChanged() to update only fields whose value has been set.
            setup.Price.SetValue(39.99m);
            setup.UpdateChanged();
            Console.WriteLine($"\nAfter UpdateChanged: Price should be 39.99");

            var verify = new Product(conn);
            verify.ID.SetValue(id);
            verify.Read();
            Console.WriteLine($"  Verified Price = {verify.Price}");

            // Clean up
            setup.Delete();
            Console.WriteLine("\nCleanup done.");
        }
    }
}
