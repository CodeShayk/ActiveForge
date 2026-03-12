using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    // Reuse Test entity from SqlServerConnectionCrudTests
    // To ensure fresh DB, we will define our own schema matching the entity
    
    [Trait("Category", "Integration")]
    public sealed class SqlServerLinqIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_linq_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string MasterConnStr =>
            Environment.GetEnvironmentVariable("SS_ADMIN_CONNSTR")
            ?? @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True";

        private string TestConnStr =>
            new SqlConnectionStringBuilder(MasterConnStr)
            {
                InitialCatalog          = _dbName,
                MultipleActiveResultSets = true,
            }.ConnectionString;

        private readonly SqlServerConnection _conn;

        public SqlServerLinqIntegrationTests()
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

        // ── Scalar Execution ──────────────────────────────────────────────────────
        
        [Fact]
        public void LinqCount_ReturnsCorrectCount()
        {
            var count = _conn.Query(new SsProduct(_conn)).Where(p => p.InStock).Count();
            count.Should().Be(2); 
        }

        [Fact]
        public void LinqAny_ReturnsTrueWhenRecordsExist()
        {
            var exists = _conn.Query(new SsProduct(_conn)).Where(p => p.Name == "Widget").Any();
            exists.Should().BeTrue();
        }

        [Fact]
        public void LinqFirst_ReturnsFirstRecord()
        {
            var first = _conn.Query(new SsProduct(_conn)).OrderBy(p => p.Name).First();
            ((string)first.Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqSingle_ReturnsSingleRecord()
        {
            var single = _conn.Query(new SsProduct(_conn)).Where(p => p.Name == "Widget").Single();
            ((string)single.Name.GetValue()).Should().Be("Widget");
        }

        // ── Predicate Translations ────────────────────────────────────────────────
        
        [Fact]
        public void LinqStartsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new SsProduct(_conn)).Where(p => ((string)p.Name).StartsWith("Gad")).ToList();
            result.Should().HaveCount(1);
            ((string)result[0].Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqEndsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new SsProduct(_conn)).Where(p => ((string)p.Name).EndsWith("get")).ToList();
            result.Should().HaveCount(2); // Gadget, Widget
        }

        [Fact]
        public void LinqImplicitBool_ReturnsMatchingRecords()
        {
            var result = _conn.Query(new SsProduct(_conn)).Where(p => p.InStock).ToList();
            result.Should().HaveCount(2);
        }

        // ── Projections (Select) ──────────────────────────────────────────────────
        
        [Fact]
        public void LinqSelect_ProjectToAnonymousType_ReturnsCorrectData()
        {
            var result = _conn.Query(new SsProduct(_conn))
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
            _conn.ExecSQL(
                "CREATE TABLE products (" +
                "  id       INT IDENTITY(1,1) PRIMARY KEY," +
                "  name     NVARCHAR(200) NOT NULL," +
                "  price    DECIMAL(18,4) NOT NULL DEFAULT 0," +
                "  in_stock BIT NOT NULL DEFAULT 1" +
                ")");
        }

        private void SeedData()
        {
            InsertProduct("Widget", 19.99m, true);
            InsertProduct("Gadget", 29.99m, false);
            InsertProduct("Zebra", 99.99m, true);
        }

        private void InsertProduct(string name, decimal price, bool inStock)
        {
            var p = new SsProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.InStock.SetValue(inStock);
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
