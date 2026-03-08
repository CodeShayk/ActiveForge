using System;
using FluentAssertions;
using Turquoise.ORM;
using Xunit;

namespace Turquoise.ORM.SqlServer.Tests
{
    /// <summary>
    /// Tests for <see cref="SqlServerConnection.MapNativeType"/>.
    /// No database connection is required.
    /// </summary>
    public class SqlServerConnectionTypeMapTests
    {
        private readonly SqlServerConnection _conn =
            new SqlServerConnection("Server=localhost;Database=Test;");

        [Theory]
        [InlineData("binary",   typeof(byte[]))]
        [InlineData("image",    typeof(byte[]))]
        [InlineData("timestamp",typeof(byte[]))]
        [InlineData("varbinary",typeof(byte[]))]
        public void MapNativeType_BinaryTypes_ReturnsByteArray(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("char",    typeof(string))]
        [InlineData("nchar",   typeof(string))]
        [InlineData("ntext",   typeof(string))]
        [InlineData("nvarchar",typeof(string))]
        [InlineData("text",    typeof(string))]
        [InlineData("varchar", typeof(string))]
        public void MapNativeType_StringTypes_ReturnsString(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("datetime",      typeof(DateTime))]
        [InlineData("smalldatetime", typeof(DateTime))]
        public void MapNativeType_DateTypes_ReturnsDateTime(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Theory]
        [InlineData("bigint",     typeof(decimal))]
        [InlineData("decimal",    typeof(decimal))]
        [InlineData("float",      typeof(decimal))]
        [InlineData("int",        typeof(decimal))]
        [InlineData("money",      typeof(decimal))]
        [InlineData("numeric",    typeof(decimal))]
        [InlineData("real",       typeof(decimal))]
        [InlineData("smallint",   typeof(decimal))]
        [InlineData("smallmoney", typeof(decimal))]
        [InlineData("tinyint",    typeof(decimal))]
        public void MapNativeType_NumericTypes_ReturnsDecimal(string sqlType, Type expected)
            => _conn.MapNativeType(sqlType).Should().Be(expected);

        [Fact]
        public void MapNativeType_Bit_ReturnsBool()
            => _conn.MapNativeType("bit").Should().Be(typeof(bool));

        [Fact]
        public void MapNativeType_UniqueIdentifier_ReturnsGuid()
            => _conn.MapNativeType("uniqueidentifier").Should().Be(typeof(Guid));

        [Fact]
        public void MapNativeType_UnknownType_ReturnsNull()
            => _conn.MapNativeType("xml").Should().BeNull();

        [Fact]
        public void MapNativeType_NullInput_ReturnsNull()
            => _conn.MapNativeType(null).Should().BeNull();

        [Fact]
        public void MapNativeType_IsCaseInsensitive()
        {
            _conn.MapNativeType("NVARCHAR").Should().Be(typeof(string));
            _conn.MapNativeType("INT").Should().Be(typeof(decimal));
            _conn.MapNativeType("BIT").Should().Be(typeof(bool));
        }
    }
}
