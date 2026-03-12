using System;
using System.Data;
using Microsoft.Extensions.Logging;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="IUnitOfWork"/>.
    /// Wraps an existing <see cref="PostgreSQLConnection"/> so that all ORM operations
    /// performed through that connection participate in the same ADO.NET transaction.
    /// </summary>
    public sealed class PostgreSQLUnitOfWork : BaseUnitOfWork
    {
        private readonly PostgreSQLConnection _connection;

        /// <param name="connection">The open <see cref="PostgreSQLConnection"/> to manage.</param>
        /// <param name="logger">Optional logger; uses NullLogger when omitted.</param>
        public PostgreSQLUnitOfWork(PostgreSQLConnection connection, ILogger<PostgreSQLUnitOfWork> logger = null)
            : base(connection, logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>The connection this unit-of-work manages.</summary>
        public PostgreSQLConnection Connection => _connection;

        protected override BaseTransaction BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
