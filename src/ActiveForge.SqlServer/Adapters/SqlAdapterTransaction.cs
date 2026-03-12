using System;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    /// <summary>
    /// SQL Server implementation of <see cref="BaseTransaction"/> backed by
    /// <see cref="SqlTransaction"/> from <c>Microsoft.Data.SqlClient</c>.
    /// <para>
    /// Wraps the commit/rollback/dispose lifecycle of a SQL Server transaction behind the
    /// provider-agnostic <see cref="BaseTransaction"/> API consumed by the ORM engine.
    /// Instances are created by <see cref="SqlAdapterConnection.BeginTransaction"/> and
    /// must not be constructed directly by application code.
    /// </para>
    /// </summary>
    public class SqlAdapterTransaction : BaseTransaction
    {
        /// <summary>The underlying SQL Server transaction managed by this adapter.</summary>
        private readonly SqlTransaction _tx;

        /// <summary>
        /// Initialises a new <see cref="SqlAdapterTransaction"/> wrapping the supplied
        /// <see cref="SqlTransaction"/>. The transaction must already be active.
        /// </summary>
        /// <param name="tx">The <see cref="SqlTransaction"/> to wrap.</param>
        public SqlAdapterTransaction(SqlTransaction tx) { _tx = tx; }

        /// <summary>
        /// Exposes the underlying <see cref="SqlTransaction"/> so that
        /// <see cref="SqlAdapterCommand"/> can attach it to <see cref="SqlCommand.Transaction"/>
        /// before execution, and so that <see cref="SqlAdapterConnection.TransactionState"/>
        /// can execute diagnostic queries within the transaction's scope.
        /// </summary>
        /// <returns>The native <see cref="SqlTransaction"/> managed by this adapter.</returns>
        public SqlTransaction GetNativeTransaction() => _tx;

        /// <summary>
        /// Commits all changes made within this transaction to the SQL Server database.
        /// Delegates to <see cref="SqlTransaction.Commit"/>. After a successful commit
        /// the transaction is no longer usable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown by the provider if the transaction has already been committed or rolled back,
        /// or if the connection is broken.
        /// </exception>
        public override void Commit() => _tx.Commit();

        /// <summary>
        /// Rolls back all changes made within this transaction, restoring the SQL Server
        /// database to its state at the time the transaction was started.
        /// Delegates to <see cref="SqlTransaction.Rollback"/>. After a rollback the
        /// transaction is no longer usable.
        /// </summary>
        public override void Rollback() => _tx.Rollback();

        /// <summary>
        /// Disposes the underlying <see cref="SqlTransaction"/>, releasing all resources
        /// associated with it. If neither <see cref="Commit"/> nor <see cref="Rollback"/>
        /// has been called, the transaction will be implicitly rolled back by the provider.
        /// </summary>
        public override void Dispose() => _tx.Dispose();
    }
}
