using System;
using System.Data;
using Microsoft.Extensions.Logging;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// SQLite implementation of <see cref="IUnitOfWork"/>.
    /// Wraps an existing <see cref="SQLiteConnection"/> so that all ORM operations
    /// performed through that connection participate in the same ADO.NET transaction.
    /// </summary>
    public sealed class SQLiteUnitOfWork : BaseUnitOfWork
    {
        private readonly SQLiteConnection _connection;

        /// <param name="connection">The <see cref="SQLiteConnection"/> to manage.</param>
        /// <param name="logger">Optional logger; uses NullLogger when omitted.</param>
        public SQLiteUnitOfWork(SQLiteConnection connection, ILogger<SQLiteUnitOfWork> logger = null)
            : base(connection, logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>The connection this unit-of-work manages.</summary>
        public SQLiteConnection Connection => _connection;

        protected override BaseTransaction BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
