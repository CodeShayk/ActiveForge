using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    /// <summary>
    /// Tests for the SQL Server dialect methods on <see cref="SqlServerConnection"/>.
    /// These tests do not open a real database connection — they only exercise
    /// pure dialect methods that return constant strings or perform local arithmetic.
    /// </summary>
    public class SqlServerConnectionDialectTests
    {
        // Instantiate with a dummy connection string; no Connect() is called.
        private readonly SqlServerConnection _conn =
            new SqlServerConnection("Server=localhost;Database=Test;");

        [Fact]
        public void GetParameterMark_ReturnsAt()
            => _conn.GetParameterMark().Should().Be("@");

        [Fact]
        public void GetLeftNameQuote_ReturnsOpenBracket()
            => _conn.GetLeftNameQuote().Should().Be("[");

        [Fact]
        public void GetRightNameQuote_ReturnsCloseBracket()
            => _conn.GetRightNameQuote().Should().Be("]");

        [Fact]
        public void GetSourceNameSeparator_ReturnsDot()
            => _conn.GetSourceNameSeparator().Should().Be(".");

        [Fact]
        public void GetUpdateLock_ReturnsUpdlock()
            => _conn.GetUpdateLock().Should().Be("WITH (UPDLOCK)");

        [Fact]
        public void IsAutoIdentity_ReturnsTrue()
            => _conn.IsAutoIdentity().Should().BeTrue();

        [Fact]
        public void GetStringConnectionOperator_ReturnsPlus()
            => _conn.GetStringConnectionOperator().Should().Be("+");

        [Fact]
        public void LimitRowCount_GeneratesSelectTop()
            => _conn.LimitRowCount(10, "Name FROM Products")
                    .Should().Be("SELECT TOP 10 Name FROM Products");

        [Fact]
        public void PreInsertIdentityCommand_GeneratesSetIdentityInsertOn()
            => _conn.PreInsertIdentityCommand("Products")
                    .Should().Be("SET IDENTITY_INSERT [Products] ON");

        [Fact]
        public void PostInsertIdentityCommand_GeneratesSetIdentityInsertOff()
            => _conn.PostInsertIdentityCommand("Products")
                    .Should().Be("SET IDENTITY_INSERT [Products] OFF");

        [Fact]
        public void CreateConcatenateOperator_JoinsPartsWithPlus()
            => _conn.CreateConcatenateOperator("a", "b", "c")
                    .Should().Be("a+b+c");

        [Fact]
        public void GetGeneratorOperator_ReturnsEmptyString()
            => _conn.GetGeneratorOperator(null).Should().Be("");
    }
}
