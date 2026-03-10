using FluentAssertions;
using ActiveForge;
using ActiveForge.Attributes;
using Xunit;

namespace ActiveForge.Tests
{
    // ── Test domain model ─────────────────────────────────────────────────────────────

    [Table("Product")]
    internal class TestProduct : IdentDataObject
    {
        [Column("Name")]
        public TString Name = new TString();

        [Column("Price")]
        public TDecimal Price = new TDecimal();

        [Column("Stock")]
        public TInt Stock = new TInt();

        [Column("InStock")]
        public TBool InStock = new TBool();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────────

    public class DataObjectTests
    {
        // ── Construction & TField creation ────────────────────────────────────────────

        [Fact]
        public void New_TFields_AreCreated_NotNull()
        {
            var p = new TestProduct();
            p.Name.Should().NotBeNull();
            p.Price.Should().NotBeNull();
            p.Stock.Should().NotBeNull();
            p.ID.Should().NotBeNull();
        }

        [Fact]
        public void New_TFields_StartAsNull()
        {
            var p = new TestProduct();
            p.Name.IsNull().Should().BeTrue();
            p.Price.IsNull().Should().BeTrue();
            p.Stock.IsNull().Should().BeTrue();
            p.ID.IsNull().Should().BeTrue();
        }

        // ── GetUniqueIdentifier ───────────────────────────────────────────────────────

        [Fact]
        public void GetUniqueIdentifier_IsNonEmpty()
        {
            var p = new TestProduct();
            p.GetUniqueIdentifier().Should().NotBe(System.Guid.Empty);
        }

        [Fact]
        public void GetUniqueIdentifier_DifferentPerInstance()
        {
            var p1 = new TestProduct();
            var p2 = new TestProduct();
            p1.GetUniqueIdentifier().Should().NotBe(p2.GetUniqueIdentifier());
        }

        [Fact]
        public void GetUniqueIdentifier_StablePerInstance()
        {
            var p = new TestProduct();
            var id1 = p.GetUniqueIdentifier();
            var id2 = p.GetUniqueIdentifier();
            id1.Should().Be(id2);
        }

        // ── IsNull(string) ────────────────────────────────────────────────────────────

        [Fact]
        public void IsNull_FreshField_IsTrue()
        {
            var p = new TestProduct();
            p.IsNull("Name").Should().BeTrue();
            p.IsNull("Price").Should().BeTrue();
            p.IsNull("Stock").Should().BeTrue();
        }

        [Fact]
        public void IsNull_AfterSetValue_IsFalse()
        {
            var p = new TestProduct();
            p.Name.SetValue("Widget");
            p.IsNull("Name").Should().BeFalse();
        }

        [Fact]
        public void IsNull_AfterSetNull_IsTrue()
        {
            var p = new TestProduct();
            p.Name.SetValue("Widget");
            p.Name.SetNull(true);
            p.IsNull("Name").Should().BeTrue();
        }

        [Fact]
        public void IsNull_UnknownField_IsTrue()
        {
            var p = new TestProduct();
            p.IsNull("NonExistentField").Should().BeTrue();
        }

        [Fact]
        public void IsNull_CaseInsensitive()
        {
            var p = new TestProduct();
            p.IsNull("name").Should().BeTrue();   // lowercase
            p.IsNull("NAME").Should().BeTrue();   // uppercase
        }

        // ── GetDifferences ────────────────────────────────────────────────────────────

        [Fact]
        public void GetDifferences_SameObject_ReturnsNonNull()
        {
            var p = new TestProduct();
            var diff = p.GetDifferences(p);
            diff.Should().NotBeNull();
        }

        [Fact]
        public void GetDifferences_DifferentType_ThrowsPersistenceException()
        {
            var p = new TestProduct();
            var other = new TestItem();
            System.Action act = () => p.GetDifferences(other);
            act.Should().Throw<PersistenceException>();
        }

        [Fact]
        public void GetDifferences_EqualObjects_DoesNotThrow()
        {
            var p1 = new TestProduct();
            var p2 = new TestProduct();
            System.Action act = () => p1.GetDifferences(p2);
            act.Should().NotThrow();
        }

        // ── DataObjectMetaDataCache ───────────────────────────────────────────────────

        [Fact]
        public void MetaDataCache_TFields_IncludesAllDeclaredFields()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            // TestProduct declares Name, Price, Stock, InStock + inherits ID from IdentDataObject
            meta.TFields.Count.Should().BeGreaterThanOrEqualTo(5);
        }

        [Fact]
        public void MetaDataCache_TFields_ContainsNameField()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            meta.TFields.Should().Contain(e => e.Name == "Name");
        }

        [Fact]
        public void MetaDataCache_TFields_ContainsPriceField()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            meta.TFields.Should().Contain(e => e.Name == "Price");
        }

        [Fact]
        public void MetaDataCache_TFields_ContainsIDField()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            meta.TFields.Should().Contain(e => e.Name == "ID");
        }

        [Fact]
        public void MetaDataCache_SourceName_UsesTableAttribute()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            meta.SourceName.Should().Be("Product");
        }

        [Fact]
        public void MetaDataCache_ClassName_IsTypeName()
        {
            var meta = DataObjectMetaDataCache.GetTypeMetaData(typeof(TestProduct));
            meta.ClassName.Should().Be("TestProduct");
        }

        // ── GetFieldInfo ──────────────────────────────────────────────────────────────

        [Fact]
        public void GetFieldInfo_ExistingField_ReturnsNonNull()
        {
            var p = new TestProduct();
            var fi = p.GetFieldInfo(p.Name);
            fi.Should().NotBeNull();
            fi.Name.Should().Be("Name");
        }

        [Fact]
        public void GetFieldInfo_IDField_ReturnsNonNull()
        {
            var p = new TestProduct();
            var fi = p.GetFieldInfo(p.ID);
            fi.Should().NotBeNull();
            fi.Name.Should().Be("ID");
        }
    }
}
