using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.SQLite.Tests
{
    /// <summary>
    /// Tests for the SQLite dialect methods on <see cref="SQLiteConnection"/>.
    /// These tests do not open a real database connection — they only exercise
    /// pure dialect methods that return constant strings or perform local arithmetic.
    /// </summary>
    public class SQLiteConnectionDialectTests
    {
        // Instantiate with a dummy connection string; no Connect() is called.
        private readonly SQLiteConnection _conn =
            new SQLiteConnection("Data Source=:memory:");

        [Fact]
        public void GetParameterMark_ReturnsAt()
            => _conn.GetParameterMark().Should().Be("@");

        [Fact]
        public void GetLeftNameQuote_ReturnsDoubleQuote()
            => _conn.GetLeftNameQuote().Should().Be("\"");

        [Fact]
        public void GetRightNameQuote_ReturnsDoubleQuote()
            => _conn.GetRightNameQuote().Should().Be("\"");

        [Fact]
        public void GetSourceNameSeparator_ReturnsDot()
            => _conn.GetSourceNameSeparator().Should().Be(".");

        [Fact]
        public void GetUpdateLock_ReturnsEmptyString()
            => _conn.GetUpdateLock().Should().Be("");

        [Fact]
        public void IsAutoIdentity_ReturnsTrue()
            => _conn.IsAutoIdentity().Should().BeTrue();

        [Fact]
        public void GetStringConnectionOperator_ReturnsPipesPipes()
            => _conn.GetStringConnectionOperator().Should().Be("||");

        [Fact]
        public void LimitRowCount_StripsLimitFromStub()
            // SQLite uses LIMIT/OFFSET as a suffix after WHERE and ORDER BY (via
            // GetPageSuffix), so LimitRowCount must not embed LIMIT in the stub.
            => _conn.LimitRowCount(10, "Name FROM Products")
                    .Should().Be("SELECT Name FROM Products");

        [Fact]
        public void PreInsertIdentityCommand_ReturnsEmptyString()
            => _conn.PreInsertIdentityCommand("Products").Should().Be("");

        [Fact]
        public void PostInsertIdentityCommand_ReturnsEmptyString()
            => _conn.PostInsertIdentityCommand("Products").Should().Be("");

        [Fact]
        public void CreateConcatenateOperator_JoinsPartsWithPipesPipes()
            => _conn.CreateConcatenateOperator("a", "b", "c")
                    .Should().Be("a||b||c");

        [Fact]
        public void GetGeneratorOperator_ReturnsEmptyString()
            => _conn.GetGeneratorOperator(null).Should().Be("");

        [Fact]
        public void MapNativeType_Integer_ReturnsLong()
            => _conn.MapNativeType("INTEGER").Should().Be(typeof(long));

        [Fact]
        public void MapNativeType_Text_ReturnsString()
            => _conn.MapNativeType("TEXT").Should().Be(typeof(string));

        [Fact]
        public void MapNativeType_Real_ReturnsDouble()
            => _conn.MapNativeType("REAL").Should().Be(typeof(double));

        [Fact]
        public void MapNativeType_Blob_ReturnsByteArray()
            => _conn.MapNativeType("BLOB").Should().Be(typeof(byte[]));

        [Fact]
        public void MapNativeType_Varchar_ReturnsString()
            => _conn.MapNativeType("VARCHAR(255)").Should().Be(typeof(string));

        [Fact]
        public void MapNativeType_Numeric_ReturnsDecimal()
            => _conn.MapNativeType("NUMERIC").Should().Be(typeof(decimal));

        [Fact]
        public void MapNativeType_Boolean_ReturnsBool()
            => _conn.MapNativeType("BOOLEAN").Should().Be(typeof(bool));

        [Fact]
        public void MapNativeType_Datetime_ReturnsDateTime()
            => _conn.MapNativeType("DATETIME").Should().Be(typeof(DateTime));
    }
}
