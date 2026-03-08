using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Turquoise.ORM;
using Turquoise.ORM.MongoDB.Internal;
using Turquoise.ORM.Query;
using Xunit;

namespace Turquoise.ORM.MongoDB.Tests
{
    /// <summary>
    /// Tests for MongoQueryTranslator — verifies that ORM QueryTerms are translated
    /// into non-null FilterDefinitions. No server connection is required.
    /// </summary>
    public class MongoQueryTranslatorTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        private MongoTestProduct NewProduct() => new MongoTestProduct(_conn);
        private MongoTestOrder  NewOrder()   => new MongoTestOrder(_conn);

        // ── Null term ─────────────────────────────────────────────────────────────────

        [Fact]
        public void NullTerm_ReturnsEmptyFilter()
        {
            var filter = MongoQueryTranslator.Translate(null, NewProduct());
            filter.Should().NotBeNull();
        }

        // ── EqualTerm ─────────────────────────────────────────────────────────────────

        [Fact]
        public void EqualTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new EqualTerm(p, p.Name, "Widget");
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void EqualTerm_OnBoolField_ProducesFilter()
        {
            var p      = NewProduct();
            var term   = new EqualTerm(p, p.InStock, true);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void EqualTerm_OnDecimalField_ProducesFilter()
        {
            var p      = NewProduct();
            var term   = new EqualTerm(p, p.Price, 9.99m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── GreaterThanTerm ───────────────────────────────────────────────────────────

        [Fact]
        public void GreaterThanTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new GreaterThanTerm(p, p.Price, 5.00m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── LessThanTerm ──────────────────────────────────────────────────────────────

        [Fact]
        public void LessThanTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new LessThanTerm(p, p.Price, 100.00m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── IsNullTerm ────────────────────────────────────────────────────────────────

        [Fact]
        public void IsNullTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new IsNullTerm(p, p.Name);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── ContainsTerm ──────────────────────────────────────────────────────────────

        [Fact]
        public void ContainsTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new ContainsTerm(p, p.Name, "widget");
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── InTerm ────────────────────────────────────────────────────────────────────

        [Fact]
        public void InTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var values = new List<object> { "Alpha", "Beta", "Gamma" };
            var term   = new InTerm(p, p.Name, values);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── Composite terms ───────────────────────────────────────────────────────────

        [Fact]
        public void AndTerm_ProducesNonNullFilter()
        {
            var p     = NewProduct();
            var left  = new EqualTerm(p, p.InStock, true);
            var right = new GreaterThanTerm(p, p.Price, 0m);
            var term  = left & right;

            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void OrTerm_ProducesNonNullFilter()
        {
            var p     = NewProduct();
            var left  = new EqualTerm(p, p.InStock, true);
            var right = new EqualTerm(p, p.InStock, false);
            var term  = left | right;

            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void NotTerm_ProducesNonNullFilter()
        {
            var p    = NewProduct();
            var inner = new EqualTerm(p, p.InStock, false);
            var term  = !inner;

            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void NestedComposite_And_Or_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var inStock = new EqualTerm(p, p.InStock, true);
            var priceLow = new LessThanTerm(p, p.Price, 10m);
            var priceHigh = new GreaterThanTerm(p, p.Price, 100m);

            var term = inStock & (priceLow | priceHigh);

            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        // ── Sort translation ──────────────────────────────────────────────────────────

        [Fact]
        public void NullSortOrder_ReturnsNull()
        {
            var sort = MongoQueryTranslator.TranslateSort(null, NewProduct());
            sort.Should().BeNull();
        }

        [Fact]
        public void AscendingSortOrder_ReturnsNonNullSort()
        {
            var p    = NewProduct();
            var sort = new OrderAscending(p, p.Name);
            var def  = MongoQueryTranslator.TranslateSort(sort, p);
            def.Should().NotBeNull();
        }

        [Fact]
        public void AscendingSortOnPrice_ReturnsNonNullSort()
        {
            var p    = NewProduct();
            var sort = new OrderAscending(p, p.Price);
            var def  = MongoQueryTranslator.TranslateSort(sort, p);
            def.Should().NotBeNull();
        }
    }
}
