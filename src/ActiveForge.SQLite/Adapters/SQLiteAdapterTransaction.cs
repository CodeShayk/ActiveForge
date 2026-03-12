using System;
using Microsoft.Data.Sqlite;

namespace ActiveForge.Adapters.SQLite
{
    /// <summary>
    /// SQLite implementation of <see cref="BaseTransaction"/> backed by
    /// <see cref="SqliteTransaction"/> from <c>Microsoft.Data.Sqlite</c>.
    /// <para>
    /// Wraps the commit/rollback/dispose lifecycle of a SQLite transaction behind the
    /// provider-agnostic <see cref="BaseTransaction"/> API consumed by the ORM engine.
    /// Instances are created by <see cref="SQLiteAdapterConnection.BeginTransaction"/> and
    /// must not be constructed directly by application code.
    /// </para>
    /// <para>
    /// Important: <c>Microsoft.Data.Sqlite</c> fails if a committed or rolled-back
    /// <see cref="SqliteTransaction"/> is reused. The ORM's
    /// <see cref="DBDataConnection.RunWrite"/> override syncs the internal transaction
    /// depth counter after a unit-of-work commit to prevent reuse of a closed transaction
    /// object.
    /// </para>
    /// </summary>
    public class SQLiteAdapterTransaction : BaseTransaction
    {
        /// <summary>The underlying SQLite transaction managed by this adapter.</summary>
        private readonly SqliteTransaction _tx;

        /// <summary>
        /// Initialises a new <see cref="SQLiteAdapterTransaction"/> wrapping the supplied
        /// <see cref="SqliteTransaction"/>. The transaction must already be active.
        /// </summary>
        /// <param name="tx">The <see cref="SqliteTransaction"/> to wrap.</param>
        public SQLiteAdapterTransaction(SqliteTransaction tx) { _tx = tx; }

        /// <summary>
        /// Exposes the underlying <see cref="SqliteTransaction"/> so that
        /// <see cref="SQLiteAdapterCommand"/> can attach it to
        /// <see cref="SqliteCommand.Transaction"/> before execution.
        /// </summary>
        /// <returns>The native <see cref="SqliteTransaction"/> managed by this adapter.</returns>
        public SqliteTransaction GetNativeTransaction() => _tx;

        /// <summary>
        /// Commits all changes made within this transaction to the SQLite database file
        /// (or flushes them to the in-memory database if <c>:memory:</c> is in use).
        /// Delegates to <see cref="SqliteTransaction.Commit"/>. After a successful commit
        /// the transaction must not be reused.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown by the provider if the transaction has already been committed or disposed.
        /// Unlike SQL Server, SQLite does not silently tolerate reuse of a committed
        /// transaction object.
        /// </exception>
        public override void Commit() => _tx.Commit();

        /// <summary>
        /// Rolls back all changes made within this transaction, discarding any
        /// modifications made since the transaction was started.
        /// Delegates to <see cref="SqliteTransaction.Rollback"/>. After a rollback the
        /// transaction must not be reused.
        /// </summary>
        public override void Rollback() => _tx.Rollback();

        /// <summary>
        /// Disposes the underlying <see cref="SqliteTransaction"/>, releasing all resources
        /// associated with it. If neither <see cref="Commit"/> nor <see cref="Rollback"/>
        /// has been called, <c>Microsoft.Data.Sqlite</c> will roll back the transaction on
        /// dispose.
        /// </summary>
        public override void Dispose() => _tx.Dispose();
    }
}
