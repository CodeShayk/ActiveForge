using System;
using System.Data;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class that wraps a native ADO.NET database connection.
    /// Decouples the ORM engine from any concrete ADO.NET provider by exposing a
    /// uniform connection lifecycle API. Concrete subclasses
    /// (e.g. <c>SqlAdapterConnection</c>, <c>NpgsqlAdapterConnection</c>,
    /// <c>SQLiteAdapterConnection</c>) delegate each operation to the underlying
    /// native connection object.
    /// </summary>
    public abstract class ConnectionBase
    {
        /// <summary>
        /// Describes the transactional state of a connection as understood by the ORM.
        /// Returned by <see cref="TransactionState"/> so that the engine can decide
        /// whether a commit or rollback is safe.
        /// </summary>
        public enum TransactionStates
        {
            /// <summary>No active transaction is associated with the connection.</summary>
            NoTransaction,

            /// <summary>
            /// A transaction is active and in a healthy state — a commit call will succeed.
            /// </summary>
            CommittableTransaction,

            /// <summary>
            /// A transaction is active but has entered an error state (e.g. a statement
            /// failed inside the transaction on PostgreSQL or SQL Server). Only a rollback
            /// is valid; attempting to commit will raise an exception.
            /// </summary>
            NonCommittableTransaction
        }

        /// <summary>The command timeout in seconds applied to every command created via <see cref="CreateCommand"/>.</summary>
        private int _timeout;

        /// <summary>
        /// Opens the underlying database connection.
        /// Must be called before any commands or transactions can be created.
        /// </summary>
        public abstract void Open();

        /// <summary>
        /// Closes the underlying database connection and returns it to the connection pool
        /// (if pooling is enabled by the provider).
        /// </summary>
        public abstract void Close();

        /// <summary>
        /// Starts a new database transaction at the specified isolation level and returns a
        /// <see cref="TransactionBase"/> that wraps the native transaction object.
        /// </summary>
        /// <param name="level">
        /// The <see cref="IsolationLevel"/> to use for the transaction. Providers that do
        /// not support a particular level may silently promote to the nearest supported level
        /// (e.g. SQLite promotes <see cref="IsolationLevel.ReadUncommitted"/> to
        /// <see cref="IsolationLevel.ReadCommitted"/>).
        /// </param>
        /// <returns>A <see cref="TransactionBase"/> wrapping the newly started native transaction.</returns>
        public abstract TransactionBase BeginTransaction(IsolationLevel level);

        /// <summary>
        /// Creates a new <see cref="CommandBase"/> for the given SQL text, associated with
        /// this connection. The command inherits the timeout returned by <see cref="GetTimeout"/>.
        /// </summary>
        /// <param name="sql">The SQL text (or stored-procedure name) to execute.</param>
        /// <returns>A provider-specific <see cref="CommandBase"/> ready to execute.</returns>
        public abstract CommandBase CreateCommand(string sql);

        /// <summary>
        /// Returns <see langword="true"/> when the underlying native connection is currently
        /// in the <see cref="ConnectionState.Open"/> state; otherwise <see langword="false"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the connection is open.</returns>
        public abstract bool IsConnected();

        /// <summary>
        /// Returns the name of the database (catalogue) that the connection is currently
        /// pointed at, as reported by the underlying ADO.NET connection.
        /// </summary>
        /// <returns>The database name string.</returns>
        public abstract string DatabaseName();

        /// <summary>
        /// Queries the provider to determine the transactional state of the supplied
        /// <paramref name="transaction"/>. The mechanism used varies by provider:
        /// SQL Server executes <c>SELECT XACT_STATE()</c>; PostgreSQL queries
        /// <c>txid_current_if_assigned()</c>; SQLite always reports
        /// <see cref="TransactionStates.CommittableTransaction"/> when a transaction object
        /// is present.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="TransactionBase"/> to inspect. If the value is not a recognised
        /// provider type, implementations return <see cref="TransactionStates.NoTransaction"/>.
        /// </param>
        /// <returns>A <see cref="TransactionStates"/> value describing the current state.</returns>
        public abstract TransactionStates TransactionState(TransactionBase transaction);

        /// <summary>
        /// Returns the command timeout, in seconds, that is applied to every command
        /// created via <see cref="CreateCommand"/>. A value of <c>0</c> means no timeout.
        /// </summary>
        /// <returns>The timeout in seconds.</returns>
        public int GetTimeout() => _timeout;

        /// <summary>
        /// Sets the command timeout, in seconds, that will be applied to every command
        /// subsequently created via <see cref="CreateCommand"/>.
        /// A value of <c>0</c> disables the timeout.
        /// </summary>
        /// <param name="seconds">The timeout duration in seconds.</param>
        public void SetTimeout(int seconds) => _timeout = seconds;
    }
}
