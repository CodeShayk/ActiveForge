using System;
using FluentAssertions;
using Turquoise.ORM;
using Turquoise.ORM.Attributes;
using Turquoise.ORM.Query;
using Turquoise.ORM.Transactions;
using Xunit;

namespace Turquoise.ORM.SQLite.Tests
{
    // ── Test entity ───────────────────────────────────────────────────────────────

    [Table("products")]
    public class Product : IdentDataObject
    {
        [Column("name")]     public TString  Name     = new TString();
        [Column("price")]    public TDecimal Price    = new TDecimal();
        [Column("in_stock")] public TBool    InStock  = new TBool();

        public Product() { }
        public Product(DataConnection conn) : base(conn) { }
    }

    // ── Fixture ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests that run against a real in-memory SQLite database.
    /// Each test class instance gets its own named shared-cache database so the
    /// connection can be reopened within <see cref="RunWrite"/> without losing state.
    /// </summary>
    public class SQLiteConnectionCrudTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"testdb_{System.Threading.Interlocked.Increment(ref _counter)}";
        private readonly SQLiteConnection _conn;

        private string ConnStr =>
            $"Data Source={_dbName};Mode=Memory;Cache=Shared";

        public SQLiteConnectionCrudTests()
        {
            _conn = new SQLiteConnection(ConnStr);
            _conn.Connect();
            CreateSchema();
        }

        public void Dispose()
        {
            _conn.Disconnect();
        }

        private void CreateSchema()
        {
            _conn.ExecSQL(
                "CREATE TABLE IF NOT EXISTS products (" +
                "  id       INTEGER PRIMARY KEY AUTOINCREMENT," +
                "  name     TEXT    NOT NULL," +
                "  price    NUMERIC NOT NULL DEFAULT 0," +
                "  in_stock INTEGER NOT NULL DEFAULT 1" +
                ")");
        }

        // ── Insert ────────────────────────────────────────────────────────────────

        [Fact]
        public void Insert_SetsIdentityField()
        {
            var p = new Product(_conn);
            p.Name.SetValue("Widget");
            p.Price.SetValue(9.99m);
            p.InStock.SetValue(true);

            p.Insert();

            ((int)p.ID.GetValue()).Should().BeGreaterThan(0);
        }

        [Fact]
        public void Insert_MultipleProducts_AssignDistinctIds()
        {
            var p1 = new Product(_conn);
            p1.Name.SetValue("Alpha");
            p1.Price.SetValue(1.00m);
            p1.InStock.SetValue(true);
            p1.Insert();

            var p2 = new Product(_conn);
            p2.Name.SetValue("Beta");
            p2.Price.SetValue(2.00m);
            p2.InStock.SetValue(false);
            p2.Insert();

            ((int)p1.ID.GetValue()).Should().NotBe((int)p2.ID.GetValue());
        }

        // ── Read ──────────────────────────────────────────────────────────────────

        [Fact]
        public void Read_ByPrimaryKey_ReturnsCorrectRow()
        {
            var inserted = new Product(_conn);
            inserted.Name.SetValue("Gadget");
            inserted.Price.SetValue(19.99m);
            inserted.InStock.SetValue(false);
            inserted.Insert();

            var found = new Product(_conn);
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
            // DBDataConnection.Read() throws PersistenceException when no row is found.
            var p = new Product(_conn);
            p.ID.SetValue(99999);

            Action act = () => p.Read();
            act.Should().Throw<PersistenceException>();
        }

        [Fact]
        public void QueryCount_NonExistentId_ReturnsZero()
        {
            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.ID, 99999);
            int count     = _conn.QueryCount(template, predicate);

            count.Should().Be(0);
        }

        // ── Update ────────────────────────────────────────────────────────────────

        [Fact]
        public void Update_ChangesFieldValues()
        {
            var p = new Product(_conn);
            p.Name.SetValue("Original");
            p.Price.SetValue(5.00m);
            p.InStock.SetValue(true);
            p.Insert();

            p.Name.SetValue("Updated");
            p.Price.SetValue(10.00m);
            p.Update(DataObjectLock.UpdateOption.IgnoreLock);

            var reread = new Product(_conn);
            reread.ID.SetValue(p.ID.GetValue());
            reread.Read();

            ((string)reread.Name.GetValue()).Should().Be("Updated");
            ((decimal)reread.Price.GetValue()).Should().Be(10.00m);
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesRow()
        {
            var p = new Product(_conn);
            p.Name.SetValue("ToDelete");
            p.Price.SetValue(1.00m);
            p.InStock.SetValue(true);
            p.Insert();

            p.Delete();

            // DBDataConnection.Read() throws when row not found; verify via QueryAll instead.
            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.Name, "ToDelete");
            var results   = _conn.QueryAll(template, predicate, null, 0, null);

            results.Should().BeEmpty();
        }

        // ── QueryAll ──────────────────────────────────────────────────────────────

        [Fact]
        public void QueryAll_ReturnsAllRows()
        {
            InsertProduct("A", 1.00m, true);
            InsertProduct("B", 2.00m, false);
            InsertProduct("C", 3.00m, true);

            var template = new Product(_conn);
            var results  = _conn.QueryAll(template, null, null, 0, null);

            results.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public void QueryAll_WithEqualTermFilter_ReturnsMatchingRows()
        {
            InsertProduct("InStockItem", 5.00m, true);
            InsertProduct("OutOfStockItem", 5.00m, false);

            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.InStock, true);
            var results   = _conn.QueryAll(template, predicate, null, 0, null);

            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(r =>
                ((bool)((Product)r).InStock.GetValue()).Should().BeTrue());
        }

        // ── Transaction rollback ──────────────────────────────────────────────────

        [Fact]
        public void Transaction_Rollback_DoesNotPersistInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new Product(_conn);
            p.Name.SetValue("RollbackMe");
            p.Price.SetValue(1.00m);
            p.InStock.SetValue(true);
            p.Insert();
            _conn.RollbackTransaction(tx);

            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.Name, "RollbackMe");
            var results   = _conn.QueryAll(template, predicate, null, 0, null);

            results.Should().BeEmpty();
        }

        [Fact]
        public void Transaction_Commit_PersistsInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new Product(_conn);
            p.Name.SetValue("CommitMe");
            p.Price.SetValue(2.00m);
            p.InStock.SetValue(true);
            p.Insert();
            _conn.CommitTransaction(tx);

            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.Name, "CommitMe");
            var results   = _conn.QueryAll(template, predicate, null, 0, null);

            results.Should().HaveCount(1);
        }

        // ── UnitOfWork auto-transaction ───────────────────────────────────────────

        [Fact]
        public void UnitOfWork_AutoTransaction_CommitsOnSuccess()
        {
            var uow = new SQLiteUnitOfWork(_conn);
            _conn.UnitOfWork = uow;
            try
            {
                var p = new Product(_conn);
                p.Name.SetValue("UoWProduct");
                p.Price.SetValue(7.99m);
                p.InStock.SetValue(true);
                p.Insert();  // RunWrite: opens UoW tx, inserts, commits, disconnects
            }
            finally
            {
                _conn.UnitOfWork = null;
                if (!_conn.IsOpen) _conn.Connect();
            }

            var template  = new Product(_conn);
            var predicate = new EqualTerm(template, template.Name, "UoWProduct");
            var results   = _conn.QueryAll(template, predicate, null, 0, null);

            results.Should().HaveCount(1);
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private void InsertProduct(string name, decimal price, bool inStock)
        {
            var p = new Product(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.InStock.SetValue(inStock);
            p.Insert();
        }
    }
}
