using System;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    /// <summary>
    /// Unit tests for <see cref="PostgreSQLUnitOfWork"/>.
    /// These tests exercise constructor validation and the Connection property
    /// without opening a real database connection.
    /// </summary>
    public class PostgreSQLUnitOfWorkTests
    {
        [Fact]
        public void Constructor_NullConnection_ThrowsArgumentNullException()
        {
            Action act = () => new PostgreSQLUnitOfWork(null);
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("connection");
        }

        [Fact]
        public void Connection_Property_ReturnsSameInstanceAsPassedToConstructor()
        {
            var conn = new PostgreSQLConnection("Host=localhost;Database=test;");
            using var uow = new PostgreSQLUnitOfWork(conn);

            uow.Connection.Should().BeSameAs(conn);
        }

        [Fact]
        public void InTransaction_Initially_ReturnsFalse()
        {
            var conn = new PostgreSQLConnection("Host=localhost;Database=test;");
            using var uow = new PostgreSQLUnitOfWork(conn);

            uow.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithOptionalLogger_DoesNotThrow()
        {
            var conn = new PostgreSQLConnection("Host=localhost;Database=test;");
            Action act = () =>
            {
                using var uow = new PostgreSQLUnitOfWork(conn, logger: null);
            };
            act.Should().NotThrow();
        }
    }
}
