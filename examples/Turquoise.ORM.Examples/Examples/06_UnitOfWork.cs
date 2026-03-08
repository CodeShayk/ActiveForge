using System;
using System.Data;
using Castle.DynamicProxy;
using Turquoise.ORM;
using Turquoise.ORM.Transactions;
using Turquoise.ORM.Examples.Domain;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 06 — Automatic Transaction Handling with IUnitOfWork and Castle DynamicProxy.
    ///
    /// Demonstrates:
    ///   A. Basic With.Transaction usage
    ///   B. Nested transactions (depth counter)
    ///   C. Rollback on exception
    ///   D. Serializable isolation shorthand
    ///   E. TurquoiseServiceLocator setup
    ///   F. [Transaction] attribute-based interception
    /// </summary>
    public static class UnitOfWorkExample
    {
        // ── A: Basic With.Transaction ─────────────────────────────────────────────────

        public static void BasicTransaction(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06A: Basic With.Transaction ===");

            using IUnitOfWork uow = new SqlServerUnitOfWork(conn);

            With.Transaction(uow, () =>
            {
                var product = new Product(conn);
                product.Name.SetValue("Transaction Widget");
                product.Price.SetValue(49.99m);
                product.InStock.SetValue(true);
                product.CreatedAt.SetValue(DateTime.UtcNow);
                conn.Insert(product);

                product.Price.SetValue(44.99m);
                product.Update();

                Console.WriteLine($"  Inserted and updated product ID={product.ID.GetValue()}");
            });

            Console.WriteLine("  Committed.");
        }

        // ── B: Nested transactions ────────────────────────────────────────────────────

        public static void NestedTransactions(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06B: Nested Transactions ===");

            using IUnitOfWork uow = new SqlServerUnitOfWork(conn);

            With.Transaction(uow, () =>
            {
                var outer = new Product(conn);
                outer.Name.SetValue("Outer Product");
                outer.Price.SetValue(10m);
                outer.InStock.SetValue(true);
                outer.CreatedAt.SetValue(DateTime.UtcNow);
                conn.Insert(outer);

                // Inner call reuses the same ADO.NET transaction (depth counter increments).
                With.Transaction(uow, () =>
                {
                    var inner = new Product(conn);
                    inner.Name.SetValue("Inner Product");
                    inner.Price.SetValue(5m);
                    inner.InStock.SetValue(true);
                    inner.CreatedAt.SetValue(DateTime.UtcNow);
                    conn.Insert(inner);
                    Console.WriteLine($"  Inner product ID={inner.ID.GetValue()} (not yet committed)");
                });

                Console.WriteLine($"  Outer product ID={outer.ID.GetValue()} (not yet committed)");
            });

            Console.WriteLine("  Both committed in one transaction.");
        }

        // ── C: Rollback on exception ──────────────────────────────────────────────────

        public static void RollbackOnException(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06C: Rollback on Exception ===");

            using IUnitOfWork uow = new SqlServerUnitOfWork(conn);

            try
            {
                With.Transaction(uow, () =>
                {
                    var product = new Product(conn);
                    product.Name.SetValue("Will Not Be Saved");
                    product.Price.SetValue(0m);
                    product.InStock.SetValue(false);
                    product.CreatedAt.SetValue(DateTime.UtcNow);
                    conn.Insert(product);

                    Console.WriteLine($"  Inserted ID={product.ID.GetValue()} (will roll back)");
                    throw new ApplicationException("Simulated failure — rolling back.");
                });
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine($"  Caught: {ex.Message}");
                Console.WriteLine("  Transaction rolled back — product was NOT saved.");
            }
        }

        // ── D: Serializable isolation ─────────────────────────────────────────────────

        public static void SerializableIsolation(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06D: Serializable Isolation ===");

            using IUnitOfWork uow = new SqlServerUnitOfWork(conn);

            With.SerializableTransaction(uow, () =>
            {
                var product = new Product(conn);
                product.Name.SetValue("Serializable Product");
                product.Price.SetValue(99.99m);
                product.InStock.SetValue(true);
                product.CreatedAt.SetValue(DateTime.UtcNow);
                conn.Insert(product);
                Console.WriteLine($"  Inserted under Serializable isolation ID={product.ID.GetValue()}");
            });
        }

        // ── E: Service Locator setup ──────────────────────────────────────────────────

        public static void ServiceLocatorSetup(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06E: TurquoiseServiceLocator ===");

            TurquoiseServiceLocator.SetUnitOfWorkFactory(
                () => new SqlServerUnitOfWork(conn));

            // With.Transaction (no-arg form) resolves the UoW via the locator.
            With.Transaction(() =>
            {
                var product = new Product(conn);
                product.Name.SetValue("Locator Product");
                product.Price.SetValue(19.99m);
                product.InStock.SetValue(true);
                product.CreatedAt.SetValue(DateTime.UtcNow);
                conn.Insert(product);
                Console.WriteLine($"  Inserted via locator ID={product.ID.GetValue()}");
            });

            TurquoiseServiceLocator.Reset();
            Console.WriteLine("  Locator reset.");
        }

        // ── F: [Transaction] attribute interception ───────────────────────────────────

        /// <summary>Service whose virtual methods are intercepted by Castle DynamicProxy.</summary>
        public class ProductService
        {
            protected readonly SqlServerConnection _conn;

            public ProductService(SqlServerConnection conn) { _conn = conn; }

            [Transaction(IsolationLevel.ReadCommitted)]
            public virtual int CreateProduct(string name, decimal price)
            {
                var product = new Product(_conn);
                product.Name.SetValue(name);
                product.Price.SetValue(price);
                product.InStock.SetValue(true);
                product.CreatedAt.SetValue(DateTime.UtcNow);
                _conn.Insert(product);
                return (int)product.ID.GetValue();
            }

            [Transaction]
            public virtual void MarkOutOfStock(int id)
            {
                var product = new Product(_conn);
                product.ID.SetValue(id);
                _conn.Read(product);
                product.InStock.SetValue(false);
                product.Update();
            }

            // No [Transaction] — passes through without starting a new transaction.
            public virtual int CountProducts()
                => _conn.QueryCount(new Product(_conn));
        }

        public static void AttributeInterception(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 06F: [Transaction] Attribute Interception ===");

            // DataConnectionProxyFactory proxies DataConnection subclasses (strategy C1).
            // For arbitrary service classes, use Castle's ProxyGenerator directly.
            using IUnitOfWork uow = new SqlServerUnitOfWork(conn);
            var interceptor = new TransactionInterceptor(uow);
            var generator   = new ProxyGenerator();

            ProductService real  = new ProductService(conn);
            ProductService proxy = (ProductService)generator.CreateClassProxyWithTarget(
                typeof(ProductService), real, interceptor);

            int id = proxy.CreateProduct("Intercepted Product", 29.99m);
            Console.WriteLine($"  Created product ID={id} (transaction committed automatically)");

            proxy.MarkOutOfStock(id);
            Console.WriteLine($"  Marked ID={id} out-of-stock (transaction committed automatically)");

            int count = proxy.CountProducts();
            Console.WriteLine($"  Total products (no transaction): {count}");
        }

        // ── Runner ────────────────────────────────────────────────────────────────────

        public static void Run(SqlServerConnection conn)
        {
            Console.WriteLine("\n╔══════════════════════════════════════╗");
            Console.WriteLine("║  Example 06 — Unit of Work            ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            BasicTransaction(conn);
            NestedTransactions(conn);
            RollbackOnException(conn);
            SerializableIsolation(conn);
            ServiceLocatorSetup(conn);
            AttributeInterception(conn);
        }
    }
}
