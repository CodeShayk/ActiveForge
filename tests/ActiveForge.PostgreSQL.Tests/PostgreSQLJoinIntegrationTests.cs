using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    // ── Test entities ─────────────────────────────────────────────────────────────

    [Table("pg_categories")]
    public sealed class PgCat : IdentDataObject
    {
        [Column("name")] public TString Name = new TString();
        public PgCat() { }
        public PgCat(DataConnection conn) : base(conn) { }
    }

    [Table("pg_products")]
    public sealed class PgJoinProd : IdentDataObject
    {
        [Column("name")]    public TString     Name    = new TString();
        [Column("PgCatID")] public TForeignKey PgCatID = new TForeignKey();

        public PgCat PgCat;
        public PgJoinProd()                          { PgCat = new PgCat(); }
        public PgJoinProd(DataConnection conn) : base(conn) { PgCat = new PgCat(conn); }
    }

    [Table("pg_products")]
    [JoinSpec("PgCatID", "PgCat", "ID", JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin)]
    public sealed class PgJoinProdOuter : IdentDataObject
    {
        [Column("name")]    public TString     Name    = new TString();
        [Column("PgCatID")] public TForeignKey PgCatID = new TForeignKey();

        public PgCat PgCat;
        public PgJoinProdOuter()                          { PgCat = new PgCat(); }
        public PgJoinProdOuter(DataConnection conn) : base(conn) { PgCat = new PgCat(conn); }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests for PostgreSQL JOIN support running against a Docker container.
    /// Mirrors the SQL Server and SQLite join test suites to verify the shared
    /// DBDataConnection join infrastructure works with the PostgreSQL dialect.
    /// </summary>
    public sealed class PostgreSQLJoinIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_join_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string AdminConnStr =>
            Environment.GetEnvironmentVariable("PG_ADMIN_CONNSTR")
            ?? "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=Pa55w0rd";

        private string TestConnStr =>
            $"Host=localhost;Port=5455;Database={_dbName};Username=postgres;Password=Pa55w0rd";

        private readonly PostgreSQLConnection _conn;
        private int _electronicsId;
        private int _booksId;

        public PostgreSQLJoinIntegrationTests()
        {
            CreateDatabase();
            _conn = new PostgreSQLConnection(TestConnStr);
            _conn.Connect();
            CreateSchema();
            SeedData();
        }

        public void Dispose()
        {
            _conn.Disconnect();
            DropDatabase();
        }

        // ── Group A — Convention INNER JOIN ───────────────────────────────────────

        [Fact]
        public void InnerJoin_QueryAll_PopulatesEmbeddedFields()
        {
            var t       = new PgJoinProd(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);
            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((PgJoinProd)r).PgCat.Name.IsNull().Should().BeFalse());
        }

        [Fact]
        public void InnerJoin_QueryAll_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var results = _conn.QueryAll(new PgJoinProd(_conn), null, null, 0, null);
            results.Should().HaveCount(3);
        }

        [Fact]
        public void InnerJoin_FilterOnJoinedColumn_ReturnsMatchingRows()
        {
            var t       = new PgJoinProd(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t.PgCat, t.PgCat.Name, "Books"), null, 0, null);
            results.Should().HaveCount(1);
            ((string)((PgJoinProd)results[0]).Name.GetValue()).Should().Be("SQL Mastery");
        }

        // ── Group B — LEFT OUTER JOIN ─────────────────────────────────────────────

        [Fact]
        public void LeftOuterJoin_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var results = _conn.QueryAll(new PgJoinProdOuter(_conn), null, null, 0, null);
            results.Should().HaveCount(4);
        }

        [Fact]
        public void LeftOuterJoin_NullJoinedFieldsForUnmatched()
        {
            InsertProduct("Orphan", null);
            var results = _conn.QueryAll(new PgJoinProdOuter(_conn), null, null, 0, null);
            var orphan  = results.Cast<PgJoinProdOuter>()
                                 .Single(p => (string)p.Name.GetValue() == "Orphan");
            orphan.PgCat.Name.IsNull().Should().BeTrue();
        }

        // ── Group C — Join-type overrides ─────────────────────────────────────────

        [Fact]
        public void JoinOverride_InnerToLeftOuter_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var overrides = new List<JoinOverride>
                { new JoinOverride(typeof(PgCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin) };
            var results = _conn.QueryAll(new PgJoinProd(_conn), null, null, 0, null, overrides);
            results.Should().HaveCount(4);
        }

        [Fact]
        public void JoinOverride_LeftOuterToInner_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var overrides = new List<JoinOverride>
                { new JoinOverride(typeof(PgCat), JoinSpecification.JoinTypeEnum.InnerJoin) };
            var results = _conn.QueryAll(new PgJoinProdOuter(_conn), null, null, 0, null, overrides);
            results.Should().HaveCount(3);
        }

        [Fact]
        public void JoinOverride_DoesNotAffectOtherQueries()
        {
            InsertProduct("Orphan", null);
            var overrides = new List<JoinOverride>
                { new JoinOverride(typeof(PgCat), JoinSpecification.JoinTypeEnum.LeftOuterJoin) };
            var t = new PgJoinProd(_conn);
            _conn.QueryAll(t, null, null, 0, null, overrides).Should().HaveCount(4);
            _conn.QueryAll(t, null, null, 0, null).Should().HaveCount(3);
        }

        // ── Group D — LINQ cross-join Where ───────────────────────────────────────

        [Fact]
        public void Linq_Where_JoinedColumn_FiltersByCategory()
        {
            var results = _conn.Query(new PgJoinProd(_conn))
                .Where(x => x.PgCat.Name == "Books")
                .ToList();
            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("SQL Mastery");
        }

        [Fact]
        public void Linq_Where_JoinedColumnIsNull_WithLeftOuter()
        {
            InsertProduct("Orphan", null);
            var results = _conn.Query(new PgJoinProdOuter(_conn))
                .Where(x => x.PgCat.Name == (TString)null)
                .ToList();
            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("Orphan");
        }

        // ── Group E — LINQ cross-join OrderBy ─────────────────────────────────────

        [Fact]
        public void Linq_OrderBy_JoinedColumn_SortsCorrectly()
        {
            var results = _conn.Query(new PgJoinProd(_conn))
                .OrderBy(x => x.PgCat.Name)
                .ThenBy(x => x.Name)
                .ToList();
            ((string)results[0].PgCat.Name.GetValue()).Should().Be("Books");
            ((string)results[^1].PgCat.Name.GetValue()).Should().Be("Electronics");
        }

        [Fact]
        public void Linq_OrderByDescending_JoinedColumn_SortsCorrectly()
        {
            var results = _conn.Query(new PgJoinProd(_conn))
                .OrderByDescending(x => x.PgCat.Name)
                .ToList();
            ((string)results[0].PgCat.Name.GetValue()).Should().Be("Electronics");
        }

        // ── Group F — LINQ join-type override ─────────────────────────────────────

        [Fact]
        public void Linq_LeftOuterJoin_IncludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var results = _conn.Query(new PgJoinProd(_conn))
                .LeftOuterJoin<PgCat>()
                .ToList();
            results.Should().HaveCount(4);
        }

        [Fact]
        public void Linq_InnerJoin_ExcludesUnmatchedProduct()
        {
            InsertProduct("Orphan", null);
            var results = _conn.Query(new PgJoinProdOuter(_conn))
                .InnerJoin<PgCat>()
                .ToList();
            results.Should().HaveCount(3);
        }

        // ── Group G — LINQ full chain ─────────────────────────────────────────────

        [Fact]
        public void Linq_FullChain_JoinOverride_Where_OrderBy_Pagination()
        {
            InsertProduct("Orphan", null);
            var results = _conn.Query(new PgJoinProd(_conn))
                .LeftOuterJoin<PgCat>()
                .Where(x => x.Name != (TString)null)
                .OrderBy(x => x.PgCat.Name)
                .ThenBy(x => x.Name)
                .Skip(0)
                .Take(3)
                .ToList();
            results.Should().HaveCount(3);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            using (_conn.ExecSQL(
                "CREATE TABLE pg_categories (" +
                "  \"ID\"  SERIAL PRIMARY KEY," +
                "  name VARCHAR(200) NOT NULL" +
                ")")) { }
            using (_conn.ExecSQL(
                "CREATE TABLE pg_products (" +
                "  \"ID\"     SERIAL PRIMARY KEY," +
                "  name     VARCHAR(200) NOT NULL," +
                "  \"PgCatID\" INTEGER" +
                ")")) { }
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
            var cat = new PgCat(_conn);
            cat.Name.SetValue(name);
            cat.Insert();
            return (int)cat.ID.GetValue();
        }

        private void InsertProduct(string name, int? catId)
        {
            var p = new PgJoinProd(_conn);
            p.Name.SetValue(name);
            if (catId.HasValue) p.PgCatID.SetValue(catId.Value);
            p.Insert();
        }

        private void CreateDatabase()
        {
            using var conn = new Npgsql.NpgsqlConnection(AdminConnStr);
            conn.Open();
            // Drop any leftover database from a previous run
            using (var killCmd = conn.CreateCommand())
            {
                killCmd.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{_dbName}'";
                killCmd.ExecuteNonQuery();
            }
            using (var dropCmd = conn.CreateCommand())
            {
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_dbName}";
                dropCmd.ExecuteNonQuery();
            }
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE {_dbName}";
            createCmd.ExecuteNonQuery();
        }

        private void DropDatabase()
        {
            using var conn = new Npgsql.NpgsqlConnection(AdminConnStr);
            conn.Open();
            using (var killCmd = conn.CreateCommand())
            {
                killCmd.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{_dbName}'";
                killCmd.ExecuteNonQuery();
            }
            using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_dbName}";
            dropCmd.ExecuteNonQuery();
        }
    }
}
