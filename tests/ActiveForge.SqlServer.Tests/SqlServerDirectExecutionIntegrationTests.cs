using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ActiveForge;
using ActiveForge.Attributes;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    [Table("direct_products")]
    public sealed class SsDirectProduct : IdentityRecord
    {
        [Column("name")]  public TString  Name  = new TString();
        [Column("price")] public TDecimal Price = new TDecimal();

        public SsDirectProduct() { }
        public SsDirectProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class SqlServerDirectExecutionIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_direct_{System.Threading.Interlocked.Increment(ref _counter)}";

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

        public SqlServerDirectExecutionIntegrationTests()
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

        [Fact]
        public void ExecSQL_StringFormat_ReturnsMappedObjects()
        {
            var results = _conn.ExecSQL(new SsDirectProduct(_conn), "SELECT * FROM direct_products WHERE name = 'Widget'");
            
            results.Should().HaveCount(1);
            ((string)((SsDirectProduct)results[0]).Name.GetValue()).Should().Be("Widget");
            ((decimal)((SsDirectProduct)results[0]).Price.GetValue()).Should().Be(10m);
        }

        [Fact]
        public void ExecSQL_WithParameters_ReturnsMappedObjects()
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PriceTarget", 15m }
            };

            var results = _conn.ExecSQL(new SsDirectProduct(_conn), "SELECT * FROM direct_products WHERE price > @PriceTarget", parameters);
            
            results.Should().HaveCount(1);
            ((string)((SsDirectProduct)results[0]).Name.GetValue()).Should().Be("Gadget");
            ((decimal)((SsDirectProduct)results[0]).Price.GetValue()).Should().Be(20m);
        }

        [Fact]
        public void ExecStoredProcedure_ReturnsMappedObjects()
        {
            var pPrice = new Record.SPInputParameter("MinPrice", 15m);
            
            var results = _conn.ExecStoredProcedure(new SsDirectProduct(_conn), "GetExpensiveProducts", 0, 0, pPrice);
            
            results.Should().HaveCount(1);
            ((string)((SsDirectProduct)results[0]).Name.GetValue()).Should().Be("Gadget");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            _conn.ExecSQL(
                "CREATE TABLE direct_products (" +
                "  id    INT IDENTITY(1,1) PRIMARY KEY," +
                "  name  NVARCHAR(200) NOT NULL," +
                "  price DECIMAL(18,4) NOT NULL DEFAULT 0" +
                ")");

            _conn.ExecSQL(
                "CREATE PROCEDURE GetExpensiveProducts @MinPrice DECIMAL(18,4)" +
                " AS " +
                " SELECT * FROM direct_products WHERE price > @MinPrice");
        }

        private void SeedData()
        {
            InsertProduct("Widget", 10m);
            InsertProduct("Gadget", 20m);
        }

        private void InsertProduct(string name, decimal price)
        {
            var p = new SsDirectProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
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
