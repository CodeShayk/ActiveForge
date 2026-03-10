using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Driver;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    // ── Test entities ─────────────────────────────────────────────────────────────

    [Table("it_products")]
    public sealed class ItProduct : IdentityRecord
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public ItProduct() { }
        public ItProduct(DataConnection conn) : base(conn) { }
    }

    [Table("it_categories")]
    public sealed class ItCategory : IdentityRecord
    {
        [Column("name")] public TString Name = new TString();

        public ItCategory() { }
        public ItCategory(DataConnection conn) : base(conn) { }
    }

    [Table("it_items")]
    public sealed class ItItem : IdentityRecord
    {
        [Column("name")]    public TString     Name    = new TString();
        [Column("cat_id")]  public TForeignKey CatId   = new TForeignKey();

        [Join("cat_id", "_id", JoinAttribute.JoinTypeEnum.LeftOuterJoin)]
        public ItCategory Category = new ItCategory();

        public ItItem() { Category = new ItCategory(); }
        public ItItem(DataConnection conn) : base(conn) { Category = new ItCategory(conn); }
    }

    // ── CRUD + Querying Integration Tests ─────────────────────────────────────────

    /// <summary>
    /// Integration tests for MongoDB CRUD, querying and join capability
    /// running against a Docker-hosted MongoDB replica set.
    /// Each test class uses its own database for isolation.
    /// </summary>
    public sealed class MongoCrudIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_crud_{System.Threading.Interlocked.Increment(ref _counter)}";

        private readonly MongoDataConnection _conn;

        public MongoCrudIntegrationTests()
        {
            _conn = new MongoDataConnection("mongodb://localhost:27017", _dbName);
            _conn.Connect();
        }

        public void Dispose()
        {
            // Drop the test database to keep MongoDB clean
            var client = new MongoClient("mongodb://localhost:27017");
            _conn.Disconnect();
            client.DropDatabase(_dbName);
        }

        // ── Insert ────────────────────────────────────────────────────────────────

        [Fact]
        public void Insert_SetsIdentityField()
        {
            var p = new ItProduct(_conn);
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
            var ins = NewProduct("Gadget", 19.99m, false);
            var found = new ItProduct(_conn);
            found.ID.SetValue(ins.ID.GetValue());
            found.Read().Should().BeTrue();
            ((string)found.Name.GetValue()).Should().Be("Gadget");
            ((decimal)found.Price.GetValue()).Should().Be(19.99m);
            ((bool)found.InStock.GetValue()).Should().BeFalse();
        }

        [Fact]
        public void Read_NonExistentId_ThrowsPersistenceException()
        {
            var p = new ItProduct(_conn);
            p.ID.SetValue(99999);
            Action act = () => p.Read();
            act.Should().Throw<PersistenceException>();
        }

        // ── QueryCount ────────────────────────────────────────────────────────────

        [Fact]
        public void QueryCount_MatchingTerm_ReturnsCorrectCount()
        {
            NewProduct("A", 1m, true);
            NewProduct("B", 2m, true);
            NewProduct("C", 3m, false);

            var t   = new ItProduct(_conn);
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

            var r = new ItProduct(_conn);
            r.ID.SetValue(p.ID.GetValue());
            r.Read();
            ((string)r.Name.GetValue()).Should().Be("Updated");
            ((decimal)r.Price.GetValue()).Should().Be(10m);
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        [Fact]
        public void Delete_RemovesDocument()
        {
            var p = NewProduct("ToDelete", 1m, true);
            p.Delete();

            var t       = new ItProduct(_conn);
            var pred    = new EqualTerm(t, t.Name, "ToDelete");
            var results = _conn.QueryAll(t, pred, null, 0, null);
            results.Should().BeEmpty();
        }

        // ── QueryAll ──────────────────────────────────────────────────────────────

        [Fact]
        public void QueryAll_ReturnsAllDocuments()
        {
            NewProduct("A", 1m, true);
            NewProduct("B", 2m, false);
            NewProduct("C", 3m, true);

            var results = _conn.QueryAll(new ItProduct(_conn), null, null, 0, null);
            results.Count.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public void QueryAll_WithEqualTermFilter_ReturnsMatchingDocuments()
        {
            NewProduct("InStock",    5m, true);
            NewProduct("OutOfStock", 5m, false);

            var t       = new ItProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.InStock, true), null, 0, null);
            results.Should().NotBeEmpty();
            results.Should().AllSatisfy(r =>
                ((bool)((ItProduct)r).InStock.GetValue()).Should().BeTrue());
        }

        [Fact]
        public void QueryAll_WithSortOrder_ReturnsSortedDocuments()
        {
            NewProduct("Zebra", 3m, true);
            NewProduct("Apple", 1m, true);
            NewProduct("Mango", 2m, true);

            var t       = new ItProduct(_conn);
            var results = _conn.QueryAll(t, null, new OrderAscending(t, t.Name), 0, null);
            results.Cast<ItProduct>()
                   .Select(p => (string)p.Name.GetValue())
                   .Should().BeInAscendingOrder();
        }

        // ── QueryPage ─────────────────────────────────────────────────────────────

        [Fact]
        public void QueryPage_ReturnsPaginatedResults()
        {
            for (int i = 1; i <= 5; i++) NewProduct($"P{i:D2}", i * 1m, true);

            var t    = new ItProduct(_conn);
            var page = _conn.QueryPage(t, null, new OrderAscending(t, t.Name), 0, 3, null);
            page.Should().HaveCount(3);
        }

        // ── Transactions ──────────────────────────────────────────────────────────

        [Fact]
        public void Transaction_Rollback_DoesNotPersistInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new ItProduct(_conn);
            p.Name.SetValue("RollbackMe"); p.Price.SetValue(1m); p.InStock.SetValue(true);
            p.Insert();
            _conn.RollbackTransaction(tx);

            var t       = new ItProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "RollbackMe"), null, 0, null);
            results.Should().BeEmpty();
        }

        [Fact]
        public void Transaction_Commit_PersistsInsert()
        {
            var tx = _conn.BeginTransaction();
            var p  = new ItProduct(_conn);
            p.Name.SetValue("CommitMe"); p.Price.SetValue(2m); p.InStock.SetValue(true);
            p.Insert();
            _conn.CommitTransaction(tx);

            var t       = new ItProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "CommitMe"), null, 0, null);
            results.Should().HaveCount(1);
        }

        [Fact]
        public void UnitOfWork_AutoTransaction_CommitsOnSuccess()
        {
            var uow = new MongoUnitOfWork(_conn);
            _conn.UnitOfWork = uow;
            try
            {
                var p = new ItProduct(_conn);
                p.Name.SetValue("UoWProduct"); p.Price.SetValue(7.99m); p.InStock.SetValue(true);
                p.Insert();
            }
            finally
            {
                _conn.UnitOfWork = null;
                if (!_conn.IsOpen) _conn.Connect();
            }

            var t       = new ItProduct(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t, t.Name, "UoWProduct"), null, 0, null);
            results.Should().HaveCount(1);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private ItProduct NewProduct(string name, decimal price, bool inStock)
        {
            var p = new ItProduct(_conn);
            p.Name.SetValue(name); p.Price.SetValue(price); p.InStock.SetValue(inStock);
            p.Insert();
            return p;
        }
    }

    // ── JOIN Integration Tests ────────────────────────────────────────────────────

    /// <summary>
    /// Integration tests for MongoDB $lookup join support.
    /// </summary>
    public sealed class MongoJoinIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_join_{System.Threading.Interlocked.Increment(ref _counter)}";

        private readonly MongoDataConnection _conn;
        private int _electronicsId;
        private int _booksId;

        public MongoJoinIntegrationTests()
        {
            _conn = new MongoDataConnection("mongodb://localhost:27017", _dbName);
            _conn.Connect();
            SeedData();
        }

        public void Dispose()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            _conn.Disconnect();
            client.DropDatabase(_dbName);
        }

        // ── Group A — $lookup JOIN (populates embedded field) ─────────────────────

        [Fact]
        public void Join_QueryAll_PopulatesEmbeddedCategory()
        {
            var results = _conn.QueryAll(new ItItem(_conn), null, null, 0, null);
            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r =>
                ((ItItem)r).Category.Name.IsNull().Should().BeFalse());
        }

        [Fact]
        public void Join_LeftOuter_IncludesOrphanItem()
        {
            InsertItem("Orphan", null);
            var results = _conn.QueryAll(new ItItem(_conn), null, null, 0, null);
            results.Should().HaveCount(4);
        }

        [Fact]
        public void Join_LeftOuter_NullCategoryForOrphan()
        {
            InsertItem("Orphan", null);
            var results = _conn.QueryAll(new ItItem(_conn), null, null, 0, null);
            var orphan  = results.Cast<ItItem>()
                                 .Single(i => (string)i.Name.GetValue() == "Orphan");
            orphan.Category.Name.IsNull().Should().BeTrue();
        }

        // ── Group B — Filter on joined field ──────────────────────────────────────

        [Fact]
        public void Join_FilterOnJoinedField_ReturnsMatchingRows()
        {
            var t       = new ItItem(_conn);
            var results = _conn.QueryAll(t, new EqualTerm(t.Category, t.Category.Name, "Books"), null, 0, null);
            results.Should().HaveCount(1);
            ((string)((ItItem)results[0]).Name.GetValue()).Should().Be("SQL Mastery");
        }

        // ── Group C — Sort on joined field ────────────────────────────────────────

        [Fact]
        public void Join_SortByJoinedField_SortsCorrectly()
        {
            var t       = new ItItem(_conn);
            var results = _conn.QueryAll(t, null, new OrderAscending(t.Category, t.Category.Name), 0, null);
            var names   = results.Cast<ItItem>()
                                 .Select(i => (string)i.Category.Name.GetValue())
                                 .Where(n => n != null)
                                 .ToList();
            names.Should().BeInAscendingOrder();
        }

        // ── Group D — Pagination with join ────────────────────────────────────────

        [Fact]
        public void Join_QueryPage_ReturnsPaginatedResults()
        {
            var t    = new ItItem(_conn);
            var page = _conn.QueryPage(t, null, new OrderAscending(t, t.Name), 0, 2, null);
            page.Should().HaveCount(2);
        }

        // ── Group E — Join-type override ──────────────────────────────────────────

        [Fact]
        public void JoinOverride_LeftOuterToInner_ExcludesOrphan()
        {
            InsertItem("Orphan", null);
            var overrides = new List<JoinOverride>
                { new JoinOverride(typeof(ItCategory), JoinSpecification.JoinTypeEnum.InnerJoin) };
            var results = _conn.QueryAll(new ItItem(_conn), null, null, 0, null, overrides);
            results.Should().HaveCount(3);
        }

        // ── Group F — LINQ ────────────────────────────────────────────────────────

        [Fact]
        public void Linq_Where_JoinedField_FiltersByCategory()
        {
            var results = _conn.Query(new ItItem(_conn))
                .Where(x => x.Category.Name == "Books")
                .ToList();
            results.Should().HaveCount(1);
            ((string)results[0].Name.GetValue()).Should().Be("SQL Mastery");
        }

        [Fact]
        public void Linq_OrderBy_JoinedField_SortsCorrectly()
        {
            var results = _conn.Query(new ItItem(_conn))
                .OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Name)
                .ToList();
            ((string)results[0].Category.Name.GetValue()).Should().Be("Books");
            ((string)results[^1].Category.Name.GetValue()).Should().Be("Electronics");
        }

        [Fact]
        public void Linq_LeftOuter_ThenInnerOverride_ExcludesOrphan()
        {
            InsertItem("Orphan", null);
            var results = _conn.Query(new ItItem(_conn))
                .InnerJoin<ItCategory>()
                .ToList();
            results.Should().HaveCount(3);
        }

        [Fact]
        public void Linq_FullChain_Where_OrderBy_Pagination()
        {
            InsertItem("Orphan", null);
            var results = _conn.Query(new ItItem(_conn))
                .Where(x => x.Name != (TString)null)
                .OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Name)
                .Skip(0)
                .Take(3)
                .ToList();
            results.Should().HaveCount(3);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SeedData()
        {
            _electronicsId = InsertCategory("Electronics");
            _booksId       = InsertCategory("Books");
            InsertItem("Phone",       _electronicsId);
            InsertItem("Laptop",      _electronicsId);
            InsertItem("SQL Mastery", _booksId);
        }

        private int InsertCategory(string name)
        {
            var c = new ItCategory(_conn);
            c.Name.SetValue(name);
            c.Insert();
            return (int)c.ID.GetValue();
        }

        private void InsertItem(string name, int? catId)
        {
            var i = new ItItem(_conn);
            i.Name.SetValue(name);
            if (catId.HasValue) i.CatId.SetValue(catId.Value);
            i.Insert();
        }
    }
}
