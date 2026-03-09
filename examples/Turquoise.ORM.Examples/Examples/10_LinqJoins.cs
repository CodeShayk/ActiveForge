using System;
using System.Collections.Generic;
using System.Linq;
using Turquoise.ORM;
using Turquoise.ORM.Linq;
using Turquoise.ORM.Examples.Domain;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 10 — LINQ Joins.
    ///
    /// Demonstrates the LINQ-layer JOIN features added in v1.2:
    ///   1. Cross-join Where predicate    — x => x.Category.Name == "Books"
    ///   2. Null check on joined column   — x => x.Category.Name == (TString)null
    ///   3. Combined predicate            — joined column AND own column
    ///   4. Cross-join OrderBy            — .OrderBy(x => x.Category.Name)
    ///   5. Multi-column sort across join — .OrderBy(...).ThenByDescending(...)
    ///   6. LeftOuterJoin&lt;T&gt; override     — query-time join type override
    ///   7. InnerJoin&lt;T&gt; override         — restore INNER on a class-level LEFT OUTER
    ///   8. Override + LINQ chain         — .LeftOuterJoin().Where().OrderBy().Take()
    ///
    /// Requires: SQL Server with SetupSql.sql applied (seed data for Categories + Products).
    /// </summary>
    public static class LinqJoinsExample
    {
        public static void Run(SqlServerConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 10 — LINQ Joins");
            Console.WriteLine("═══════════════════════════════════════\n");

            // ── Seed check ────────────────────────────────────────────────────────────

            var pCheck = new Product(conn);
            if (conn.QueryCount(pCheck, null) == 0)
            {
                Console.WriteLine("  Products table is empty — run SetupSql.sql first.");
                return;
            }

            // ── 1. Cross-join Where — filter on a joined column ───────────────────────

            Console.WriteLine("── 1. Cross-join Where: x => x.Category.Name == \"Books\" ──");

            // Use the explicit-template overload so the template has a live connection.
            var books = conn.Query(new ProductWithCategory(conn))
                .Where(x => x.Category.Name == "Books")
                .ToList();

            Console.WriteLine($"   Books ({books.Count}):");
            foreach (ProductWithCategory p in books)
                Console.WriteLine($"     {(string)p.Name.GetValue(),-30}  ${(decimal)p.Price.GetValue():F2}");

            // ── 2. Null check on a joined column ──────────────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 2. IS NULL on joined column: x.Category.Name == (TString)null ──");

            // ProductWithOptionalCategory uses [JoinSpec(LeftOuterJoin)] so rows without
            // a matching Category are included; their Category.Name will be null.
            var noCategory = conn.Query(new ProductWithOptionalCategory(conn))
                .Where(x => x.Category.Name == (TString)null)
                .ToList();

            Console.WriteLine($"   Products with no category: {noCategory.Count}");

            // ── 3. Combined predicate — joined column AND own column ──────────────────

            Console.WriteLine();
            Console.WriteLine("── 3. Combined predicate: category == \"Books\" AND price < 20 ──");

            var cheapBooks = conn.Query(new ProductWithCategory(conn))
                .Where(x => x.Category.Name == "Books" && x.Price < 20m)
                .ToList();

            Console.WriteLine($"   Cheap books ({cheapBooks.Count}):");
            foreach (ProductWithCategory p in cheapBooks)
                Console.WriteLine($"     {(string)p.Name.GetValue()}  ${(decimal)p.Price.GetValue():F2}");

            // ── 4. Cross-join OrderBy — sort on a joined column ───────────────────────

            Console.WriteLine();
            Console.WriteLine("── 4. Cross-join OrderBy: .OrderBy(x => x.Category.Name) ──");

            var byCat = conn.Query(new ProductWithCategory(conn))
                .OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Name)
                .ToList();

            Console.WriteLine($"   All products sorted by category then name ({byCat.Count}):");
            foreach (ProductWithCategory p in byCat)
            {
                string cat  = (string)p.Category.Name.GetValue();
                string name = (string)p.Name.GetValue();
                Console.WriteLine($"     {cat,-20}  {name}");
            }

            // ── 5. Multi-column sort with mixed directions ────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 5. Multi-column sort: .OrderBy(category).ThenByDescending(price) ──");

            var sorted = conn.Query(new ProductWithCategory(conn))
                .OrderBy(x => x.Category.Name)
                .ThenByDescending(x => x.Price)
                .Take(10)
                .ToList();

            Console.WriteLine($"   Top 10 (category ASC, price DESC):");
            foreach (ProductWithCategory p in sorted)
                Console.WriteLine($"     {(string)p.Category.Name.GetValue(),-20}  {(string)p.Name.GetValue(),-25}  ${(decimal)p.Price.GetValue():F2}");

            // ── 6. LeftOuterJoin<T> — query-time override ────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 6. LeftOuterJoin<Category>() — query-time override ──");
            Console.WriteLine("   ProductWithCategory normally uses INNER JOIN by convention.");
            Console.WriteLine("   .LeftOuterJoin<Category>() switches it to LEFT OUTER for this query.\n");

            var allWithOuter = conn.Query(new ProductWithCategory(conn))
                .LeftOuterJoin<Category>()
                .OrderBy(x => x.Name)
                .ToList();

            Console.WriteLine($"   All products, left-outer-joined ({allWithOuter.Count} rows):");
            foreach (ProductWithCategory p in allWithOuter)
            {
                string name = (string)p.Name.GetValue();
                string cat  = p.Category.Name.IsNull() ? "<no category>" : (string)p.Category.Name.GetValue();
                Console.WriteLine($"     {name,-30}  {cat}");
            }

            // ── 7. InnerJoin<T> — restore strict join on a LEFT OUTER class ──────────

            Console.WriteLine();
            Console.WriteLine("── 7. InnerJoin<Category>() on a [JoinSpec(LeftOuterJoin)] class ──");
            Console.WriteLine("   ProductWithOptionalCategory is defined with LEFT OUTER JOIN.");
            Console.WriteLine("   .InnerJoin<Category>() restores INNER JOIN for this query only.\n");

            var strictOnly = conn.Query(new ProductWithOptionalCategory(conn))
                .InnerJoin<Category>()
                .OrderBy(x => x.Name)
                .ToList();

            Console.WriteLine($"   Products with a matched category ({strictOnly.Count} — no orphans):");
            foreach (ProductWithOptionalCategory p in strictOnly)
                Console.WriteLine($"     {(string)p.Name.GetValue(),-30}  {(string)p.Category.Name.GetValue()}");

            // ── 8. Override + full LINQ chain ─────────────────────────────────────────

            Console.WriteLine();
            Console.WriteLine("── 8. Full chain: .LeftOuterJoin().Where().OrderBy().Skip().Take() ──");

            var page = conn.Query(new ProductWithCategory(conn))
                .LeftOuterJoin<Category>()
                .Where(x => x.InStock == true)
                .OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Price)
                .Skip(0)
                .Take(5)
                .ToList();

            Console.WriteLine($"   First 5 in-stock products (left-outer, by category then price):");
            foreach (ProductWithCategory p in page)
            {
                string name = (string)p.Name.GetValue();
                string cat  = p.Category.Name.IsNull() ? "<no category>" : (string)p.Category.Name.GetValue();
                decimal price = (decimal)p.Price.GetValue();
                Console.WriteLine($"     {cat,-20}  {name,-30}  ${price:F2}");
            }

            Console.WriteLine();
            Console.WriteLine("  Done.");
        }
    }
}
