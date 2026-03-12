using System.Data;
using Microsoft.Extensions.Logging;
using ActiveForge.Transactions;

namespace ActiveForge
{
    /// <summary>
    /// Unit of Work implementation for MongoDB.
    /// Wraps <see cref="MongoDataConnection"/> in ActiveForge ORM's nested-transaction model.
    /// </summary>
    /// <remarks>
    /// MongoDB multi-document transactions require a replica set or sharded cluster.
    /// On a standalone server, <see cref="CreateTransaction"/> will throw a MongoDB error.
    /// </remarks>
    public sealed class MongoUnitOfWork : BaseUnitOfWork
    {
        private readonly MongoDataConnection _connection;

        /// <param name="connection">The connected <see cref="MongoDataConnection"/> to manage transactions on.</param>
        /// <param name="logger">Optional logger; <c>NullLogger</c> used when omitted.</param>
        public MongoUnitOfWork(MongoDataConnection connection, ILogger? logger = null)
            : base(connection, logger)
        {
            _connection = connection ?? throw new System.ArgumentNullException(nameof(connection));
        }

        /// <summary>Returns the underlying <see cref="MongoDataConnection"/>.</summary>
        public MongoDataConnection Connection => _connection;

        protected override BaseTransaction BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
