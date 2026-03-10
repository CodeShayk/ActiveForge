using System;
using System.Linq;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Query;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    // ── Test entity ───────────────────────────────────────────────────────────────

    [Table("products")]
    public sealed class PgProduct : IdentityRecord
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public PgProduct() { }
        public PgProduct(DataConnection conn) : base(conn) { }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests for PostgreSQL CRUD and querying running against a Docker container.
    /// Each test class instance creates its own uniquely-named database for isolation.
    /// </summary>
    public sealed class PostgreSQLConnectionCrudTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_crud_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string AdminConnStr =>
            Environment.GetEnvironmentVariable("PG_ADMIN_CONNSTR")
            ?? "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=Pa55w0rd";

        private string TestConnStr =>
            $"Host=localhost;Port=5455;Database={_dbName};Username=postgres;Password=Pa55w0rd";

        private readonly PostgreSQLConnection _conn;

        public PostgreSQLConnectionCrudTests()
        {
            CreateDatabase();
            _conn = new PostgreSQLConnection(TestConnStr);
            _conn.Connect();
            CreateSchema();
        }

        public void Dispose()
        {
            _conn.Disconnect();
            DropDatabase();
        }

        // ── Insert ────────────────────────────────────────────────────────────────

        [Fact]
        public void Insert_SetsIdentityField()
        {
            var p = new PgProduct(_conn);
            p.Name.SetValue("Widget");
            p.Price.SetValue(9.99m);
            p.InStock.SetValue(true);
            p.Insert();
            ((int)p.ID.GetValue()).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Insert_MultipleProducts_AssignDistinctIds()
        {
            var p1 = NewProduct("Alpha", 1m, true);
            var p2 = NewProduct("Beta",  2m, false);
            ((int)p1.ID.GetValue()).Should().NotBe((int)p2.ID.GetValue());
        }

        // ── Read ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Read_ByPrimaryKey_ReturnsCorrectRow()
        {
            var ins  = NewProduct("Gadget", 19.99m, false);
            var found = new PgProduct(_conn);
            found.ID.SetValue(ins.ID.GetValue());
            found.Read().Should().BeTrue();
            ((string)found.Name.GetValue()).Should().Be("Gadget");
            ((decimal)found.Price.GetValue()).Should().Be(19.99m);
            ((bool)found.InStock.GetValue()).Should().BeFalse();
        }

        [Fact]
        public void Read_NonExistentId_ThrowsPersistenceException()
        {
            var p = new PgProduct(_conn);
            p.ID.SetValue(99999);
            Action act = () => p.Read();
            act.Should().Throw<PersistenceException>();
        }

        [Fact]
        public void QueryCount_MatchingTerm_ReturnsCorrectCount()
        {
            NewProduct("A", 1m, true);
            NewProduct("B", 2m, true);
            NewProduct("C", 3m, false);

            var t   = new PgProduct(_conn);
            var cnt = _conn.QueryCount(t, new EqualTerm(t, t.InStock, true));
            cnt.Should().Be(2);
        }

        // ── Update ────────────────────────────────────────────────────────────────

        [Fact]
        public void Update_ChangesFieldValues()
        {
            var p = NewProduct("Original", 5m, true);
            p.Name.SetValue("Updated");
            p.Price.SetValue(10m);
            p.Update(RecordLock.UpdateOption.IgnoreLock);

            var r = new PgProduct(_conn);
            r.ID.SetValue(p.ID.GetValue());
            r.Read();
            ((string)r.Name.GetValue()).Should().Be("Updated");
            ((decimal)r.Price.GetValue()).Should().Be(10m);
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesRow()
        {
            var p = NewProduct("ToDelete", 1m, true);
            p.Delete();

            var t       = new PgProduct(_conn);
            var pred    = new EqualTerm(t, t.Name, "ToDelete");
            var results = _conn.QueryAll(t, pred, null, 0, null);
            results.Should().BeEmpty();
        }

        // ── QueryAll ──────────────────────────────────────────────────────────────

        [Fact]
        public void QueryAll_ReturnsAllRows()
        {
            NewProduct("A", 1m, true);
            NewProduct("B", 2m, false);
            NewProduct("C", 3m, true);

            var results = _conn.QueryAll(new PgProduct(_conn), null, null, 0, null);
            results.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public void QueryAll_WithEqualTermFilter_ReturnsMatchingRows()
        {
            NewProduct("InStock",    5m, true);
            NewProduct("OutOfStock", 5m, false);

            var t       = new PgProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.InStock, true), null, 0, null);
            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(r =>
                ((bool)((PgProduct)r).InStock.GetValue()).Should().BeTrue());
        }

        [Fact]
        public void QueryAll_WithSortOrder_ReturnsSortedRows()
        {
            NewProduct("Zebra", 3m, true);
            NewProduct("Apple", 1m, true);
            NewProduct("Mango", 2m, true);

            var t       = new PgProduct(_conn);
            var results = _conn.QueryAll(t, null, new OrderAscending(t, t.Name), 0, null);
            results.Cast<PgProduct>()
                   .Select(p => (string)p.Name.GetValue())
                   .Should().BeInAscendingOrder();
        }

        [Fact]
        public void QueryPage_ReturnsPaginatedResults()
        {
            for (int i = 1; i <= 5; i++) NewProduct($"P{i:D2}", i * 1m, true);

            var t    = new PgProduct(_conn);
            var page = _conn.QueryPage(t, null, new OrderAscending(t, t.Name), 0, 3, null);
            page.Should().HaveCount(3);
        }

        // ── Transactions ──────────────────────────────────────────────────────────

        [Fact]
        public void Transaction_Rollback_DoesNotPersistInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new PgProduct(_conn);
            p.Name.SetValue("RollbackMe"); p.Price.SetValue(1m); p.InStock.SetValue(true);
            p.Insert();
            _conn.RollbackTransaction(tx);

            var t       = new PgProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "RollbackMe"), null, 0, null);
            results.Should().BeEmpty();
        }

        [Fact]
        public void Transaction_Commit_PersistsInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new PgProduct(_conn);
            p.Name.SetValue("CommitMe"); p.Price.SetValue(2m); p.InStock.SetValue(true);
            p.Insert();
            _conn.CommitTransaction(tx);

            var t       = new PgProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "CommitMe"), null, 0, null);
            results.Should().HaveCount(1);
        }

        [Fact]
        public void UnitOfWork_AutoTransaction_CommitsOnSuccess()
        {
            var uow = new PostgreSQLUnitOfWork(_conn);
            _conn.UnitOfWork = uow;
            try
            {
                var p = new PgProduct(_conn);
                p.Name.SetValue("UoWProduct"); p.Price.SetValue(7.99m); p.InStock.SetValue(true);
                p.Insert();
            }
            finally
            {
                _conn.UnitOfWork = null;
                if (!_conn.IsOpen) _conn.Connect();
            }

            var t       = new PgProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "UoWProduct"), null, 0, null);
            results.Should().HaveCount(1);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private PgProduct NewProduct(string name, decimal price, bool inStock)
        {
            var p = new PgProduct(_conn);
            p.Name.SetValue(name); p.Price.SetValue(price); p.InStock.SetValue(inStock);
            p.Insert();
            return p;
        }

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
