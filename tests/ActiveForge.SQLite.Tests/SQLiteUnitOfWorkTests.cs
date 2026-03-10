using System;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.SQLite.Tests
{
    /// <summary>
    /// Unit tests for <see cref="SQLiteUnitOfWork"/>.
    /// These tests exercise constructor validation and the Connection property
    /// without opening a real database connection.
    /// </summary>
    public class SQLiteUnitOfWorkTests
    {
        [Fact]
        public void Constructor_NullConnection_ThrowsArgumentNullException()
        {
            Action act = () => new SQLiteUnitOfWork(null);
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("connection");
        }

        [Fact]
        public void Connection_Property_ReturnsSameInstanceAsPassedToConstructor()
        {
            var conn = new SQLiteConnection("Data Source=:memory:");
            using var uow = new SQLiteUnitOfWork(conn);

            uow.Connection.Should().BeSameAs(conn);
        }

        [Fact]
        public void InTransaction_Initially_ReturnsFalse()
        {
            var conn = new SQLiteConnection("Data Source=:memory:");
            using var uow = new SQLiteUnitOfWork(conn);

            uow.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithOptionalLogger_DoesNotThrow()
        {
            var conn = new SQLiteConnection("Data Source=:memory:");
            Action act = () =>
            {
                using var uow = new SQLiteUnitOfWork(conn, logger: null);
            };
            act.Should().NotThrow();
        }
    }
}
