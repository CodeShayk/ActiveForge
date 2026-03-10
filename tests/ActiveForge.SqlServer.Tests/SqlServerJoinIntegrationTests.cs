using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    // ── Test entities ─────────────────────────────────────────────────────────────

    [Table("ss_categories")]
    public sealed class SsCat : IdentityRecord
    {
        [Column("name")] public TString Name = new TString();

        public SsCat() { }
        public SsCat(DataConnection conn) : base(conn) { }
    }

    /// <summary>Convention INNER JOIN: FK field SsCatID + embedded SsCat triggers auto join.</summary>
    [Table("ss_products")]
    public sealed class SsJoinProd : IdentityRecord
    {
        [Column("name")]     public TString     Name     = new TString();
        [Column("SsCatID")]  public TForeignKey SsCatID  = new TForeignKey();

        public SsCat SsCat;

        public SsJoinProd()                          { SsCat = new SsCat(); }
        public SsJoinProd(DataConnection conn) : base(conn) { SsCat = new SsCat(conn); }
    }

    /// <summary>[JoinSpec] LEFT OUTER JOIN on the same tables.</summary>
    [Table("ss_products")]
    [JoinSpec("SsCatID", "SsCat", "ID", JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
    public sealed class SsJoinProdOuter : IdentityRecord
    {
        [Column("name")]    public TString     Name    = new TString();
        [Column("SsCatID")] public TForeignKey SsCatID = new TForeignKey();

        public SsCat SsCat;

        public SsJoinProdOuter()                          { SsCat = new SsCat(); }
        public SsJoinProdOuter(DataConnection conn) : base(conn) { SsCat = new SsCat(conn); }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests for SQL Server JOIN support running against LocalDB.
    /// Mirrors the SQLite join test suite to verify the shared DBDataConnection
    /// join infrastructure works correctly with the SQL Server dialect.
    /// </summary>
    public sealed class SqlServerJoinIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_join_{System.Threading.Interlocked.Increment(ref _counter)}";

        private const string MasterConnStr =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;" +
            "Integrated Security=True;TrustServerCertificate=True";

        private string TestConnStr =>
            $@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog={_dbName};" +
            "Integrated Security=True;TrustServerCertificate=True;" +
            "MultipleActiveResultSets=True";

        private readonly SqlServerConnection _conn;
        private int _electronicsId;
        private int _booksId;

        public SqlServerJoinIntegrationTests()
        {
            CreateDatabase();
            _conn = new SqlServerConnection(TestConnStr);
            _conn.Connect();
            CreateSchema();
            SeedData();
        }

        public void Dispose()
        {
            _conn.Disconnect();
            DropDatabase();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group A — Convention INNER JOIN (QueryTerm API)
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void InnerJoin_QueryAll_PopulatesEmbeddedFields()
        {
            var t       = new SsJoinProd(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);

            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((SsJoinProd)r).SsCat.Name.IsNull().Should().BeFalse(),
                "every row has a matching category");
        }

        [Fact]
        public void InnerJoin_QueryAll_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var t       = new SsJoinProd(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);

            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((string)((SsJoinProd)r).Name.GetValue()).Should().NotBe("Orphan"));
        }

        [Fact]
        public void InnerJoin_QueryAll_FilterOnJoinedColumn_ReturnsMatchingRows()
        {
            var t       = new SsJoinProd(_conn);
            var term    = new EqualTerm(t.SsCat, t.SsCat.Name, "Books");
            var results = _conn.QueryAll(t, term, null, 0, null);

            results.Should().HaveCount(1);
            ((string)((SsJoinProd)results[0]).Name.GetValue()).Should().Be("SQL Mastery");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group B — [JoinSpec] LEFT OUTER JOIN (QueryTerm API)
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void LeftOuterJoin_QueryAll_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var t       = new SsJoinProdOuter(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);

            results.Should().HaveCount(4);
        }

        [Fact]
        public void LeftOuterJoin_QueryAll_NullJoinedFieldsForUnmatched()
        {
            InsertProduct("Orphan", null);

            var t       = new SsJoinProdOuter(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);
            var orphan  = results.Cast<SsJoinProdOuter>()
                                 .Single(p => (string)p.Name.GetValue() == "Orphan");

            orphan.SsCat.Name.IsNull().Should().BeTrue();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group C — Query-time join-type overrides
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void JoinOverride_InnerToLeftOuter_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(SsCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin)
            };
            var t       = new SsJoinProd(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null, overrides);

            results.Should().HaveCount(4);
        }

        [Fact]
        public void JoinOverride_LeftOuterToInner_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(SsCat), JoinSpecification.JoinTypeEnum.InnerJoin)
            };
            var t       = new SsJoinProdOuter(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null, overrides);

            results.Should().HaveCount(3);
        }

        [Fact]
        public void JoinOverride_DoesNotAffectOtherQueries()
        {
            InsertProduct("Orphan", null);

            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(SsCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin)
            };
            var t = new SsJoinProd(_conn);

            var withOverride    = _conn.QueryAll(t, null, null, 0, null, overrides);
            var withoutOverride = _conn.QueryAll(t, null, null, 0, null);

            withOverride.Should().HaveCount(4);
            withoutOverride.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group D — LINQ cross-join Where predicates
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_Where_JoinedColumn_FiltersByCategory()
        {
            var results = _conn.Query(new SsJoinProd(_conn))
                .Where(x => x.SsCat.Name == "Books")
                .ToList();

            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("SQL Mastery");
        }

        [Fact]
        public void Linq_Where_JoinedCategory_Electronics_ReturnsTwoRows()
        {
            var results = _conn.Query(new SsJoinProd(_conn))
                .Where(x => x.SsCat.Name == "Electronics")
                .ToList();

            results.Should().HaveCount(2);
        }

        [Fact]
        public void Linq_Where_JoinedColumnIsNull_WithLeftOuter()
        {
            InsertProduct("Orphan", null);

            var results = _conn.Query(new SsJoinProdOuter(_conn))
                .Where(x => x.SsCat.Name == (TString)null)
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
            var results = _conn.Query(new SsJoinProd(_conn))
                .OrderBy(x => x.SsCat.Name)
                .ThenBy(x => x.Name)
                .ToList();

            ((string)results[0].SsCat.Name.GetValue()).Should().Be("Books");
            ((string)results[^1].SsCat.Name.GetValue()).Should().Be("Electronics");
        }

        [Fact]
        public void Linq_OrderByDescending_JoinedColumn_SortsCorrectly()
        {
            var results = _conn.Query(new SsJoinProd(_conn))
                .OrderByDescending(x => x.SsCat.Name)
                .ToList();

            ((string)results[0].SsCat.Name.GetValue()).Should().Be("Electronics");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group F — LINQ join-type override
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_LeftOuterJoin_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var results = _conn.Query(new SsJoinProd(_conn))
                .LeftOuterJoin<SsCat>()
                .ToList();

            results.Should().HaveCount(4);
        }

        [Fact]
        public void Linq_InnerJoin_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);

            var results = _conn.Query(new SsJoinProdOuter(_conn))
                .InnerJoin<SsCat>()
                .ToList();

            results.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Group G — LINQ full chain
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Linq_FullChain_JoinOverride_Where_OrderBy_Pagination()
        {
            InsertProduct("Orphan", null);

            var results = _conn.Query(new SsJoinProd(_conn))
                .LeftOuterJoin<SsCat>()
                .Where(x => x.Name != (TString)null)
                .OrderBy(x => x.SsCat.Name)
                .ThenBy(x => x.Name)
                .Skip(0)
                .Take(3)
                .ToList();

            results.Should().HaveCount(3);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            _conn.ExecSQL(
                "CREATE TABLE ss_categories (" +
                "  id   INT IDENTITY(1,1) PRIMARY KEY," +
                "  name NVARCHAR(200) NOT NULL" +
                ")");

            _conn.ExecSQL(
                "CREATE TABLE ss_products (" +
                "  id       INT IDENTITY(1,1) PRIMARY KEY," +
                "  name     NVARCHAR(200) NOT NULL," +
                "  SsCatID  INT NULL" +
                ")");
        }

        private void SeedData()
        {
            _electronicsId = InsertCategory("Electronics");
            _booksId       = InsertCategory("Books");

            InsertProduct("Phone",       _electronicsId);
            InsertProduct("Laptop",      _electronicsId);
            InsertProduct("SQL Mastery", _booksId);
        }

        private int InsertCategory(string name)
        {
            var cat = new SsCat(_conn);
            cat.Name.SetValue(name);
            cat.Insert();
            return (int)cat.ID.GetValue();
        }

        private void InsertProduct(string name, int? catId)
        {
            var p = new SsJoinProd(_conn);
            p.Name.SetValue(name);
            if (catId.HasValue)
                p.SsCatID.SetValue(catId.Value);
            p.Insert();
        }

        private void CreateDatabase()
        {
            using var conn = new SqlConnection(MasterConnStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"IF DB_ID('{_dbName}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_dbName}] END; " +
                $"CREATE DATABASE [{_dbName}]";
            cmd.ExecuteNonQuery();
        }

        private void DropDatabase()
        {
            using var conn = new SqlConnection(MasterConnStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_dbName}]";
            cmd.ExecuteNonQuery();
        }
    }
}
