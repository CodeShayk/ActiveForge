using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.SQLite.Tests
{
    // ── Test entities ─────────────────────────────────────────────────────────────
    //
    // TestCat  — the "category" side of the join.
    // JoinProd — uses the naming convention (TForeignKey TestCatID + embedded TestCat)
    //            to produce an auto INNER JOIN.
    // JoinProdOuter — same tables but with [JoinSpec(LeftOuterJoin)] for a LEFT OUTER JOIN.
    //
    // The embedded field constructors are initialised explicitly in the DataConnection
    // overload so the embedded objects carry a live connection — required by
    // QueryTerm.Initialize and SortOrder.GetSQL.

    [Table("join_categories")]
    public sealed class TestCat : IdentityRecord
    {
        [Column("name")] public TString Name = new TString();

        public TestCat() { }
        public TestCat(DataConnection conn) : base(conn) { }
    }

    /// <summary>
    /// Convention INNER JOIN: TForeignKey <c>TestCatID</c> + embedded <c>TestCat</c>
    /// field whose type ends in <c>TestCat</c> triggers:
    ///   INNER JOIN join_categories ON join_products.TestCatID = join_categories.id
    /// </summary>
    [Table("join_products")]
    public sealed class JoinProd : IdentityRecord
    {
        [Column("name")]      public TString     Name      = new TString();
        [Column("TestCatID")] public TForeignKey TestCatID = new TForeignKey();

        // Initialised in constructors (not as a field initialiser) so that the
        // conn-based constructor propagates the connection to the embedded object.
        public TestCat TestCat;

        public JoinProd()                        { TestCat = new TestCat(); }
        public JoinProd(DataConnection conn) : base(conn) { TestCat = new TestCat(conn); }
    }

    /// <summary>
    /// [JoinSpec] LEFT OUTER JOIN on the same tables.
    /// Products with no matching category are included; their TestCat fields are null.
    /// </summary>
    [Table("join_products")]
    [JoinSpec("TestCatID", "TestCat", "ID", JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
    public sealed class JoinProdOuter : IdentityRecord
    {
        [Column("name")]      public TString     Name      = new TString();
        [Column("TestCatID")] public TForeignKey TestCatID = new TForeignKey();

        public TestCat TestCat;

        public JoinProdOuter()                        { TestCat = new TestCat(); }
        public JoinProdOuter(DataConnection conn) : base(conn) { TestCat = new TestCat(conn); }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests for JOIN support using an in-memory SQLite database.
    ///
    /// Each test class instance gets a unique named shared-cache database so that
    /// the connection can be reopened within RunWrite without losing state.
    ///
    /// Test groups:
    ///   A — Convention INNER JOIN via QueryTerm API
    ///   B — [JoinSpec] LEFT OUTER JOIN via QueryTerm API
    ///   C — Query-time join-type overrides via DataConnection API
    ///   D — LINQ cross-join Where predicates
    ///   E — LINQ cross-join OrderBy selectors
    ///   F — LINQ join-type override (.InnerJoin / .LeftOuterJoin)
    ///   G — LINQ full chain (join override + Where + OrderBy + pagination)
    /// </summary>
    public sealed class SQLiteJoinIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"jointest_{System.Threading.Interlocked.Increment(ref _counter)}";

        private readonly SQLiteConnection _conn;

        private string ConnStr =>
            $"Data Source={_dbName};Mode=Memory;Cache=Shared";

        // ── IDs for the two seed categories ──────────────────────────────────────

        private int _electronicsId;
        private int _booksId;

        public SQLiteJoinIntegrationTests()
        {
            _conn = new SQLiteConnection(ConnStr);
            _conn.Connect();
            CreateSchema();
            SeedData();
        }

        public void Dispose() => _conn.Disconnect();

        // ─────────────────────────────────────────────────────────────────────────
        // Group A — Convention INNER JOIN (QueryTerm API)
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InnerJoin_QueryAll_PopulatesEmbeddedFields()
        {
            // All three seed products reference a real category, so all three are
            // returned and each one's TestCat.Name is populated from the joined row.
            var template = new JoinProd(_conn);
            var results  = _conn.QueryAll(template, null, null, 0, null);

            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((JoinProd)r).TestCat.Name.IsNull().Should().BeFalse(),
                "every row has a matching category");
        }

        [Fact]
        public void InnerJoin_QueryAll_ExcludesUnmatchedProduct()
        {
            // Insert a product with a non-existent FK — INNER JOIN must exclude it.
            InsertProduct("Orphan", null, _conn);

            var template = new JoinProd(_conn);
            var results  = _conn.QueryAll(template, null, null, 0, null);

            // Three matched + one orphan inserted, but INNER JOIN returns only 3.
            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((string)((JoinProd)r).Name.GetValue()).Should().NotBe("Orphan"));
        }

        [Fact]
        public void InnerJoin_QueryAll_FilterOnJoinedColumn_ReturnsMatchingRows()
        {
            var template  = new JoinProd(_conn);
            var term      = new EqualTerm(template.TestCat, template.TestCat.Name, "Books");
            var results   = _conn.QueryAll(template, term, null, 0, null);

            results.Should().HaveCount(1);
            ((string)((JoinProd)results[0]).Name.GetValue()).Should().Be("SQL Mastery");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group B — [JoinSpec] LEFT OUTER JOIN (QueryTerm API)
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void LeftOuterJoin_QueryAll_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null, _conn);

            var template = new JoinProdOuter(_conn);
            var results  = _conn.QueryAll(template, null, null, 0, null);

            // Three matched + one orphan = 4 rows.
            results.Should().HaveCount(4);
        }

        [Fact]
        public void LeftOuterJoin_QueryAll_NullJoinedFieldsForUnmatched()
        {
            InsertProduct("Orphan", null, _conn);

            var template = new JoinProdOuter(_conn);
            var results  = _conn.QueryAll(template, null, null, 0, null);

            var orphan = results.Cast<JoinProdOuter>()
                .Single(p => (string)p.Name.GetValue() == "Orphan");

            orphan.TestCat.Name.IsNull().Should().BeTrue("unmatched LEFT OUTER row has null joined fields");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group C — Query-time join-type overrides via DataConnection API
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void JoinOverride_InnerToLeftOuter_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null, _conn);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(TestCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin)
            };

            var template = new JoinProd(_conn);
            // JoinProd normally uses INNER JOIN by convention; override to LEFT OUTER.
            var results  = _conn.QueryAll(template, null, null, 0, null, overrides);

            results.Should().HaveCount(4, "LEFT OUTER includes the unmatched orphan");
        }

        [Fact]
        public void JoinOverride_LeftOuterToInner_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null, _conn);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(TestCat), JoinSpecification.JoinTypeEnum.InnerJoin)
            };

            var template = new JoinProdOuter(_conn);
            // JoinProdOuter normally uses LEFT OUTER JOIN; override to INNER.
            var results  = _conn.QueryAll(template, null, null, 0, null, overrides);

            results.Should().HaveCount(3, "INNER JOIN excludes the unmatched orphan");
        }

        [Fact]
        public void JoinOverride_DoesNotAffectOtherQueries()
        {
            // Calling QueryAll with overrides must not mutate cached state and affect
            // a subsequent call without overrides.
            InsertProduct("Orphan", null, _conn);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(TestCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin)
            };
            var template = new JoinProd(_conn);

            // First query with override (LEFT OUTER → 4 rows).
            var withOverride = _conn.QueryAll(template, null, null, 0, null, overrides);
            // Second query without override (INNER → 3 rows).
            var withoutOverride = _conn.QueryAll(template, null, null, 0, null);

            withOverride.Should().HaveCount(4);
            withoutOverride.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group D — LINQ cross-join Where predicates
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_Where_JoinedColumn_FiltersByCategory()
        {
            var results = _conn.Query(new JoinProd(_conn))
                .Where(x => x.TestCat.Name == "Books")
                .ToList();

            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("SQL Mastery");
        }

        [Fact]
        public void Linq_Where_CombinedOwnAndJoinedColumn()
        {
            // Own column: Name starts with filter; joined column: category == "Electronics"
            var results = _conn.Query(new JoinProd(_conn))
                .Where(x => x.TestCat.Name == "Electronics")
                .ToList();

            results.Should().HaveCount(2);
            results.Should().AllSatisfy(r =>
                ((string)((JoinProd)r).TestCat.Name.GetValue()).Should().Be("Electronics"));
        }

        [Fact]
        public void Linq_Where_JoinedColumnIsNull_WithLeftOuter()
        {
            InsertProduct("Orphan", null, _conn);

            // JoinProdOuter uses LEFT OUTER so orphan rows are included.
            // Filtering x.TestCat.Name == null (IS NULL) should return only the orphan.
            var results = _conn.Query(new JoinProdOuter(_conn))
                .Where(x => x.TestCat.Name == (TString)null)
                .ToList();

            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("Orphan");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group E — LINQ cross-join OrderBy selectors
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_OrderBy_JoinedColumn_SortsCorrectly()
        {
            var results = _conn.Query(new JoinProd(_conn))
                .OrderBy(x => x.TestCat.Name)
                .ThenBy(x => x.Name)
                .ToList();

            // "Books" < "Electronics" alphabetically
            ((string)results[0].TestCat.Name.GetValue()).Should().Be("Books");
            ((string)results[^1].TestCat.Name.GetValue()).Should().Be("Electronics");
        }

        [Fact]
        public void Linq_OrderByDescending_JoinedColumn_SortsCorrectly()
        {
            var results = _conn.Query(new JoinProd(_conn))
                .OrderByDescending(x => x.TestCat.Name)
                .ToList();

            // "Electronics" > "Books" — should appear first in descending order
            ((string)results[0].TestCat.Name.GetValue()).Should().Be("Electronics");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group F — LINQ join-type override
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_LeftOuterJoin_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null, _conn);

            var results = _conn.Query(new JoinProd(_conn))
                .LeftOuterJoin<TestCat>()
                .ToList();

            results.Should().HaveCount(4, "LEFT OUTER JOIN includes orphan");
        }

        [Fact]
        public void Linq_InnerJoin_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null, _conn);

            // JoinProdOuter normally uses LEFT OUTER; .InnerJoin() overrides it.
            var results = _conn.Query(new JoinProdOuter(_conn))
                .InnerJoin<TestCat>()
                .ToList();

            results.Should().HaveCount(3, "INNER JOIN excludes orphan");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group G — LINQ full chain
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_FullChain_JoinOverride_Where_OrderBy_Pagination()
        {
            InsertProduct("Orphan", null, _conn);

            var results = _conn.Query(new JoinProd(_conn))
                .LeftOuterJoin<TestCat>()
                .Where(x => x.Name != (TString)null)   // all rows (non-null name)
                .OrderBy(x => x.TestCat.Name)
                .ThenBy(x => x.Name)
                .Skip(0)
                .Take(3)
                .ToList();

            // 4 total (3 matched + 1 orphan), Take(3) returns first 3 in category-name order.
            results.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Schema and seed helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            _conn.ExecSQL(
                "CREATE TABLE IF NOT EXISTS join_categories (" +
                "  id   INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  name TEXT NOT NULL" +
                ")");

            _conn.ExecSQL(
                "CREATE TABLE IF NOT EXISTS join_products (" +
                "  id        INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  name      TEXT NOT NULL," +
                "  TestCatID INTEGER" +
                ")");
        }

        private void SeedData()
        {
            _electronicsId = InsertCategory("Electronics");
            _booksId       = InsertCategory("Books");

            // Two electronics products, one books product — all have a valid FK.
            InsertProduct("Phone",       _electronicsId, _conn);
            InsertProduct("Laptop",      _electronicsId, _conn);
            InsertProduct("SQL Mastery", _booksId,       _conn);
        }

        private int InsertCategory(string name)
        {
            var cat = new TestCat(_conn);
            cat.Name.SetValue(name);
            cat.Insert();
            return (int)cat.ID.GetValue();
        }

        private void InsertProduct(string name, int? catId, DataConnection conn)
        {
            var p = new JoinProd(conn as SQLiteConnection ?? _conn);
            p.Name.SetValue(name);
            if (catId.HasValue)
                p.TestCatID.SetValue(catId.Value);
            p.Insert();
        }
    }
}
