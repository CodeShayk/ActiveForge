using System;
using System.Linq;
using FluentAssertions;
using MongoDB.Driver;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.Linq;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    // ── Test entity ───────────────────────────────────────────────────────────────

    [Table("linq_products")]
    public sealed class MongoLinqProduct : IdentityRecord
    {
        [Column("name")]     public TString  Name    = new TString();
        [Column("price")]    public TDecimal Price   = new TDecimal();
        [Column("in_stock")] public TBool    InStock = new TBool();

        public MongoLinqProduct() { }
        public MongoLinqProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class MongoLinqIntegrationTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_linq_{System.Threading.Interlocked.Increment(ref _counter)}";

        private readonly MongoDataConnection _conn;

        public MongoLinqIntegrationTests()
        {
            _conn = new MongoDataConnection("mongodb://localhost:27017", _dbName);
            _conn.Connect();
            SeedData();
        }

        public void Dispose()
        {
            // Drop the test database to keep MongoDB clean
            var client = new MongoClient("mongodb://localhost:27017");
            _conn.Disconnect();
            client.DropDatabase(_dbName);
        }

        // ── Scalar Execution ──────────────────────────────────────────────────────
        
        [Fact]
        public void LinqCount_ReturnsCorrectCount()
        {
            var count = _conn.Query(new MongoLinqProduct(_conn)).Where(p => p.InStock).Count();
            count.Should().Be(2); 
        }

        [Fact]
        public void LinqAny_ReturnsTrueWhenRecordsExist()
        {
            var exists = _conn.Query(new MongoLinqProduct(_conn)).Where(p => p.Name == "Widget").Any();
            exists.Should().BeTrue();
        }

        [Fact]
        public void LinqFirst_ReturnsFirstRecord()
        {
            var first = _conn.Query(new MongoLinqProduct(_conn)).OrderBy(p => p.Name).First();
            ((string)first.Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqSingle_ReturnsSingleRecord()
        {
            var single = _conn.Query(new MongoLinqProduct(_conn)).Where(p => p.Name == "Widget").Single();
            ((string)single.Name.GetValue()).Should().Be("Widget");
        }

        // ── Predicate Translations ────────────────────────────────────────────────
        
        [Fact]
        public void LinqStartsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new MongoLinqProduct(_conn)).Where(p => ((string)p.Name).StartsWith("Gad")).ToList();
            result.Should().HaveCount(1);
            ((string)result[0].Name.GetValue()).Should().Be("Gadget");
        }

        [Fact]
        public void LinqEndsWith_ReturnsMatchingRecord()
        {
            var result = _conn.Query(new MongoLinqProduct(_conn)).Where(p => ((string)p.Name).EndsWith("get")).ToList();
            result.Should().HaveCount(2); // Gadget, Widget
        }

        [Fact]
        public void LinqImplicitBool_ReturnsMatchingRecords()
        {
            var result = _conn.Query(new MongoLinqProduct(_conn)).Where(p => p.InStock).ToList();
            result.Should().HaveCount(2);
        }

        // ── Projections (Select) ──────────────────────────────────────────────────
        
        [Fact]
        public void LinqSelect_ProjectToAnonymousType_ReturnsCorrectData()
        {
            var result = _conn.Query(new MongoLinqProduct(_conn))
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

        private void SeedData()
        {
            InsertProduct("Widget", 19.99m, true);
            InsertProduct("Gadget", 29.99m, false);
            InsertProduct("Zebra", 99.99m, true);
        }

        private void InsertProduct(string name, decimal price, bool inStock)
        {
            var p = new MongoLinqProduct(_conn);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.InStock.SetValue(inStock);
            p.Insert();
        }
    }
}
