using System;
using System.Collections.Generic;
using System.Linq;
using Turquoise.ORM;
using Turquoise.ORM.Linq;
using Turquoise.ORM.Examples.Domain;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 07 — LINQ Query Support.
    ///
    /// Demonstrates:
    ///   A. Basic equality predicate
    ///   B. Comparison operators
    ///   C. Logical AND / OR / NOT
    ///   D. Null checks (IS NULL / IS NOT NULL)
    ///   E. IN clause (List.Contains)
    ///   F. Sorting (OrderBy, ThenBy)
    ///   G. Pagination (Skip / Take)
    ///   H. Chained Where calls (all ANDed together)
    ///   I. Local variable capture
    ///   J. Full chain
    /// </summary>
    public static class LinqQueryingExample
    {
        // ── A: Basic equality ─────────────────────────────────────────────────────────

        public static void BasicWhere(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07A: Basic Where ===");

            List<Product> products = conn.Query<Product>()
                .Where(p => p.Name == "Widget")
                .ToList();

            Console.WriteLine($"  Found {products.Count} product(s) named 'Widget'.");
            foreach (var p in products)
                Console.WriteLine($"    {(string)p.Name.GetValue()} — ${(decimal)p.Price.GetValue():F2}");
        }

        // ── B: Comparison operators ───────────────────────────────────────────────────

        public static void ComparisonOperators(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07B: Comparison Operators ===");

            int expensive = conn.Query<Product>().Where(p => p.Price >= 50m).ToList().Count;
            Console.WriteLine($"  Price >= 50: {expensive}");

            int cheap = conn.Query<Product>().Where(p => p.Price < 10m).ToList().Count;
            Console.WriteLine($"  Price < 10: {cheap}");

            int notWidget = conn.Query<Product>().Where(p => p.Name != "Widget").ToList().Count;
            Console.WriteLine($"  Not 'Widget': {notWidget}");
        }

        // ── C: Logical composition ────────────────────────────────────────────────────

        public static void LogicalComposition(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07C: Logical Composition ===");

            // AND
            int inStockExpensive = conn.Query<Product>()
                .Where(p => p.InStock == true && p.Price > 20m)
                .ToList().Count;
            Console.WriteLine($"  InStock AND Price > 20: {inStockExpensive}");

            // OR
            int extremes = conn.Query<Product>()
                .Where(p => p.Price < 5m || p.Price > 100m)
                .ToList().Count;
            Console.WriteLine($"  Price < 5 OR > 100: {extremes}");

            // NOT
            int outOfStock = conn.Query<Product>()
                .Where(p => !(p.InStock == true))
                .ToList().Count;
            Console.WriteLine($"  NOT InStock: {outOfStock}");
        }

        // ── D: Null checks ────────────────────────────────────────────────────────────

        public static void NullChecks(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07D: Null Checks ===");

            int noName = conn.Query<Product>()
                .Where(p => p.Name == (TString)null)
                .ToList().Count;
            Console.WriteLine($"  Name IS NULL: {noName}");

            int hasName = conn.Query<Product>()
                .Where(p => p.Name != (TString)null)
                .ToList().Count;
            Console.WriteLine($"  Name IS NOT NULL: {hasName}");
        }

        // ── E: IN clause ──────────────────────────────────────────────────────────────

        public static void InClause(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07E: IN Clause (Contains) ===");

            var featuredNames = new List<string> { "Widget", "Gadget", "Gizmo" };

            List<Product> featured = conn.Query<Product>()
                .Where(p => featuredNames.Contains(p.Name))
                .ToList();

            Console.WriteLine($"  Found {featured.Count} featured product(s).");
            foreach (var p in featured)
                Console.WriteLine($"    {(string)p.Name.GetValue()}");
        }

        // ── F: Sorting ────────────────────────────────────────────────────────────────

        public static void Sorting(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07F: Sorting ===");

            List<Product> byName = conn.Query<Product>()
                .OrderBy(p => p.Name)
                .Take(5)
                .ToList();
            Console.WriteLine("  Top 5 by Name ASC:");
            foreach (var p in byName)
                Console.WriteLine($"    {(string)p.Name.GetValue()}");

            List<Product> byPriceDesc = conn.Query<Product>()
                .OrderByDescending(p => p.Price)
                .Take(3)
                .ToList();
            Console.WriteLine("  Top 3 by Price DESC:");
            foreach (var p in byPriceDesc)
                Console.WriteLine($"    {(string)p.Name.GetValue()} — ${(decimal)p.Price.GetValue():F2}");
        }

        // ── G: Pagination ─────────────────────────────────────────────────────────────

        public static void Pagination(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07G: Pagination ===");

            const int pageSize = 5;

            for (int page = 0; page < 3; page++)
            {
                List<Product> pageResults = conn.Query<Product>()
                    .Where(p => p.InStock == true)
                    .OrderBy(p => p.Name)
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToList();

                Console.WriteLine($"  Page {page + 1}: {pageResults.Count} item(s)");
                foreach (var p in pageResults)
                    Console.WriteLine($"    {(string)p.Name.GetValue()}");

                if (pageResults.Count < pageSize)
                {
                    Console.WriteLine("  (last page)");
                    break;
                }
            }
        }

        // ── H: Chained Where (auto-AND) ───────────────────────────────────────────────

        public static void ChainedWhere(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07H: Chained Where ===");

            List<Product> results = conn.Query<Product>()
                .Where(p => p.InStock  == true)
                .Where(p => p.Price    > 10m)
                .Where(p => p.Name     != "Discontinued")
                .OrderBy(p => p.Price)
                .Take(10)
                .ToList();

            Console.WriteLine($"  InStock + Price>10 + !=Discontinued: {results.Count}");
        }

        // ── I: Local variable capture ─────────────────────────────────────────────────

        public static void LocalVariableCapture(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07I: Local Variable Capture ===");

            string  searchName = "Widget";
            decimal minPrice   = 5m;
            decimal maxPrice   = 50m;

            List<Product> results = conn.Query<Product>()
                .Where(p => p.Name  == searchName)
                .Where(p => p.Price >= minPrice)
                .Where(p => p.Price <= maxPrice)
                .ToList();

            Console.WriteLine($"  '{searchName}' in ${minPrice:F2}–${maxPrice:F2}: {results.Count}");
        }

        // ── J: Full chain ─────────────────────────────────────────────────────────────

        public static void FullChain(SqlServerConnection conn)
        {
            Console.WriteLine("\n=== 07J: Full Chain ===");

            var featuredIds = new List<string> { "Widget", "Gadget" };
            decimal min     = 5m;
            int     skip    = 0;
            int     take    = 10;

            List<Product> results = conn.Query<Product>()
                .Where(p => featuredIds.Contains(p.Name) || p.Price > min)
                .Where(p => p.InStock == true)
                .OrderBy(p => p.Price)
                .ThenBy(p => p.Name)
                .Skip(skip)
                .Take(take)
                .ToList();

            Console.WriteLine($"  Full chain: {results.Count} product(s).");
            foreach (var p in results)
                Console.WriteLine($"    {(string)p.Name.GetValue(),-20} ${(decimal)p.Price.GetValue(),8:F2}  InStock={(bool)p.InStock.GetValue()}");
        }

        // ── Runner ────────────────────────────────────────────────────────────────────

        public static void Run(SqlServerConnection conn)
        {
            Console.WriteLine("\n╔══════════════════════════════════════╗");
            Console.WriteLine("║  Example 07 — LINQ Querying           ║");
            Console.WriteLine("╚══════════════════════════════════════╝");

            BasicWhere(conn);
            ComparisonOperators(conn);
            LogicalComposition(conn);
            NullChecks(conn);
            InClause(conn);
            Sorting(conn);
            Pagination(conn);
            ChainedWhere(conn);
            LocalVariableCapture(conn);
            FullChain(conn);
        }
    }
}
