using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Tests for dialect-helper methods on <see cref="MongoDataConnection"/>.
    /// No real MongoDB server is required — these methods return constants only.
    /// </summary>
    public class MongoDataConnectionDialectTests
    {
        // Instantiate without connecting — dialect methods are pure.
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        [Fact]
        public void GetParameterMark_ReturnsEmptyString()
            => _conn.GetParameterMark().Should().BeEmpty();

        [Fact]
        public void GetLeftNameQuote_ReturnsEmptyString()
            => _conn.GetLeftNameQuote().Should().BeEmpty();

        [Fact]
        public void GetRightNameQuote_ReturnsEmptyString()
            => _conn.GetRightNameQuote().Should().BeEmpty();

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
        public void GetStringConnectionOperator_ReturnsEmptyString()
            => _conn.GetStringConnectionOperator().Should().BeEmpty();

        [Fact]
        public void GetGeneratorOperator_ReturnsEmptyString()
            => _conn.GetGeneratorOperator(null).Should().BeEmpty();

        [Fact]
        public void PreInsertIdentityCommand_ReturnsEmptyString()
            => _conn.PreInsertIdentityCommand("any").Should().BeEmpty();

        [Fact]
        public void PostInsertIdentityCommand_ReturnsEmptyString()
            => _conn.PostInsertIdentityCommand("any").Should().BeEmpty();

        [Fact]
        public void QuoteName_ReturnsNameUnchanged()
            => _conn.QuoteName("myField").Should().Be("myField");

        [Fact]
        public void CreateConcatenateOperator_ConcatenateParts()
            => _conn.CreateConcatenateOperator("a", "b", "c").Should().Be("abc");

        [Fact]
        public void GetTimeout_Returns30()
            => _conn.GetTimeout().Should().Be(30);

        [Fact]
        public void MapType_WithNoFactory_ReturnsSameType()
            => _conn.MapType(typeof(MongoTestProduct)).Should().Be(typeof(MongoTestProduct));

        [Fact]
        public void GetValidationMessage_ReturnsDefault()
            => _conn.GetValidationMessage("missing.key", "fallback").Should().Be("fallback");
    }
}
