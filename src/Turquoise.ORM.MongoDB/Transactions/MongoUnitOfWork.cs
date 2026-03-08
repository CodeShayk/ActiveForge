using System.Data;
using Microsoft.Extensions.Logging;
using Turquoise.ORM.Transactions;

namespace Turquoise.ORM
{
    /// <summary>
    /// Unit of Work implementation for MongoDB.
    /// Wraps <see cref="MongoDataConnection"/> in Turquoise ORM's nested-transaction model.
    /// </summary>
    /// <remarks>
    /// MongoDB multi-document transactions require a replica set or sharded cluster.
    /// On a standalone server, <see cref="CreateTransaction"/> will throw a MongoDB error.
    /// </remarks>
    public sealed class MongoUnitOfWork : UnitOfWorkBase
    {
        private readonly MongoDataConnection _connection;

        /// <param name="connection">The connected <see cref="MongoDataConnection"/> to manage transactions on.</param>
        /// <param name="logger">Optional logger; <c>NullLogger</c> used when omitted.</param>
        public MongoUnitOfWork(MongoDataConnection connection, ILogger? logger = null)
            : base(logger)
        {
            _connection = connection ?? throw new System.ArgumentNullException(nameof(connection));
        }

        /// <summary>Returns the underlying <see cref="MongoDataConnection"/>.</summary>
        public MongoDataConnection Connection => _connection;

        protected override TransactionBase BeginTransactionCore(IsolationLevel level)
            => _connection.BeginTransaction(level);
    }
}
