using System;
using ActiveForge;
using ActiveForge.Transactions;
using ActiveForge.Examples.Domain;

namespace ActiveForge.Examples.Examples
{
    /// <summary>
    /// Example 08 — Unit of Work with SQLite.
    ///
    /// Demonstrates:
    ///   A. Basic With.Transaction usage with SQLiteUnitOfWork
    ///   B. Rollback on exception
    ///   C. Auto-transaction via conn.UnitOfWork
    /// </summary>
    public static class SQLiteUnitOfWorkExample
    {
        public static void Run(SQLiteConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 08 — SQLite Unit of Work");
            Console.WriteLine("═══════════════════════════════════════\n");

            BasicTransaction(conn);
            RollbackOnException(conn);
            AutoTransactionViaProperty(conn);
        }

        // ── A: Basic With.Transaction ─────────────────────────────────────────────────

        static void BasicTransaction(SQLiteConnection conn)
        {
            Console.WriteLine("  A. Basic With.Transaction");

            using IUnitOfWork uow = new SQLiteUnitOfWork(conn);

            With.Transaction(uow, () =>
            {
                var product = new Product(conn);
                product.Name.SetValue("SQLite Widget");
                product.Price.SetValue(9.99m);
                product.InStock.SetValue(true);
                conn.Insert(product);

                product.Price.SetValue(7.99m);
                product.Update();

                Console.WriteLine($"     Inserted product ID={product.ID.GetValue()}, price updated to 7.99");
            });

            Console.WriteLine("     Committed.\n");
        }

        // ── B: Rollback on exception ──────────────────────────────────────────────────

        static void RollbackOnException(SQLiteConnection conn)
        {
            Console.WriteLine("  B. Rollback on exception");

            using IUnitOfWork uow = new SQLiteUnitOfWork(conn);

            int countBefore = conn.QueryCount(new Product(conn), null);

            try
            {
                With.Transaction(uow, () =>
                {
                    var product = new Product(conn);
                    product.Name.SetValue("WillRollBack");
                    product.Price.SetValue(1.00m);
                    product.InStock.SetValue(true);
                    conn.Insert(product);

                    throw new InvalidOperationException("Simulated failure — rolling back.");
                });
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"     Caught: {ex.Message}");
            }

            int countAfter = conn.QueryCount(new Product(conn), null);
            Console.WriteLine($"     Row count before={countBefore}, after={countAfter} (unchanged = rollback OK)\n");
        }

        // ── C: Auto-transaction via conn.UnitOfWork ───────────────────────────────────

        static void AutoTransactionViaProperty(SQLiteConnection conn)
        {
            Console.WriteLine("  C. Auto-transaction via conn.UnitOfWork");

            var uow = new SQLiteUnitOfWork(conn);
            conn.UnitOfWork = uow;
            try
            {
                // Each write auto-opens conn, begins tx, executes, commits, closes.
                var p1 = new Product(conn);
                p1.Name.SetValue("Auto-Tx Product A");
                p1.Price.SetValue(3.00m);
                p1.InStock.SetValue(true);
                p1.Insert();

                var p2 = new Product(conn);
                p2.Name.SetValue("Auto-Tx Product B");
                p2.Price.SetValue(4.00m);
                p2.InStock.SetValue(false);
                p2.Insert();

                Console.WriteLine($"     Inserted IDs {p1.ID.GetValue()} and {p2.ID.GetValue()} via auto-transaction.");
            }
            finally
            {
                conn.UnitOfWork = null;
                // Re-open for subsequent examples
                if (!conn.IsOpen) conn.Connect();
            }

            Console.WriteLine();
        }
    }
}
