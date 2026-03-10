using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    /// <summary>
    /// Tests for <see cref="PostgreSQLConnection.MapNativeType"/>.
    /// No database connection is required.
    /// </summary>
    public class PostgreSQLConnectionTypeMapTests
    {
        private readonly PostgreSQLConnection _conn =
            new PostgreSQLConnection("Host=localhost;Database=test;");

        [Fact]
        public void MapNativeType_Bytea_ReturnsByteArray()
            => _conn.MapNativeType("bytea").Should().Be(typeof(byte[]));

        [Theory]
        [InlineData("character")]
        [InlineData("character varying")]
        [InlineData("text")]
        [InlineData("name")]
        [InlineData("citext")]
        public void MapNativeType_StringTypes_ReturnsString(string sqlType)
            => _conn.MapNativeType(sqlType).Should().Be(typeof(string));

        [Theory]
        [InlineData("timestamp without time zone")]
        [InlineData("timestamp with time zone")]
        [InlineData("timestamp")]
        [InlineData("date")]
        public void MapNativeType_DateTimeTypes_ReturnsDateTime(string sqlType)
            => _conn.MapNativeType(sqlType).Should().Be(typeof(DateTime));

        [Theory]
        [InlineData("time without time zone")]
        [InlineData("time with time zone")]
        [InlineData("time")]
        [InlineData("interval")]
        public void MapNativeType_TimeTypes_ReturnsTimeSpan(string sqlType)
            => _conn.MapNativeType(sqlType).Should().Be(typeof(TimeSpan));

        [Theory]
        [InlineData("integer")]
        [InlineData("bigint")]
        [InlineData("smallint")]
        [InlineData("numeric")]
        [InlineData("decimal")]
        [InlineData("real")]
        [InlineData("double precision")]
        [InlineData("money")]
        [InlineData("oid")]
        public void MapNativeType_NumericTypes_ReturnsDecimal(string sqlType)
            => _conn.MapNativeType(sqlType).Should().Be(typeof(decimal));

        [Fact]
        public void MapNativeType_Boolean_ReturnsBool()
            => _conn.MapNativeType("boolean").Should().Be(typeof(bool));

        [Fact]
        public void MapNativeType_Uuid_ReturnsGuid()
            => _conn.MapNativeType("uuid").Should().Be(typeof(Guid));

        [Fact]
        public void MapNativeType_UnknownType_ReturnsNull()
            => _conn.MapNativeType("xml").Should().BeNull();

        [Fact]
        public void MapNativeType_NullInput_ReturnsNull()
            => _conn.MapNativeType(null).Should().BeNull();

        [Fact]
        public void MapNativeType_IsCaseInsensitive()
        {
            _conn.MapNativeType("CHARACTER VARYING").Should().Be(typeof(string));
            _conn.MapNativeType("INTEGER").Should().Be(typeof(decimal));
            _conn.MapNativeType("BOOLEAN").Should().Be(typeof(bool));
            _conn.MapNativeType("TIMESTAMP WITHOUT TIME ZONE").Should().Be(typeof(DateTime));
        }
    }
}
