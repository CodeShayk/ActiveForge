using System;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="TransactionBase"/> backed by
    /// <see cref="NpgsqlTransaction"/> from the <c>Npgsql</c> library.
    /// <para>
    /// Wraps the commit/rollback/dispose lifecycle of a PostgreSQL transaction behind the
    /// provider-agnostic <see cref="TransactionBase"/> API consumed by the ORM engine.
    /// Instances are created by <see cref="NpgsqlAdapterConnection.BeginTransaction"/> and
    /// must not be constructed directly by application code.
    /// </para>
    /// <para>
    /// Note: if a SQL statement raises an error inside a PostgreSQL transaction, the
    /// transaction enters an aborted state and only a rollback (or <c>ROLLBACK TO SAVEPOINT</c>)
    /// is valid. Attempting to commit in this state will throw.
    /// <see cref="NpgsqlAdapterConnection.TransactionState"/> detects this condition by
    /// querying <c>txid_current_if_assigned()</c>.
    /// </para>
    /// </summary>
    public class NpgsqlAdapterTransaction : TransactionBase
    {
        /// <summary>The underlying Npgsql transaction managed by this adapter.</summary>
        private readonly NpgsqlTransaction _tx;

        /// <summary>
        /// Initialises a new <see cref="NpgsqlAdapterTransaction"/> wrapping the supplied
        /// <see cref="NpgsqlTransaction"/>. The transaction must already be active.
        /// </summary>
        /// <param name="tx">The <see cref="NpgsqlTransaction"/> to wrap.</param>
        public NpgsqlAdapterTransaction(NpgsqlTransaction tx) { _tx = tx; }

        /// <summary>
        /// Exposes the underlying <see cref="NpgsqlTransaction"/> so that
        /// <see cref="NpgsqlAdapterCommand"/> can attach it to
        /// <see cref="NpgsqlCommand.Transaction"/> before execution, and so that
        /// <see cref="NpgsqlAdapterConnection.TransactionState"/> can execute diagnostic
        /// queries within the transaction's scope.
        /// </summary>
        /// <returns>The native <see cref="NpgsqlTransaction"/> managed by this adapter.</returns>
        public NpgsqlTransaction GetNativeTransaction() => _tx;

        /// <summary>
        /// Commits all changes made within this transaction to the PostgreSQL database.
        /// Delegates to <see cref="NpgsqlTransaction.Commit"/>. After a successful commit
        /// the transaction is no longer usable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown by the provider if the transaction is in an aborted state (a prior
        /// statement failed and the transaction was not rolled back).
        /// </exception>
        public override void Commit() => _tx.Commit();

        /// <summary>
        /// Rolls back all changes made within this transaction, restoring the PostgreSQL
        /// database to its state at the time the transaction was started.
        /// Delegates to <see cref="NpgsqlTransaction.Rollback"/>. After a rollback the
        /// transaction is no longer usable.
        /// </summary>
        public override void Rollback() => _tx.Rollback();

        /// <summary>
        /// Disposes the underlying <see cref="NpgsqlTransaction"/>, releasing all resources
        /// associated with it. If neither <see cref="Commit"/> nor <see cref="Rollback"/>
        /// has been called, Npgsql will implicitly roll back the transaction on dispose.
        /// </summary>
        public override void Dispose() => _tx.Dispose();
    }
}
