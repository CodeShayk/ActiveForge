using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using ActiveForge;
using ActiveForge.Attributes;
using ActiveForge.MongoDB.Internal;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Unit tests for MongoDB join support (MongoJoinBuilder + MongoMapper.FromBsonDocumentWithJoins).
    /// No MongoDB server required — all tests exercise pure in-memory logic.
    /// </summary>
    public class MongoJoinBuilderTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        // ── BuildStages — explicit [Join] attribute ───────────────────────────────────

        [Fact]
        public void BuildStages_WithJoinAttribute_ReturnsOneStage()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages.Should().HaveCount(1);
        }

        [Fact]
        public void BuildStages_WithJoinAttribute_HasCorrectCollectionName()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages[0].CollectionName.Should().Be("products");
        }

        [Fact]
        public void BuildStages_WithJoinAttribute_HasCorrectLocalField()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages[0].LocalField.Should().Be("product_id");
        }

        [Fact]
        public void BuildStages_WithJoinAttribute_HasCorrectForeignField()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages[0].ForeignField.Should().Be("_id");
        }

        [Fact]
        public void BuildStages_WithJoinAttribute_AliasIsUnique()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages[0].Alias.Should().StartWith("__joined_");
        }

        [Fact]
        public void BuildStages_LeftOuterJoinAttribute_IsLeftJoinTrue()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));
            stages[0].IsLeftJoin.Should().BeTrue();
        }

        // ── BuildStages — convention FK discovery ─────────────────────────────────────

        [Fact]
        public void BuildStages_ConventionFk_FindsProductIDField()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItemConv));
            stages.Should().HaveCount(1);
            stages[0].LocalField.Should().Be("product_id");
        }

        [Fact]
        public void BuildStages_NoEmbeddedDataObjects_ReturnsEmpty()
        {
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestProduct));
            stages.Should().BeEmpty();
        }

        // ── BuildStages — JoinOverride changes join type ──────────────────────────────

        [Fact]
        public void BuildStages_JoinOverride_InnerJoin_SetsIsLeftJoinFalse()
        {
            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(MongoTestProduct), JoinSpecification.JoinTypeEnum.InnerJoin)
            };

            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem), overrides);
            stages[0].IsLeftJoin.Should().BeFalse();
        }

        [Fact]
        public void BuildStages_JoinOverride_LeftOuterJoin_SetsIsLeftJoinTrue()
        {
            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(MongoTestProduct), JoinSpecification.JoinTypeEnum.LeftOuterJoin)
            };

            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem), overrides);
            stages[0].IsLeftJoin.Should().BeTrue();
        }

        [Fact]
        public void BuildStages_JoinOverride_UnrelatedType_NoEffect()
        {
            // Override for MongoTestOrder should not affect the MongoTestProduct stage
            var overrides = new List<JoinOverride>
            {
                new JoinOverride(typeof(MongoTestOrder), JoinSpecification.JoinTypeEnum.InnerJoin)
            };

            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem), overrides);
            // The attribute says LeftOuterJoin; override for a different type should not change it
            stages[0].IsLeftJoin.Should().BeTrue();
        }

        // ── FromBsonDocumentWithJoins ─────────────────────────────────────────────────

        [Fact]
        public void FromBsonDocumentWithJoins_PopulatesRootFields()
        {
            var item = new MongoTestOrderItem(_conn);
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));

            var doc = new BsonDocument
            {
                { "_id",        new BsonInt32(42) },
                { "product_id", new BsonInt32(7)  },
                { "quantity",   new BsonInt32(3)  },
            };

            MongoMapper.FromBsonDocumentWithJoins(doc, item, stages);

            item.Quantity.GetValue().Should().Be(3);
            item.ProductId.GetValue().Should().Be(7);
        }

        [Fact]
        public void FromBsonDocumentWithJoins_PopulatesEmbeddedProduct()
        {
            var item = new MongoTestOrderItem(_conn);
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));

            var productDoc = new BsonDocument
            {
                { "_id",      new BsonInt32(7)     },
                { "name",     new BsonString("Widget") },
                { "price",    new BsonDecimal128(9.99m) },
                { "in_stock", new BsonBoolean(true) },
            };

            var doc = new BsonDocument
            {
                { "_id",        new BsonInt32(42) },
                { "product_id", new BsonInt32(7)  },
                { "quantity",   new BsonInt32(3)  },
                { "__joined_Product", productDoc },
            };

            MongoMapper.FromBsonDocumentWithJoins(doc, item, stages);

            item.Product.Should().NotBeNull();
            item.Product.Name.GetValue().Should().Be("Widget");
            ((decimal)item.Product.Price.GetValue()!).Should().Be(9.99m);
            item.Product.InStock.GetValue().Should().Be(true);
        }

        [Fact]
        public void FromBsonDocumentWithJoins_MissingJoinAlias_DoesNotThrow()
        {
            var item = new MongoTestOrderItem(_conn);
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));

            // No joined sub-document — simulate INNER JOIN miss
            var doc = new BsonDocument
            {
                { "_id",        new BsonInt32(42) },
                { "product_id", new BsonInt32(7)  },
                { "quantity",   new BsonInt32(3)  },
            };

            Action act = () => MongoMapper.FromBsonDocumentWithJoins(doc, item, stages);
            act.Should().NotThrow();
        }

        [Fact]
        public void FromBsonDocumentWithJoins_NullJoinValue_DoesNotThrow()
        {
            var item = new MongoTestOrderItem(_conn);
            var stages = MongoJoinBuilder.BuildStages(typeof(MongoTestOrderItem));

            var doc = new BsonDocument
            {
                { "_id",               new BsonInt32(42) },
                { "product_id",        new BsonInt32(7)  },
                { "quantity",          new BsonInt32(3)  },
                { "__joined_Product",  BsonNull.Value    },
            };

            Action act = () => MongoMapper.FromBsonDocumentWithJoins(doc, item, stages);
            act.Should().NotThrow();
        }

        [Fact]
        public void FromBsonDocumentWithJoins_EmptyStages_JustMapsRoot()
        {
            var product = new MongoTestProduct(_conn);
            var stages  = new List<MongoJoinStage>();

            var doc = new BsonDocument
            {
                { "_id",      new BsonInt32(1) },
                { "name",     new BsonString("Gadget") },
                { "price",    new BsonDecimal128(19.99m) },
                { "in_stock", new BsonBoolean(false) },
            };

            MongoMapper.FromBsonDocumentWithJoins(doc, product, stages);
            product.Name.GetValue().Should().Be("Gadget");
        }
    }

    /// <summary>
    /// Additional query translator tests covering terms added after initial implementation.
    /// </summary>
    public class MongoQueryTranslatorExtendedTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        private MongoTestProduct NewProduct() => new MongoTestProduct(_conn);

        [Fact]
        public void GreaterOrEqualTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new GreaterOrEqualTerm(p, p.Price, 5.00m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void LessOrEqualTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new LessOrEqualTerm(p, p.Price, 100.00m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void LikeTerm_ProducesNonNullFilter()
        {
            var p      = NewProduct();
            var term   = new LikeTerm(p, p.Name, "widget");
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void GreaterOrEqualAndLessOrEqual_Combined_ProducesFilter()
        {
            var p    = NewProduct();
            var term = new GreaterOrEqualTerm(p, p.Price, 5m) & new LessOrEqualTerm(p, p.Price, 50m);
            var filter = MongoQueryTranslator.Translate(term, p);
            filter.Should().NotBeNull();
        }

        [Fact]
        public void DescendingSortOrder_ReturnsNonNullSort()
        {
            var p    = NewProduct();
            var sort = new OrderDescending(p, p.Price);
            var def  = MongoQueryTranslator.TranslateSort(sort, p);
            def.Should().NotBeNull();
        }

        [Fact]
        public void CombinedSortOrder_ReturnsNonNullSort()
        {
            // CombinedSortOrder is an internal type produced by the LINQ provider's ThenBy.
            // Instantiate it via reflection so we can test TranslateSort's handling.
            var p       = NewProduct();
            var primary = new OrderAscending(p, p.Name);
            var second  = new OrderDescending(p, p.Price);

            var coreAsm  = typeof(SortOrder).Assembly;
            var combType = coreAsm.GetType("ActiveForge.Linq.CombinedSortOrder");
            combType.Should().NotBeNull("CombinedSortOrder type must exist in the core assembly");

            var combined = (SortOrder)Activator.CreateInstance(
                combType!,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new object[] { primary, second },
                null)!;

            var def = MongoQueryTranslator.TranslateSort(combined, p);
            def.Should().NotBeNull();
        }
    }
}
