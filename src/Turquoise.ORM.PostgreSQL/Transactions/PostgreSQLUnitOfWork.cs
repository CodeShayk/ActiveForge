using System;
using System.Data;
using Microsoft.Extensions.Logging;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="IUnitOfWork"/>.
    /// Wraps an existing <see cref="PostgreSQLConnection"/> so that all ORM operations
    /// performed through that connection participate in the same ADO.NET transaction.
    /// </summary>
    public sealed class PostgreSQLUnitOfWork : UnitOfWorkBase
    {
        private readonly PostgreSQLConnection _connection;

        /// <param name="connection">The open <see cref="PostgreSQLConnection"/> to manage.</param>
        /// <param name="logger">Optional logger; uses NullLogger when omitted.</param>
        public PostgreSQLUnitOfWork(PostgreSQLConnection connection, ILogger<PostgreSQLUnitOfWork> logger = null)
            : base(logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>The connection this unit-of-work manages.</summary>
        public PostgreSQLConnection Connection => _connection;

        protected override TransactionBase BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
