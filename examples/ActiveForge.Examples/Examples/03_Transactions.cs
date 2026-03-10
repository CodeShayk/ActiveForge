using System;
using ActiveForge;
using ActiveForge.Examples.Domain;

namespace ActiveForge.Examples.Examples
{
    /// <summary>
    /// Example 03 — Transactions.
    ///
    /// ActiveForge supports explicit transactions that wrap multiple operations.
    /// The transaction is obtained from the connection; DataObjects use it automatically
    /// because they are bound to the same connection.
    ///
    /// Pattern:
    ///   using var tx = conn.BeginTransaction();
    ///   ... do work ...
    ///   conn.CommitTransaction(tx);
    ///   // or conn.RollbackTransaction(tx) on error
    ///
    /// The IDisposable implementation rolls back automatically if Commit was not called.
    /// </summary>
    public static class Transactions
    {
        public static void Run(DataConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 03 — Transactions");
            Console.WriteLine("═══════════════════════════════════════\n");

            // ── Successful transaction ─────────────────────────────────────────────────

            Console.WriteLine("Inserting two products in a single transaction...");

            TransactionBase tx = conn.BeginTransaction();
            try
            {
                var p1 = new Product(conn);
                p1.Name.SetValue("Transactional Widget A");
                p1.Price.SetValue(5.00m);
                p1.InStock.SetValue(true);
                p1.Insert();

                var p2 = new Product(conn);
                p2.Name.SetValue("Transactional Widget B");
                p2.Price.SetValue(7.50m);
                p2.InStock.SetValue(true);
                p2.Insert();

                conn.CommitTransaction(tx);
                Console.WriteLine($"  ✓ Both products committed (IDs {(int)p1.ID.GetValue()}, {(int)p2.ID.GetValue()})");

                // Clean up
                p1.Delete();
                p2.Delete();
                Console.WriteLine("  ✓ Cleaned up test products");
            }
            catch (Exception ex)
            {
                conn.RollbackTransaction(tx);
                Console.WriteLine($"  ✗ Transaction rolled back: {ex.Message}");
            }

            // ── Rollback demonstration ────────────────────────────────────────────────

            Console.WriteLine("\nDemonstrating rollback (intentional error)...");

            int countBefore = conn.QueryCount(new Product(conn), null);

            tx = conn.BeginTransaction();
            try
            {
                var p = new Product(conn);
                p.Name.SetValue("This will be rolled back");
                p.Price.SetValue(1.00m);
                p.Insert();
                Console.WriteLine($"  Inserted temporarily (ID {(int)p.ID.GetValue()})");

                // Simulate an application-level error
                throw new InvalidOperationException("Simulated error — rolling back");
            }
            catch (InvalidOperationException ex)
            {
                conn.RollbackTransaction(tx);
                Console.WriteLine($"  ✓ Rolled back after: {ex.Message}");
            }

            int countAfter = conn.QueryCount(new Product(conn), null);
            Console.WriteLine(countBefore == countAfter
                ? $"  ✓ Row count unchanged ({countAfter}) — rollback succeeded"
                : $"  ✗ Row count changed {countBefore} → {countAfter}");

            // ── Action queue (deferred operations) ────────────────────────────────────

            // The action queue lets you accumulate operations and flush them in a single
            // round-trip (useful for batch inserts / bulk updates).
            Console.WriteLine("\nUsing action queue for batch inserts...");

            var q1 = new Product(conn);
            q1.Name.SetValue("Queue Widget 1");
            q1.Price.SetValue(3.00m);
            q1.QueueForInsert();   // deferred — not yet in DB

            var q2 = new Product(conn);
            q2.Name.SetValue("Queue Widget 2");
            q2.Price.SetValue(4.00m);
            q2.QueueForInsert();

            conn.ProcessActionQueue();   // flush all queued operations
            Console.WriteLine("  ✓ Action queue flushed");

            // Clean up
            conn.ClearActionQueue();
            q1.Delete();
            q2.Delete();
        }
    }
}
