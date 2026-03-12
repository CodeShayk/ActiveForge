using System;
using System.Data;
using Microsoft.Extensions.Logging;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// SQL Server implementation of <see cref="IUnitOfWork"/>.
    /// Wraps an existing <see cref="SqlServerConnection"/> so that all ORM operations
    /// performed through that connection participate in the same ADO.NET transaction.
    /// </summary>
    public sealed class SqlServerUnitOfWork : BaseUnitOfWork
    {
        private readonly SqlServerConnection _connection;

        /// <param name="connection">The open <see cref="SqlServerConnection"/> to manage.</param>
        /// <param name="logger">Optional logger; uses NullLogger when omitted.</param>
        public SqlServerUnitOfWork(SqlServerConnection connection, ILogger<SqlServerUnitOfWork> logger = null)
            : base(connection, logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>The connection this unit-of-work manages.</summary>
        public SqlServerConnection Connection => _connection;

        protected override BaseTransaction BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
