using System;
using System.Linq;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    // ── Test entity ───────────────────────────────────────────────────────────────

    [Table("products")]
    public sealed class PgLinqProduct : IdentityRecord
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public PgLinqProduct() { }
        public PgLinqProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class PostgreSQLLinqIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_linq_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string AdminConnStr =>
            Environment.GetEnvironmentVariable("PG_ADMIN_CONNSTR")
            ?? "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=Pa55w0rd";

        private string TestConnStr =>
            $"Host=localhost;Port=5455;Database={_dbName};Username=postgres;Password=Pa55w0rd";

        private readonly PostgreSQLConnection _conn;

        public PostgreSQLLinqIntegrationTests()
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

        // ── Scalar Execution ──────────────────────────────────────────────────────
        
        [Fact]
        public void LinqCount_ReturnsCorrectCount()
        {
            var count = _conn.Query(new PgLinqProduct(_conn)).Where(p => p.InStock).Count();
            count.Should().Be(2); 
        }

        [Fact]
        public void LinqAny_ReturnsTrueWhenRecordsExist()
        {
            var exists = _conn.Query(new PgLinqProduct(_conn)).Where(p => p.Name == "Widget").Any();
            exists.Should().BeTrue();
        }

        [Fact]
        public void LinqFirst_ReturnsFirstRecord()
        {
            var first = _conn.Query(new PgLinqProduct(_conn)).OrderBy(p => p.Name).First();
            ((string)first.Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqSingle_ReturnsSingleRecord()
        {
            var single = _conn.Query(new PgLinqProduct(_conn)).Where(p => p.Name == "Widget").Single();
            ((string)single.Name.GetValue()).Should().Be("Widget");
        }

        // ── Predicate Translations ────────────────────────────────────────────────
        
        [Fact]
        public void LinqStartsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new PgLinqProduct(_conn)).Where(p => ((string)p.Name).StartsWith("Gad")).ToList();
            result.Should().HaveCount(1);
            ((string)result[0].Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqEndsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new PgLinqProduct(_conn)).Where(p => ((string)p.Name).EndsWith("get")).ToList();
            result.Should().HaveCount(2); // Gadget, Widget
        }

        [Fact]
        public void LinqImplicitBool_ReturnsMatchingRecords()
        {
            var result = _conn.Query(new PgLinqProduct(_conn)).Where(p => p.InStock).ToList();
            result.Should().HaveCount(2);
        }

        // ── Projections (Select) ──────────────────────────────────────────────────
        
        [Fact]
        public void LinqSelect_ProjectToAnonymousType_ReturnsCorrectData()
        {
            var result = _conn.Query(new PgLinqProduct(_conn))
                              .Where(p => p.InStock)
                              .OrderBy(p => p.Name)
                              .Select(p => new { Name = (string)p.Name, Price = (decimal)p.Price })
                              .ToList();

            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Widget");
            result[0].Price.Should().Be(19.99m);
            result[1].Name.Should().Be("Zebra");
            result[1].Price.Should().Be(99.99m);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            using (_conn.ExecSQL(
                "CREATE TABLE products (" +
                "  \"ID\"     SERIAL PRIMARY KEY," +
                "  name     VARCHAR(200) NOT NULL," +
                "  price    DECIMAL(18,4) NOT NULL DEFAULT 0," +
                "  in_stock BOOLEAN NOT NULL DEFAULT TRUE" +
                ")")) { }
        }

        private void SeedData()
        {
            InsertProduct("Widget", 19.99m, true);
            InsertProduct("Gadget", 29.99m, false);
            InsertProduct("Zebra", 99.99m, true);
        }

        private void InsertProduct(string name, decimal price, bool inStock)
        {
            var p = new PgLinqProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.InStock.SetValue(inStock);
            p.Insert();
        }

        private void CreateDatabase()
        {
            using var conn = new Npgsql.NpgsqlConnection(AdminConnStr);
            conn.Open();
            // Terminate existing connections and drop leftover DB from previous runs
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
