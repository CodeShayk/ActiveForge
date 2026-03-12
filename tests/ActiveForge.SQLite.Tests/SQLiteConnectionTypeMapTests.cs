using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.SQLite.Tests
{
    /// <summary>
    /// Tests for <see cref="SQLiteConnection.MapNativeType"/>.
    /// No database connection is required.
    /// </summary>
    public class SQLiteConnectionTypeMapTests
    {
        private readonly SQLiteConnection _conn =
            new SQLiteConnection("Data Source=:memory:");

        [Theory]
        [InlineData("int", typeof(long))]
        [InlineData("integer", typeof(long))]
        [InlineData("tinyint", typeof(long))]
        [InlineData("smallint", typeof(long))]
        [InlineData("bigint", typeof(long))]
        public void MapNativeType_IntTypes_ReturnsLong(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("real", typeof(double))]
        [InlineData("float", typeof(double))]
        [InlineData("double", typeof(double))]
        public void MapNativeType_FloatingPointTypes_ReturnsDouble(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("blob", typeof(byte[]))]
        [InlineData("", typeof(byte[]))]
        public void MapNativeType_BlobTypes_ReturnsByteArray(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("bool", typeof(bool))]
        [InlineData("boolean", typeof(bool))]
        public void MapNativeType_BoolTypes_ReturnsBool(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("date", typeof(DateTime))]
        [InlineData("datetime", typeof(DateTime))]
        [InlineData("timestamp", typeof(DateTime))] // TIME triggers DateTime matching
        public void MapNativeType_DateTypes_ReturnsDateTime(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("guid", typeof(Guid))]
        [InlineData("uuid", typeof(Guid))]
        public void MapNativeType_GuidTypes_ReturnsGuid(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("numeric", typeof(decimal))]
        [InlineData("decimal", typeof(decimal))]
        [InlineData("money", typeof(decimal))]
        public void MapNativeType_DecimalTypes_ReturnsDecimal(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("text", typeof(string))]
        [InlineData("varchar", typeof(string))]
        [InlineData("nvarchar", typeof(string))]
        [InlineData("char", typeof(string))]
        [InlineData("unknown_type", typeof(string))]
        public void MapNativeType_TextAndUnknownTypes_ReturnsString(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Fact]
        public void MapNativeType_NullInput_ReturnsByteArray() // SQLite defaults untyped/null to BLOB/byte[]
            => _conn.MapNativeType(null).Should().Be(typeof(byte[]));

        [Fact]
        public void MapNativeType_IsCaseInsensitive()
        {
            _conn.MapNativeType("INTEGER").Should().Be(typeof(long));
            _conn.MapNativeType("REAL").Should().Be(typeof(double));
            _conn.MapNativeType("DATETIME").Should().Be(typeof(DateTime));
        }
    }
}
