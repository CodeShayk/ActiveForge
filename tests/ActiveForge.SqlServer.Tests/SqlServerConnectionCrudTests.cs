using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Query;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    // ── Test entity ───────────────────────────────────────────────────────────────

    [Table("products")]
    public sealed class SsProduct : IdentDataObject
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public SsProduct() { }
        public SsProduct(DataConnection conn) : base(conn) { }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests that run against a real SQL Server LocalDB instance.
    /// Each test class instance gets its own uniquely named database so test
    /// runs are fully isolated even when executed in parallel.
    /// </summary>
    public sealed class SqlServerConnectionCrudTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_crud_{System.Threading.Interlocked.Increment(ref _counter)}";

        private const string MasterConnStr =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;" +
            "Integrated Security=True;TrustServerCertificate=True";

        private string TestConnStr =>
            $@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog={_dbName};" +
            "Integrated Security=True;TrustServerCertificate=True;" +
            "MultipleActiveResultSets=True";

        private readonly SqlServerConnection _conn;

        public SqlServerConnectionCrudTests()
        {
            CreateDatabase();
            _conn = new SqlServerConnection(TestConnStr);
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
            var p = new SsProduct(_conn);
            p.Name.SetValue("Widget");
            p.Price.SetValue(9.99m);
            p.InStock.SetValue(true);

            p.Insert();

            ((int)p.ID.GetValue()).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Insert_MultipleProducts_AssignDistinctIds()
        {
            var p1 = NewProduct("Alpha", 1.00m, true);
            var p2 = NewProduct("Beta",  2.00m, false);

            ((int)p1.ID.GetValue()).Should().NotBe((int)p2.ID.GetValue());
        }

        // ── Read ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Read_ByPrimaryKey_ReturnsCorrectRow()
        {
            var inserted = NewProduct("Gadget", 19.99m, false);

            var found = new SsProduct(_conn);
            found.ID.SetValue(inserted.ID.GetValue());
            bool ok = found.Read();

            ok.Should().BeTrue();
            ((string)found.Name.GetValue()).Should().Be("Gadget");
            ((decimal)found.Price.GetValue()).Should().Be(19.99m);
            ((bool)found.InStock.GetValue()).Should().BeFalse();
        }

        [Fact]
        public void Read_NonExistentId_ThrowsPersistenceException()
        {
            var p = new SsProduct(_conn);
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

            var t    = new SsProduct(_conn);
            var term = new EqualTerm(t, t.InStock, true);
            int cnt  = _conn.QueryCount(t, term);

            cnt.Should().Be(2);
        }

        // ── Update ────────────────────────────────────────────────────────────────

        [Fact]
        public void Update_ChangesFieldValues()
        {
            var p = NewProduct("Original", 5.00m, true);
            p.Name.SetValue("Updated");
            p.Price.SetValue(10.00m);
            p.Update(DataObjectLock.UpdateOption.IgnoreLock);

            var reread = new SsProduct(_conn);
            reread.ID.SetValue(p.ID.GetValue());
            reread.Read();

            ((string)reread.Name.GetValue()).Should().Be("Updated");
            ((decimal)reread.Price.GetValue()).Should().Be(10.00m);
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesRow()
        {
            var p = NewProduct("ToDelete", 1.00m, true);
            p.Delete();

            var t       = new SsProduct(_conn);
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

            var t       = new SsProduct(_conn);
            var results = _conn.QueryAll(t, null, null, 0, null);

            results.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public void QueryAll_WithEqualTermFilter_ReturnsMatchingRows()
        {
            NewProduct("InStock",    5m, true);
            NewProduct("OutOfStock", 5m, false);

            var t       = new SsProduct(_conn);
            var pred    = new EqualTerm(t, t.InStock, true);
            var results = _conn.QueryAll(t, pred, null, 0, null);

            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(r =>
                ((bool)((SsProduct)r).InStock.GetValue()).Should().BeTrue());
        }

        [Fact]
        public void QueryAll_WithSortOrder_ReturnsSortedRows()
        {
            NewProduct("Zebra",  9m, true);
            NewProduct("Apple",  1m, true);
            NewProduct("Mango",  5m, true);

            var t       = new SsProduct(_conn);
            var sort    = new OrderAscending(t, t.Name);
            var results = _conn.QueryAll(t, null, sort, 0, null);

            var names = results.Cast<SsProduct>().Select(p => (string)p.Name.GetValue()).ToList();
            names.Should().BeInAscendingOrder();
        }

        [Fact]
        public void QueryPage_ReturnsPaginatedResults()
        {
            for (int i = 1; i <= 5; i++)
                NewProduct($"P{i:D2}", i * 1m, true);

            var t       = new SsProduct(_conn);
            var sort    = new OrderAscending(t, t.Name);
            var page    = _conn.QueryPage(t, null, sort, 0, 3, null);

            page.Should().HaveCount(3);
        }

        // ── Transaction rollback ──────────────────────────────────────────────────

        [Fact]
        public void Transaction_Rollback_DoesNotPersistInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new SsProduct(_conn);
            p.Name.SetValue("RollbackMe");
            p.Price.SetValue(1m);
            p.InStock.SetValue(true);
            p.Insert();
            _conn.RollbackTransaction(tx);

            var t       = new SsProduct(_conn);
            var pred    = new EqualTerm(t, t.Name, "RollbackMe");
            var results = _conn.QueryAll(t, pred, null, 0, null);

            results.Should().BeEmpty();
        }

        [Fact]
        public void Transaction_Commit_PersistsInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new SsProduct(_conn);
            p.Name.SetValue("CommitMe");
            p.Price.SetValue(2m);
            p.InStock.SetValue(true);
            p.Insert();
            _conn.CommitTransaction(tx);

            var t       = new SsProduct(_conn);
            var pred    = new EqualTerm(t, t.Name, "CommitMe");
            var results = _conn.QueryAll(t, pred, null, 0, null);

            results.Should().HaveCount(1);
        }

        // ── UnitOfWork auto-transaction ───────────────────────────────────────────

        [Fact]
        public void UnitOfWork_AutoTransaction_CommitsOnSuccess()
        {
            var uow = new SqlServerUnitOfWork(_conn);
            _conn.UnitOfWork = uow;
            try
            {
                var p = new SsProduct(_conn);
                p.Name.SetValue("UoWProduct");
                p.Price.SetValue(7.99m);
                p.InStock.SetValue(true);
                p.Insert();
            }
            finally
            {
                _conn.UnitOfWork = null;
                if (!_conn.IsOpen) _conn.Connect();
            }

            var t       = new SsProduct(_conn);
            var pred    = new EqualTerm(t, t.Name, "UoWProduct");
            var results = _conn.QueryAll(t, pred, null, 0, null);

            results.Should().HaveCount(1);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private SsProduct NewProduct(string name, decimal price, bool inStock)
        {
            var p = new SsProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.InStock.SetValue(inStock);
            p.Insert();
            return p;
        }

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
