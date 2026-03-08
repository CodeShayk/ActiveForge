using System;
using Turquoise.ORM;
using Turquoise.ORM.Examples.Domain;
using Turquoise.ORM.Examples.Examples;
using ExampleTransactions = Turquoise.ORM.Examples.Examples.Transactions;

namespace Turquoise.ORM.Examples
{
    /// <summary>
    /// Turquoise.ORM — Interactive example runner.
    ///
    /// SETUP — SQL Server
    /// ──────────────────
    /// 1. Create a SQL Server database (e.g. "TurquoiseDemo").
    /// 2. Run the SQL in SetupSql.sql against that database to create the tables.
    /// 3. Update SqlServerConnectionString below.
    /// 4. Run this console application and choose provider S.
    ///
    /// SETUP — SQLite
    /// ──────────────
    /// 1. No external database required — SQLite creates the file automatically.
    /// 2. Run this console application and choose provider L.
    ///    The tables are created automatically from SetupSqlite.sql on first run.
    /// </summary>
    internal static class Program
    {
        private const string SqlServerConnectionString =
            "Server=localhost;Database=TurquoiseDemo;Integrated Security=True;" +
            "TrustServerCertificate=True;";

        private const string SQLiteConnectionString = "Data Source=TurquoiseDemo.db";

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintBanner();

            var factory = new ShopFactory();

            Console.WriteLine("  Choose provider:");
            Console.WriteLine("  S — SQL Server");
            Console.WriteLine("  L — SQLite (file-based, no setup required)");
            Console.Write("\n  Enter S or L: ");
            char providerKey = char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar);
            Console.WriteLine();

            DataConnection conn;
            try
            {
                if (providerKey == 'L')
                {
                    var sqliteConn = new SQLiteConnection(SQLiteConnectionString, factory);
                    sqliteConn.Connect();
                    EnsureSQLiteSchema(sqliteConn);
                    conn = sqliteConn;
                    Console.WriteLine($"  Connected to SQLite ({SQLiteConnectionString}).\n");
                }
                else
                {
                    conn = new SqlServerConnection(SqlServerConnectionString, factory);
                    conn.Connect();
                    Console.WriteLine("  Connected to SQL Server.\n");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  Cannot connect: {ex.Message}");
                if (providerKey != 'L')
                {
                    Console.WriteLine("\n  Please update SqlServerConnectionString in Program.cs");
                    Console.WriteLine("  and run the DDL script first (see SetupSql.sql).");
                }
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
                        case '1': BasicCrud.Run(conn);                                  break;
                        case '2': QueryBuilding.Run(conn);                              break;
                        case '3': ExampleTransactions.Run(conn);                        break;
                        case '4': FieldSubsets.Run(conn);                               break;
                        case '5': LazyEnumeration.Run(conn);                            break;
                        case '6':
                            if (conn is SqlServerConnection sqlConn)
                                UnitOfWorkExample.Run(sqlConn);
                            else if (conn is SQLiteConnection liteConn)
                                SQLiteUnitOfWorkExample.Run(liteConn);
                            else
                                Console.WriteLine("  Unit of Work example requires SQL Server or SQLite.");
                            break;
                        case '7':
                            if (conn is SqlServerConnection sqlConn2)
                                LinqQueryingExample.Run(sqlConn2);
                            else
                                Console.WriteLine("  LINQ querying example requires SQL Server.");
                            break;
                        case 'q':
                        case 'Q':
                            Console.WriteLine("  Goodbye!");
                            conn.Disconnect();
                            return;
                        default:
                            Console.WriteLine("  Unknown option — press 1-7 or Q.");
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

        static void EnsureSQLiteSchema(SQLiteConnection conn)
        {
            // Create tables if they don't exist (idempotent).
            conn.ExecSQL(
                "CREATE TABLE IF NOT EXISTS Products (" +
                "  ID         INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  Name       TEXT    NOT NULL DEFAULT ''," +
                "  Price      NUMERIC NOT NULL DEFAULT 0," +
                "  InStock    INTEGER NOT NULL DEFAULT 1," +
                "  Category   TEXT    NOT NULL DEFAULT ''," +
                "  StockCount INTEGER NOT NULL DEFAULT 0)");

            conn.ExecSQL(
                "CREATE TABLE IF NOT EXISTS Orders (" +
                "  ID         INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  ProductID  INTEGER NOT NULL DEFAULT 0," +
                "  Quantity   INTEGER NOT NULL DEFAULT 1," +
                "  Status     TEXT    NOT NULL DEFAULT 'Pending'," +
                "  OrderDate  TEXT    NOT NULL DEFAULT '')");
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
            Console.WriteLine("  6 — Unit of Work (IUnitOfWork, With.Transaction)");
            Console.WriteLine("  7 — LINQ querying (SQL Server only)");
            Console.WriteLine("  Q — Quit");
            Console.Write("\n  Enter 1–7 or Q: ");
        }
    }
}
