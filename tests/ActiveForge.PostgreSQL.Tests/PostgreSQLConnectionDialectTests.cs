using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    /// <summary>
    /// Tests for the PostgreSQL dialect methods on <see cref="PostgreSQLConnection"/>.
    /// No real database connection is opened — these methods return constant strings
    /// or perform local string operations only.
    /// </summary>
    public class PostgreSQLConnectionDialectTests
    {
        // Instantiate with a dummy DSN; Connect() is never called.
        private readonly PostgreSQLConnection _conn =
            new PostgreSQLConnection("Host=localhost;Database=test;");

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
            => _conn.GetUpdateLock().Should().BeEmpty();

        [Fact]
        public void IsAutoIdentity_ReturnsTrue()
            => _conn.IsAutoIdentity().Should().BeTrue();

        [Fact]
        public void GetStringConnectionOperator_ReturnsPipePipe()
            => _conn.GetStringConnectionOperator().Should().Be("||");

        [Fact]
        public void GetGeneratorOperator_ReturnsEmptyString()
            => _conn.GetGeneratorOperator(null).Should().Be("");

        [Fact]
        public void LimitRowCount_ReturnsStubWithoutLimit()
            => _conn.LimitRowCount(10, "name, price FROM products WHERE in_stock = TRUE")
                    .Should().Be("SELECT name, price FROM products WHERE in_stock = TRUE");

        [Fact]
        public void LimitRowCount_ZeroRows_IsValid()
            => _conn.LimitRowCount(0, "id FROM products")
                    .Should().Be("SELECT id FROM products");

        [Fact]
        public void CreateConcatenateOperator_JoinsPartsWithPipePipe()
            => _conn.CreateConcatenateOperator("a", "b", "c")
                    .Should().Be("a||b||c");

        [Fact]
        public void CreateConcatenateOperator_SinglePart_ReturnsPart()
            => _conn.CreateConcatenateOperator("x")
                    .Should().Be("x");

        [Fact]
        public void QuoteName_WrapsInDoubleQuotes()
            => _conn.QuoteName("products")
                    .Should().Be("\"products\"");

        [Fact]
        public void QuoteName_PreservesCase()
            => _conn.QuoteName("MyTable")
                    .Should().Be("\"MyTable\"");

        [Fact]
        public void PreInsertIdentityCommand_ReturnsEmpty()
            => _conn.PreInsertIdentityCommand("products").Should().BeEmpty();

        [Fact]
        public void PostInsertIdentityCommand_ReturnsEmpty()
            => _conn.PostInsertIdentityCommand("products").Should().BeEmpty();
    }
}
