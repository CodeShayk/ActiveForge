using System;
using FluentAssertions;
using MongoDB.Bson;
using ActiveForge;
using ActiveForge.MongoDB.Internal;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Tests for MongoTypeCache and MongoMapper — verifies collection name resolution,
    /// BSON name mapping, identity detection, and BSON value conversion.
    /// No server connection required.
    /// </summary>
    public class MongoTypeMapTests
    {
        // ── MongoTypeCache ────────────────────────────────────────────────────────────

        [Fact]
        public void CollectionName_DerivedFromTableAttribute()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            entry.CollectionName.Should().Be("products");
        }

        [Fact]
        public void CollectionName_OrderEntity_ReturnsOrders()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestOrder));
            entry.CollectionName.Should().Be("orders");
        }

        [Fact]
        public void IdentityField_MappedTo_id()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            entry.Identity.Should().NotBeNull();
            entry.Identity!.BsonName.Should().Be("_id");
        }

        [Fact]
        public void ColumnAttribute_SetsBsonName()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            MongoFieldDescriptor nameField = null;
            foreach (var f in entry.Fields)
            {
                if (f.FieldInfo.Name == "Name") { nameField = f; break; }
            }
            nameField.Should().NotBeNull();
            nameField!.BsonName.Should().Be("name");
        }

        [Fact]
        public void PriceField_BsonName_IsPrice()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            MongoFieldDescriptor priceField = null;
            foreach (var f in entry.Fields)
            {
                if (f.FieldInfo.Name == "Price") { priceField = f; break; }
            }
            priceField!.BsonName.Should().Be("price");
        }

        [Fact]
        public void InStockField_BsonName_IsInStock()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            MongoFieldDescriptor inStockField = null;
            foreach (var f in entry.Fields)
            {
                if (f.FieldInfo.Name == "InStock") { inStockField = f; break; }
            }
            inStockField!.BsonName.Should().Be("in_stock");
        }

        [Fact]
        public void EntityWithoutIdentity_HasNullIdentity()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestLog));
            entry.Identity.Should().BeNull();
        }

        [Fact]
        public void Fields_ContainsAllTFieldMembers()
        {
            var entry = MongoTypeCache.GetEntry(typeof(MongoTestProduct));
            // IdentityRecord adds ID + Name + Price + InStock = at least 4
            entry.Fields.Count.Should().BeGreaterThanOrEqualTo(4);
        }

        // ── MongoMapper.ClrToBson ─────────────────────────────────────────────────────

        [Fact]
        public void ClrToBson_Null_ReturnsBsonNull()
            => MongoMapper.ClrToBson(null).Should().Be(BsonNull.Value);

        [Fact]
        public void ClrToBson_Int_ReturnsBsonInt32()
        {
            BsonValue v = MongoMapper.ClrToBson(42);
            v.Should().BeOfType<BsonInt32>();
            v.AsInt32.Should().Be(42);
        }

        [Fact]
        public void ClrToBson_String_ReturnsBsonString()
        {
            BsonValue v = MongoMapper.ClrToBson("hello");
            v.Should().BeOfType<BsonString>();
            v.AsString.Should().Be("hello");
        }

        [Fact]
        public void ClrToBson_Bool_ReturnsBsonBoolean()
        {
            BsonValue v = MongoMapper.ClrToBson(true);
            v.Should().BeOfType<BsonBoolean>();
            v.AsBoolean.Should().BeTrue();
        }

        [Fact]
        public void ClrToBson_Decimal_ReturnsBsonDecimal128()
        {
            BsonValue v = MongoMapper.ClrToBson(9.99m);
            v.Should().BeOfType<BsonDecimal128>();
        }

        [Fact]
        public void ClrToBson_DateTime_ReturnsBsonDateTime()
        {
            var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            BsonValue v = MongoMapper.ClrToBson(dt);
            v.Should().BeOfType<BsonDateTime>();
        }

        [Fact]
        public void ClrToBson_Long_ReturnsBsonInt64()
        {
            BsonValue v = MongoMapper.ClrToBson(9999999999L);
            v.Should().BeOfType<BsonInt64>();
        }

        [Fact]
        public void ClrToBson_Double_ReturnsBsonDouble()
        {
            BsonValue v = MongoMapper.ClrToBson(3.14);
            v.Should().BeOfType<BsonDouble>();
        }

        [Fact]
        public void ClrToBson_Guid_ReturnsBsonString()
        {
            var g = Guid.NewGuid();
            BsonValue v = MongoMapper.ClrToBson(g);
            v.AsString.Should().Be(g.ToString());
        }

        [Fact]
        public void ClrToBson_ByteArray_ReturnsBsonBinary()
        {
            BsonValue v = MongoMapper.ClrToBson(new byte[] { 1, 2, 3 });
            v.Should().BeOfType<BsonBinaryData>();
        }

        // ── MongoMapper.ToBsonDocument ────────────────────────────────────────────────

        [Fact]
        public void ToBsonDocument_SerializesSetFields()
        {
            var conn = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var p    = new MongoTestProduct(conn);
            p.Name.SetValue("Widget");
            p.Price.SetValue(9.99m);
            p.InStock.SetValue(true);

            BsonDocument doc = MongoMapper.ToBsonDocument(p);

            doc.Contains("name").Should().BeTrue();
            doc["name"].AsString.Should().Be("Widget");
            doc.Contains("price").Should().BeTrue();
            doc.Contains("in_stock").Should().BeTrue();
            doc["in_stock"].AsBoolean.Should().BeTrue();
        }

        [Fact]
        public void ToBsonDocument_ExcludesNullFields()
        {
            var conn = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var p    = new MongoTestProduct(conn);
            p.Name.SetValue("Widget");
            // Price and InStock left null

            BsonDocument doc = MongoMapper.ToBsonDocument(p);

            doc.Contains("name").Should().BeTrue();
            doc.Contains("price").Should().BeFalse();
            doc.Contains("in_stock").Should().BeFalse();
        }

        // ── MongoMapper.FromBsonDocument ──────────────────────────────────────────────

        [Fact]
        public void FromBsonDocument_PopulatesFields()
        {
            var conn = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var p    = new MongoTestProduct(conn);

            var doc = new BsonDocument
            {
                { "_id",      new BsonInt32(7)      },
                { "name",     new BsonString("Gizmo") },
                { "price",    new BsonDecimal128(19.99m) },
                { "in_stock", new BsonBoolean(false) }
            };

            MongoMapper.FromBsonDocument(doc, p);

            ((int)p.ID.GetValue()).Should().Be(7);
            ((string)p.Name.GetValue()).Should().Be("Gizmo");
            p.InStock.IsNull().Should().BeFalse();
        }

        [Fact]
        public void FromBsonDocument_IgnoresMissingFields()
        {
            var conn = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var p    = new MongoTestProduct(conn);

            var doc = new BsonDocument { { "name", new BsonString("Widget") } };
            MongoMapper.FromBsonDocument(doc, p);

            p.Name.IsNull().Should().BeFalse();
            p.Price.IsNull().Should().BeTrue();
        }

        // ── Minimal RecordBinding ─────────────────────────────────────────────────────

        [Fact]
        public void BuildMinimalObjectBinding_SetsSourceName()
        {
            var conn    = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var product = new MongoTestProduct(conn);
            var binding = MongoMapper.BuildMinimalObjectBinding(product);

            binding.SourceName.Should().Be("products");
        }

        [Fact]
        public void BuildMinimalObjectBinding_PopulatesFields()
        {
            var conn    = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var product = new MongoTestProduct(conn);
            var binding = MongoMapper.BuildMinimalObjectBinding(product);

            binding.Fields.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public void BuildMinimalObjectBinding_IdentityFieldIsMapped()
        {
            var conn    = new MongoDataConnection("mongodb://localhost:27017", "testdb");
            var product = new MongoTestProduct(conn);
            var binding = MongoMapper.BuildMinimalObjectBinding(product);

            binding.Identity.Should().NotBeNull();
            binding.Identity!.TargetName.Should().Be("_id");
        }
    }
}
