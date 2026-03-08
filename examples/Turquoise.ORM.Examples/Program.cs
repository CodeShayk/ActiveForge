using System;
using Turquoise.ORM;
using Turquoise.ORM.Examples.Domain;
using Turquoise.ORM.Examples.Examples;

namespace Turquoise.ORM.Examples
{
    /// <summary>
    /// Turquoise.ORM — Interactive example runner.
    ///
    /// SETUP
    /// ─────
    /// 1. Create a SQL Server database (e.g. "TurquoiseDemo").
    /// 2. Run the SQL in SetupSql.Ddl against that database to create the tables.
    /// 3. Update the connection string below.
    /// 4. Run this console application.
    /// </summary>
    internal static class Program
    {
        // ── Connection string — update to match your SQL Server ───────────────────────
        private const string ConnectionString =
            "Server=localhost;Database=TurquoiseDemo;Integrated Security=True;" +
            "TrustServerCertificate=True;";

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintBanner();

            // The FactoryBase controls polymorphic type mapping.
            // ShopFactory is our application-specific subclass.
            var factory = new ShopFactory();

            // SqlServerConnection is the SQL Server implementation of DataConnection.
            DataConnection conn;
            try
            {
                conn = new SqlServerConnection(ConnectionString, factory);
                conn.Connect();
                Console.WriteLine("  Connected to SQL Server.\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  Cannot connect: {ex.Message}");
                Console.WriteLine("\n  Please update the ConnectionString in Program.cs");
                Console.WriteLine("  and run the DDL script first (see SetupSql.Ddl).");
                Console.ResetColor();
                return;
            }

            while (true)
            {
                PrintMenu();
                var key = Console.ReadKey(intercept: true);
                Console.WriteLine();

                try
                {
                    switch (key.KeyChar)
                    {
                        case '1': BasicCrud.Run(conn);          break;
                        case '2': QueryBuilding.Run(conn);      break;
                        case '3': Transactions.Run(conn);       break;
                        case '4': FieldSubsets.Run(conn);       break;
                        case '5': LazyEnumeration.Run(conn);    break;
                        case 'q':
                        case 'Q':
                            Console.WriteLine("  Goodbye!");
                            conn.Disconnect();
                            return;
                        default:
                            Console.WriteLine("  Unknown option — press 1-5 or Q.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n  ERROR: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"  INNER: {ex.InnerException.Message}");
                    Console.ResetColor();
                }
            }
        }

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║      Turquoise.ORM  — Examples App       ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Select an example:");
            Console.ResetColor();
            Console.WriteLine("  1 — Basic CRUD (Insert / Read / Update / Delete)");
            Console.WriteLine("  2 — Query building (WHERE, ORDER BY, pagination)");
            Console.WriteLine("  3 — Transactions and action queue");
            Console.WriteLine("  4 — Field subsets (partial fetch / partial update)");
            Console.WriteLine("  5 — Lazy enumeration (streaming large result sets)");
            Console.WriteLine("  Q — Quit");
            Console.Write("\n  > ");
        }
    }
}
