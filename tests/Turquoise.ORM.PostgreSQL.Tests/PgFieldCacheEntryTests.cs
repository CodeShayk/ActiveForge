using FluentAssertions;
using Turquoise.ORM;
using Xunit;

namespace Turquoise.ORM.PostgreSQL.Tests
{
    /// <summary>
    /// Tests for <see cref="PostgreSQLConnection.PgFieldCacheEntry"/>.
    /// </summary>
    public class PgFieldCacheEntryTests
    {
        [Fact]
        public void Properties_AreStoredCorrectly()
        {
            var entry = new PostgreSQLConnection.PgFieldCacheEntry(
                "products", "name", "character varying",
                nullable: false, isSerial: false, length: 200, precision: 0, scale: 0);

            entry.TableName.Should().Be("products");
            entry.ColumnName.Should().Be("name");
            entry.NativeType.Should().Be("character varying");
            entry.IsNullable.Should().BeFalse();
            entry.IsSerial.Should().BeFalse();
            entry.Length.Should().Be(200);
            entry.Precision.Should().Be(0);
            entry.Scale.Should().Be(0);
        }

        [Fact]
        public void IsSerial_True_WhenSerialColumn()
        {
            var entry = new PostgreSQLConnection.PgFieldCacheEntry(
                "products", "id", "integer",
                nullable: false, isSerial: true, length: 0, precision: 0, scale: 0);

            entry.IsSerial.Should().BeTrue();
        }

        [Fact]
        public void NumericColumn_PrecisionAndScale_StoredCorrectly()
        {
            var entry = new PostgreSQLConnection.PgFieldCacheEntry(
                "orders", "total", "numeric",
                nullable: true, isSerial: false, length: 0, precision: 18, scale: 4);

            entry.Precision.Should().Be(18);
            entry.Scale.Should().Be(4);
            entry.IsNullable.Should().BeTrue();
        }

        [Fact]
        public void TextColumn_LengthZero_ForUnboundedText()
        {
            // PostgreSQL TEXT has no length limit; information_schema returns NULL → parsed as 0
            var entry = new PostgreSQLConnection.PgFieldCacheEntry(
                "articles", "body", "text",
                nullable: true, isSerial: false, length: 0, precision: 0, scale: 0);

            entry.Length.Should().Be(0);
            entry.NativeType.Should().Be("text");
        }
    }
}
