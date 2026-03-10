using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Tests for <see cref="MongoUnitOfWork"/> construction and basic properties.
    /// No live MongoDB server is required.
    /// </summary>
    public class MongoUnitOfWorkTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        [Fact]
        public void Constructor_NullConnection_ThrowsArgumentNullException()
        {
            Action act = () => new MongoUnitOfWork(null);
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("connection");
        }

        [Fact]
        public void Constructor_ValidConnection_DoesNotThrow()
        {
            Action act = () => new MongoUnitOfWork(_conn);
            act.Should().NotThrow();
        }

        [Fact]
        public void Connection_Property_ReturnsProvidedConnection()
        {
            var uow = new MongoUnitOfWork(_conn);
            uow.Connection.Should().BeSameAs(_conn);
        }

        [Fact]
        public void InTransaction_Initially_False()
        {
            var uow = new MongoUnitOfWork(_conn);
            uow.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var uow = new MongoUnitOfWork(_conn);
            Action act = () => uow.Dispose();
            act.Should().NotThrow();
        }
    }
}
