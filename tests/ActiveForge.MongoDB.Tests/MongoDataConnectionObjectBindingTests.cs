using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Tests that <see cref="MongoDataConnection.GetObjectBinding"/> returns a usable
    /// minimal ObjectBinding populated with field descriptors from the DataObject.
    /// No live server required.
    /// </summary>
    public class MongoDataConnectionObjectBindingTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        [Fact]
        public void GetObjectBinding_ReturnsNonNull()
        {
            var product = new MongoTestProduct(_conn);
            var binding = _conn.GetObjectBinding(product, true, false);
            binding.Should().NotBeNull();
        }

        [Fact]
        public void GetObjectBinding_SourceName_MatchesTableAttribute()
        {
            var product = new MongoTestProduct(_conn);
            var binding = _conn.GetObjectBinding(product, true, false);
            binding.SourceName.Should().Be("products");
        }

        [Fact]
        public void GetObjectBinding_Fields_NotEmpty()
        {
            var product = new MongoTestProduct(_conn);
            var binding = _conn.GetObjectBinding(product, true, false);
            binding.Fields.Should().NotBeEmpty();
        }

        [Fact]
        public void GetObjectBinding_Identity_IsSet()
        {
            var product = new MongoTestProduct(_conn);
            var binding = _conn.GetObjectBinding(product, true, false);
            binding.Identity.Should().NotBeNull();
            binding.Identity!.TargetName.Should().Be("_id");
        }

        [Fact]
        public void GetObjectBinding_WithExpectedTypes_ReturnsBinding()
        {
            var product = new MongoTestProduct(_conn);
            var binding = _conn.GetObjectBinding(product, true, false, null, false);
            binding.Should().NotBeNull();
        }

        [Fact]
        public void DefaultFieldSubset_ReturnsNonNull()
        {
            var product = new MongoTestProduct(_conn);
            var subset  = _conn.DefaultFieldSubset(product);
            subset.Should().NotBeNull();
        }

        [Fact]
        public void FieldSubset_InitialState_ReturnsNonNull()
        {
            var product = new MongoTestProduct(_conn);
            var subset  = _conn.FieldSubset(product, FieldSubset.InitialState.IncludeAll);
            subset.Should().NotBeNull();
        }
    }
}
