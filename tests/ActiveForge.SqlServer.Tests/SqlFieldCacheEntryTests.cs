using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    /// <summary>
    /// Tests for <see cref="SqlServerConnection.SqlFieldCacheEntry"/>,
    /// specifically the nvarchar/nchar character-length halving logic.
    /// </summary>
    public class SqlFieldCacheEntryTests
    {
        [Theory]
        [InlineData("nvarchar", 200, 100)]   // byte length 200 → char length 100
        [InlineData("nchar",    10,  5)]     // byte length 10  → char length 5
        [InlineData("varchar",  100, 100)]   // single-byte: no change
        [InlineData("char",     50,  50)]    // single-byte: no change
        [InlineData("nvarchar", -1,  -1)]    // MAX (-1): no change
        public void Length_NvarcharAndNchar_HalvesByteLength(
            string nativeType, int rawLength, int expectedLength)
        {
            var entry = new SqlServerConnection.SqlFieldCacheEntry(
                "Table", "Column", nativeType, true, rawLength, 0, 0);

            entry.Length.Should().Be(expectedLength);
        }

        [Fact]
        public void Properties_AreStoredCorrectly()
        {
            var entry = new SqlServerConnection.SqlFieldCacheEntry(
                "Products", "Name", "nvarchar", false, 400, 0, 0);

            entry.TableName.Should().Be("Products");
            entry.ColumnName.Should().Be("Name");
            entry.NativeType.Should().Be("nvarchar");
            entry.IsNullable.Should().BeFalse();
            entry.Length.Should().Be(200);  // 400 / 2
            entry.Precision.Should().Be(0);
            entry.Scale.Should().Be(0);
        }

        [Fact]
        public void Precision_And_Scale_StoredCorrectly()
        {
            var entry = new SqlServerConnection.SqlFieldCacheEntry(
                "Orders", "Total", "decimal", true, 0, 18, 4);

            entry.Precision.Should().Be(18);
            entry.Scale.Should().Be(4);
        }
    }
}
