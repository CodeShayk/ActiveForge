using System;
using FluentAssertions;
using Turquoise.ORM;
using Turquoise.ORM.Transactions;
using Xunit;

namespace Turquoise.ORM.SqlServer.Tests
{
    /// <summary>
    /// Unit tests for <see cref="SqlServerUnitOfWork"/>.
    /// These tests exercise constructor validation and the Connection property
    /// without opening a real database connection.
    /// </summary>
    public class SqlServerUnitOfWorkTests
    {
        [Fact]
        public void Constructor_NullConnection_ThrowsArgumentNullException()
        {
            Action act = () => new SqlServerUnitOfWork(null);
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("connection");
        }

        [Fact]
        public void Connection_Property_ReturnsSameInstanceAsPassedToConstructor()
        {
            var conn = new SqlServerConnection("Server=localhost;Database=Test;");
            using var uow = new SqlServerUnitOfWork(conn);

            uow.Connection.Should().BeSameAs(conn);
        }

        [Fact]
        public void InTransaction_Initially_ReturnsFalse()
        {
            var conn = new SqlServerConnection("Server=localhost;Database=Test;");
            using var uow = new SqlServerUnitOfWork(conn);

            uow.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithOptionalLogger_DoesNotThrow()
        {
            var conn = new SqlServerConnection("Server=localhost;Database=Test;");
            Action act = () =>
            {
                using var uow = new SqlServerUnitOfWork(conn, logger: null);
            };
            act.Should().NotThrow();
        }
    }
}
