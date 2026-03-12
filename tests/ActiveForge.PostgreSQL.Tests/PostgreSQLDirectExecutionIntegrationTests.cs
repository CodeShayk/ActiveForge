using System;
using System.Collections.Generic;
using FluentAssertions;
using Npgsql;
using ActiveForge;
using ActiveForge.Attributes;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    [Table("direct_products")]
    public sealed class PgDirectProduct : IdentityRecord
    {
        [Column("name")]  public TString  Name  = new TString();
        [Column("price")] public TDecimal Price = new TDecimal();

        public PgDirectProduct() { }
        public PgDirectProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class PostgreSQLDirectExecutionIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_direct_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string AdminConnStr =>
            Environment.GetEnvironmentVariable("PG_ADMIN_CONNSTR")
            ?? "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=Pa55w0rd";

        private string TestConnStr =>
            $"Host=localhost;Port=5455;Database={_dbName};Username=postgres;Password=Pa55w0rd;CommandTimeout=1;Pooling=false;";

        private readonly PostgreSQLConnection _conn;

        public PostgreSQLDirectExecutionIntegrationTests()
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

        [Fact]
        public void ExecSQL_StringFormat_ReturnsMappedObjects()
        {
            var results = _conn.ExecSQL(new PgDirectProduct(_conn), "SELECT * FROM direct_products WHERE name = 'Widget'");
            
            results.Should().HaveCount(1);
            ((string)((PgDirectProduct)results[0]).Name.GetValue()).Should().Be("Widget");
            ((decimal)((PgDirectProduct)results[0]).Price.GetValue()).Should().Be(10m);
        }

        [Fact]
        public void ExecSQL_WithParameters_ReturnsMappedObjects()
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PriceTarget", 15m }
            };

            var results = _conn.ExecSQL(new PgDirectProduct(_conn), "SELECT * FROM direct_products WHERE price > @PriceTarget", parameters);
            
            results.Should().HaveCount(1);
            ((string)((PgDirectProduct)results[0]).Name.GetValue()).Should().Be("Gadget");
            ((decimal)((PgDirectProduct)results[0]).Price.GetValue()).Should().Be(20m);
        }

        [Fact]
        public void ExecStoredProcedure_ReturnsMappedObjects()
        {
            var parameters = new Dictionary<string, object> { { "@min_price", 15m } };
            var results = _conn.ExecSQL(new PgDirectProduct(_conn), "SELECT * FROM get_expensive_products(@min_price)", parameters);
            
            results.Should().HaveCount(1);
            ((string)((PgDirectProduct)results[0]).Name.GetValue()).Should().Be("Gadget");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            using (_conn.ExecSQL(
                "CREATE TABLE direct_products (" +
                "  \"ID\"     SERIAL PRIMARY KEY," +
                "  name     VARCHAR(200) NOT NULL," +
                "  price    DECIMAL(18,4) NOT NULL DEFAULT 0" +
                ")")) { }

            using (_conn.ExecSQL(
                "CREATE OR REPLACE FUNCTION get_expensive_products(min_price DECIMAL) " +
                "RETURNS SETOF direct_products AS $$ " +
                "BEGIN " +
                "    RETURN QUERY SELECT * FROM direct_products WHERE price > min_price; " +
                "END; $$ LANGUAGE plpgsql;")) { }
        }

        private void SeedData()
        {
            InsertProduct("Widget", 10m);
            InsertProduct("Gadget", 20m);
        }

        private void InsertProduct(string name, decimal price)
        {
            var p = new PgDirectProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.Insert();
        }

        private void CreateDatabase()
        {
            using var conn = new NpgsqlConnection(AdminConnStr);
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
            using var conn = new NpgsqlConnection(AdminConnStr);
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
