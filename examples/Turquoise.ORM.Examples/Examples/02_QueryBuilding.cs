using System;
using System.Collections.Generic;
using Turquoise.ORM;
using Turquoise.ORM.Examples.Domain;
using Turquoise.ORM.Query;

namespace Turquoise.ORM.Examples.Examples
{
    /// <summary>
    /// Example 02 — Query building.
    ///
    /// Turquoise.ORM uses a composable predicate tree for WHERE clauses.
    /// Each QueryTerm is an object that knows how to emit SQL and bind parameters.
    /// Terms compose with &amp; (AND), | (OR), ! (NOT).
    ///
    /// Key query term types:
    ///   EqualTerm          — field = @value
    ///   GreaterThanTerm    — field > @value
    ///   GreaterOrEqualTerm — field >= @value
    ///   LessThanTerm       — field &lt; @value
    ///   LessOrEqualTerm    — field &lt;= @value
    ///   IsNullTerm         — field IS NULL
    ///   LikeTerm           — field LIKE @pattern  (caller adds %)
    ///   ContainsTerm       — field LIKE '%value%'  (% added automatically)
    ///   InTerm             — field IN (@p1, @p2, ...)
    ///   RawSqlTerm         — verbatim SQL predicate (no parameter binding)
    /// </summary>
    public static class QueryBuilding
    {
        public static void Run(DataConnection conn)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("  Example 02 — Query Building");
            Console.WriteLine("═══════════════════════════════════════\n");

            // A Product instance with connection acts as the "template" for the query.
            // Its TField members are used to identify which column each term refers to.
            var template = new Product(conn);

            // ── Simple equality ───────────────────────────────────────────────────────

            Console.WriteLine("— EqualTerm: inStock = true");
            var inStockTerm = new EqualTerm(template, template.InStock, true);
            var inStock = conn.QueryAll(template, inStockTerm, null, 0, null);
            Console.WriteLine($"  Found {inStock.Count} in-stock product(s)");

            // ── Numeric comparison ────────────────────────────────────────────────────

            Console.WriteLine("— GreaterThanTerm: price > 10");
            var priceTerm = new GreaterThanTerm(template, template.Price, 10m);
            var expensive = conn.QueryAll(template, priceTerm, null, 0, null);
            Console.WriteLine($"  Found {expensive.Count} product(s) with price > 10");

            Console.WriteLine("— LessOrEqualTerm: price <= 5");
            var cheapTerm = new LessOrEqualTerm(template, template.Price, 5m);
            var cheap = conn.QueryAll(template, cheapTerm, null, 0, null);
            Console.WriteLine($"  Found {cheap.Count} cheap product(s)");

            // ── AND composition ───────────────────────────────────────────────────────

            // Compose terms with & to create an AND predicate.
            // Both sides must evaluate to true for a row to be included.
            Console.WriteLine("— AND: inStock AND price > 10");
            var andTerm = inStockTerm & priceTerm;
            var results = conn.QueryAll(template, andTerm, null, 0, null);
            Console.WriteLine($"  Found {results.Count} in-stock product(s) with price > 10");

            // ── OR composition ────────────────────────────────────────────────────────

            Console.WriteLine("— OR: price <= 5 OR price > 50");
            var veryExpensive = new GreaterThanTerm(template, template.Price, 50m);
            var orTerm = cheapTerm | veryExpensive;
            var extremes = conn.QueryAll(template, orTerm, null, 0, null);
            Console.WriteLine($"  Found {extremes.Count} extreme-priced product(s)");

            // ── NOT ───────────────────────────────────────────────────────────────────

            Console.WriteLine("— NOT: !inStock");
            var notInStock = !inStockTerm;
            var outOfStock = conn.QueryAll(template, notInStock, null, 0, null);
            Console.WriteLine($"  Found {outOfStock.Count} out-of-stock product(s)");

            // ── ContainsTerm (LIKE '%x%') ─────────────────────────────────────────────

            // ContainsTerm wraps the value in % automatically.
            // Use when you want case-insensitive substring matching.
            Console.WriteLine("— ContainsTerm: name contains 'widget'");
            var containsTerm = new ContainsTerm(template, template.Name, "widget");
            var widgets = conn.QueryAll(template, containsTerm, null, 0, null);
            Console.WriteLine($"  Found {widgets.Count} widget(s)");

            // ── LikeTerm (caller-controlled wildcards) ────────────────────────────────

            Console.WriteLine("— LikeTerm: name LIKE 'Widget%'");
            var likeTerm = new LikeTerm(template, template.Name, "Widget%");
            var likeResults = conn.QueryAll(template, likeTerm, null, 0, null);
            Console.WriteLine($"  Found {likeResults.Count} product(s) starting with 'Widget'");

            // ── IsNullTerm ────────────────────────────────────────────────────────────

            Console.WriteLine("— IsNullTerm: description IS NULL");
            var isNullTerm = new IsNullTerm(template, template.Description);
            var noDesc = conn.QueryAll(template, isNullTerm, null, 0, null);
            Console.WriteLine($"  Found {noDesc.Count} product(s) with no description");

            // ── InTerm ────────────────────────────────────────────────────────────────

            // InTerm generates: field IN (@p1, @p2, @p3, ...)
            Console.WriteLine("— InTerm: categoryID IN (1, 2, 3)");
            var categories = new List<object> { 1, 2, 3 };
            var inTerm = new InTerm(template, template.CategoryID, categories);
            var byCat = conn.QueryAll(template, inTerm, null, 0, null);
            Console.WriteLine($"  Found {byCat.Count} product(s) in categories 1-3");

            // ── Sorting ───────────────────────────────────────────────────────────────

            Console.WriteLine("— OrderAscending by name");
            var sortAsc  = new OrderAscending(template, template.Name);
            var sortedAsc = conn.QueryAll(template, null, sortAsc, 0, null);
            if (sortedAsc.Count > 0)
                Console.WriteLine($"  First: {((Product)sortedAsc[0]).Name}");

            Console.WriteLine("— OrderDescending by price");
            var sortDesc = new OrderDescending(template, template.Price);
            var sortedDesc = conn.QueryAll(template, null, sortDesc, 0, null);
            if (sortedDesc.Count > 0)
                Console.WriteLine($"  Most expensive: {((Product)sortedDesc[0]).Name} @ {((Product)sortedDesc[0]).Price}");

            // ── QueryCount ────────────────────────────────────────────────────────────

            // QueryCount emits SELECT COUNT(*) — no object hydration overhead.
            int total = conn.QueryCount(template, null);
            Console.WriteLine($"— Total products in table: {total}");

            int cheapCount = conn.QueryCount(template, cheapTerm);
            Console.WriteLine($"— Products with price <= 5: {cheapCount}");

            // ── Pagination ────────────────────────────────────────────────────────────

            // QueryPage(obj, term, sort, start, count, fieldSubset)
            // start = first record (0-based), count = page size
            Console.WriteLine("— QueryPage: page 1 (records 0-9)");
            var page1 = conn.QueryPage(template, null, sortAsc, 0, 10, null);
            Console.WriteLine($"  Page 1: {page1.Count} record(s), IsMoreData={page1.IsMoreData}");

            if (page1.IsMoreData)
            {
                var page2 = conn.QueryPage(template, null, sortAsc, 10, 10, null);
                Console.WriteLine($"  Page 2: {page2.Count} record(s)");
            }

            // ── Complex compound query ─────────────────────────────────────────────────

            Console.WriteLine("— Complex: (inStock AND price > 10) OR (name contains 'special')");
            var special = new ContainsTerm(template, template.Name, "special");
            var complex = (inStockTerm & priceTerm) | special;
            var complexResults = conn.QueryAll(template, complex, null, 0, null);
            Console.WriteLine($"  Found {complexResults.Count} matching product(s)");
        }
    }
}
