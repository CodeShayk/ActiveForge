using System;
using ActiveForge;
using ActiveForge.Examples.Domain;

namespace ActiveForge.Examples.Examples
{
    /// <summary>
    /// Example 01 — Basic CRUD operations.
    ///
    /// This example shows the fundamental Active Record pattern:
    ///   1. Create a Record and set its fields.
    ///   2. Call Insert() to persist it to the database.
    ///   3. Modify a field, call Update() to persist changes.
    ///   4. Call Delete() to remove the row.
    ///   5. Reload a record by primary key using Read().
    /// </summary>
    public static class BasicCrud
    {
        public static void Run(DataConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 01 — Basic CRUD");
            Console.WriteLine("═══════════════════════════════════════\n");

            // ── INSERT ────────────────────────────────────────────────────────────────

            // Each Record is bound to a connection at construction time.
            // The connection is used automatically when you call Insert/Update/Delete/Read.
            var product = new Product(conn);
            product.Name.SetValue("Example Widget");
            product.Description.SetValue("A high-quality widget for testing purposes.");
            product.Price.SetValue(19.99m);
            product.CategoryID.SetValue(1);
            product.InStock.SetValue(true);
            product.CreatedAt.SetValue(DateTime.UtcNow);

            Console.WriteLine("Inserting product...");
            bool inserted = product.Insert();

            if (inserted)
            {
                // After Insert(), the auto-generated ID is populated automatically
                // (via SCOPE_IDENTITY() on SQL Server).
                Console.WriteLine($"  ✓ Inserted with ID = {(int)product.ID.GetValue()}");
            }
            else
            {
                Console.WriteLine("  ✗ Insert returned false");
                return;
            }

            int savedId = (int)product.ID.GetValue();

            // ── READ ──────────────────────────────────────────────────────────────────

            // To read back a record, set the primary key and call Read().
            // This emits: SELECT ... FROM Products WHERE ID = @ID
            var readBack = new Product(conn);
            readBack.ID.SetValue(savedId);
            bool found = readBack.Read();

            Console.WriteLine(found
                ? $"  ✓ Read back: Name='{readBack.Name}', Price={readBack.Price}"
                : $"  ✗ Record {savedId} not found after insert");

            // ── UPDATE ────────────────────────────────────────────────────────────────

            // Modify the product and persist the change.
            // UpdateOption.IgnoreLock skips optimistic-locking checks (simplest mode).
            product.Name.SetValue("Updated Widget");
            product.Price.SetValue(24.99m);

            Console.WriteLine("Updating product name and price...");
            product.Update(RecordLock.UpdateOption.IgnoreLock);

            // Read it back to confirm
            var afterUpdate = new Product(conn);
            afterUpdate.ID.SetValue(savedId);
            afterUpdate.Read();
            Console.WriteLine($"  ✓ After update: Name='{afterUpdate.Name}', Price={afterUpdate.Price}");

            // ── UPDATE ALL FIELDS ─────────────────────────────────────────────────────

            // UpdateAll() emits an UPDATE that includes EVERY column, regardless of
            // whether it changed.  Use when you want to overwrite with a known-good state.
            product.Price.SetValue(9.99m);
            product.UpdateAll();
            Console.WriteLine($"  ✓ UpdateAll: Price now {product.Price}");

            // ── DELETE ────────────────────────────────────────────────────────────────

            Console.WriteLine("Deleting product...");
            bool deleted = product.Delete();
            Console.WriteLine(deleted ? "  ✓ Deleted successfully" : "  ✗ Delete failed");

            // Verify it's gone
            var check = new Product(conn);
            check.ID.SetValue(savedId);
            bool stillExists = check.Read();
            Console.WriteLine(stillExists
                ? $"  ✗ Product {savedId} still exists!"
                : $"  ✓ Confirmed: product {savedId} no longer exists");
        }
    }
}
