using FluentAssertions;
using Turquoise.ORM;
using Turquoise.ORM.Attributes;
using Xunit;

namespace Turquoise.ORM.Tests
{
    // Minimal DataObject for testing collection operations
    [Table("TestItem")]
    internal class TestItem : IdentDataObject { }

    public class ObjectCollectionTests
    {
        // ── Default state ─────────────────────────────────────────────────────────────

        [Fact]
        public void Default_IsEmpty()
        {
            var col = new ObjectCollection();
            col.Count.Should().Be(0);
        }

        [Fact]
        public void Default_Pagination_PropertiesAreZero()
        {
            var col = new ObjectCollection();
            col.StartRecord.Should().Be(0);
            col.PageSize.Should().Be(0);
            col.TotalRowCount.Should().Be(0);
        }

        [Fact]
        public void Default_Flags_AreFalse()
        {
            var col = new ObjectCollection();
            col.IsMoreData.Should().BeFalse();
            col.TotalRowCountValid.Should().BeFalse();
        }

        // ── Property setters ──────────────────────────────────────────────────────────

        [Fact]
        public void StartRecord_CanBeSet()
        {
            var col = new ObjectCollection { StartRecord = 20 };
            col.StartRecord.Should().Be(20);
        }

        [Fact]
        public void PageSize_CanBeSet()
        {
            var col = new ObjectCollection { PageSize = 10 };
            col.PageSize.Should().Be(10);
        }

        [Fact]
        public void IsMoreData_CanBeSetTrue()
        {
            var col = new ObjectCollection { IsMoreData = true };
            col.IsMoreData.Should().BeTrue();
        }

        [Fact]
        public void TotalRowCount_CanBeSet()
        {
            var col = new ObjectCollection { TotalRowCount = 500 };
            col.TotalRowCount.Should().Be(500);
        }

        [Fact]
        public void TotalRowCountValid_CanBeSetTrue()
        {
            var col = new ObjectCollection { TotalRowCountValid = true };
            col.TotalRowCountValid.Should().BeTrue();
        }

        // ── AddTail ───────────────────────────────────────────────────────────────────

        [Fact]
        public void AddTail_AppendsToEnd()
        {
            var col = new ObjectCollection();
            var a = new TestItem();
            var b = new TestItem();
            col.AddTail(a);
            col.AddTail(b);
            col.Count.Should().Be(2);
            col[0].Should().BeSameAs(a);
            col[1].Should().BeSameAs(b);
        }

        // ── AddHead ───────────────────────────────────────────────────────────────────

        [Fact]
        public void AddHead_PrependToFront()
        {
            var col = new ObjectCollection();
            var a = new TestItem();
            var b = new TestItem();
            col.AddTail(a);
            col.AddHead(b);
            col.Count.Should().Be(2);
            col[0].Should().BeSameAs(b);
            col[1].Should().BeSameAs(a);
        }

        // ── Add(ObjectCollection) ─────────────────────────────────────────────────────

        [Fact]
        public void Add_OtherCollection_MergesContents()
        {
            var col1 = new ObjectCollection();
            var col2 = new ObjectCollection();
            var a = new TestItem();
            var b = new TestItem();
            col1.AddTail(a);
            col2.AddTail(b);
            col1.Add(col2);
            col1.Count.Should().Be(2);
            col1[0].Should().BeSameAs(a);
            col1[1].Should().BeSameAs(b);
        }

        [Fact]
        public void Add_NullCollection_IsNoOp()
        {
            var col = new ObjectCollection();
            col.AddTail(new TestItem());
            col.Add((ObjectCollection)null);
            col.Count.Should().Be(1);
        }

        [Fact]
        public void Add_EmptyCollection_IsNoOp()
        {
            var col = new ObjectCollection();
            col.AddTail(new TestItem());
            col.Add(new ObjectCollection());
            col.Count.Should().Be(1);
        }

        // ── Generic ObjectCollection<T> ───────────────────────────────────────────────

        [Fact]
        public void Generic_GetEnumerator_CastsElements()
        {
            var col = new ObjectCollection<TestItem>();
            var a = new TestItem();
            var b = new TestItem();
            col.AddTail(a);
            col.AddTail(b);

            int count = 0;
            foreach (TestItem item in col)
            {
                item.Should().NotBeNull();
                count++;
            }
            count.Should().Be(2);
        }

        [Fact]
        public void Generic_PreservesBaseProperties()
        {
            var col = new ObjectCollection<TestItem>
            {
                StartRecord = 10,
                PageSize    = 20,
                IsMoreData  = true
            };
            col.StartRecord.Should().Be(10);
            col.PageSize.Should().Be(20);
            col.IsMoreData.Should().BeTrue();
        }
    }
}
