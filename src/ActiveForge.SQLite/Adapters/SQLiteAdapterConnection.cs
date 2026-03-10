using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace ActiveForge.Adapters.SQLite
{
    /// <summary>
    /// SQLite implementation of <see cref="ConnectionBase"/> backed by
    /// <see cref="SqliteConnection"/> from <c>Microsoft.Data.Sqlite</c>.
    /// <para>
    /// Wraps the full lifecycle of a SQLite connection — open, close, transaction
    /// management, command creation, and transaction-state inspection — behind the
    /// provider-agnostic <see cref="ConnectionBase"/> API consumed by the ORM engine.
    /// </para>
    /// <para>
    /// In-memory databases: pass <c>"Data Source=:memory:"</c> as the connection string
    /// to obtain a lightweight in-memory SQLite database suitable for unit tests.
    /// </para>
    /// <para>
    /// Isolation-level mapping: <c>Microsoft.Data.Sqlite</c> supports only
    /// <see cref="IsolationLevel.ReadCommitted"/> and <see cref="IsolationLevel.Serializable"/>.
    /// <see cref="BeginTransaction"/> maps unsupported levels to the nearest supported
    /// equivalent before forwarding to the provider:
    /// <list type="table">
    ///   <listheader><term>Requested</term><description>Mapped to</description></listheader>
    ///   <item><term><see cref="IsolationLevel.ReadUncommitted"/></term><description><see cref="IsolationLevel.ReadCommitted"/></description></item>
    ///   <item><term><see cref="IsolationLevel.RepeatableRead"/></term><description><see cref="IsolationLevel.Serializable"/></description></item>
    ///   <item><term><see cref="IsolationLevel.Snapshot"/></term><description><see cref="IsolationLevel.Serializable"/></description></item>
    /// </list>
    /// All other levels (including <see cref="IsolationLevel.ReadCommitted"/> and
    /// <see cref="IsolationLevel.Serializable"/>) are passed through unchanged.
    /// </para>
    /// <para>
    /// Transaction-state inspection: SQLite transactions are always committable when
    /// present; <see cref="TransactionState"/> therefore returns
    /// <see cref="TransactionStates.CommittableTransaction"/> whenever the supplied
    /// argument is a <see cref="SQLiteAdapterTransaction"/> and
    /// <see cref="TransactionStates.NoTransaction"/> otherwise.
    /// </para>
    /// </summary>
    public class SQLiteAdapterConnection : ConnectionBase
    {
        /// <summary>The underlying SQLite connection managed by this adapter.</summary>
        private readonly SqliteConnection _conn;

        /// <summary>
        /// Initialises a new <see cref="SQLiteAdapterConnection"/> using the supplied
        /// ADO.NET connection string. The underlying <see cref="SqliteConnection"/> is
        /// created immediately but not opened; call <see cref="Open"/> before executing
        /// any commands.
        /// </summary>
        /// <param name="connectionString">
        /// A valid SQLite connection string (e.g. <c>"Data Source=mydb.db"</c> for a file
        /// database, or <c>"Data Source=:memory:"</c> for an in-memory database).
        /// </param>
        public SQLiteAdapterConnection(string connectionString)
        {
            _conn = new SqliteConnection(connectionString);
        }

        /// <summary>
        /// Opens the underlying <see cref="SqliteConnection"/>, establishing a connection
        /// to the SQLite database file (or creating the in-memory database if
        /// <c>:memory:</c> was specified as the data source).
        /// </summary>
        public override void Open()  => _conn.Open();

        /// <summary>
        /// Closes the underlying <see cref="SqliteConnection"/>. For in-memory databases
        /// this also destroys all data in the database.
        /// </summary>
        public override void Close() => _conn.Close();

        /// <summary>
        /// Returns <see langword="true"/> when the underlying <see cref="SqliteConnection"/>
        /// is in the <see cref="ConnectionState.Open"/> state.
        /// </summary>
        /// <returns><see langword="true"/> if the connection is open.</returns>
        public override bool IsConnected() => _conn.State == ConnectionState.Open;

        /// <summary>
        /// Returns the name of the database (catalogue) currently targeted by the
        /// connection, as reported by <see cref="SqliteConnection.Database"/>.
        /// For file-based databases this is typically the database filename without path.
        /// </summary>
        /// <returns>The database name string.</returns>
        public override string DatabaseName() => _conn.Database;

        /// <summary>
        /// Starts a new SQLite transaction at the (possibly remapped) isolation level and
        /// returns a <see cref="SQLiteAdapterTransaction"/> wrapping the native
        /// <see cref="SqliteTransaction"/>.
        /// <para>
        /// Unsupported isolation levels are silently promoted to the nearest supported
        /// equivalent before the transaction is started:
        /// <see cref="IsolationLevel.ReadUncommitted"/> → <see cref="IsolationLevel.ReadCommitted"/>;
        /// <see cref="IsolationLevel.RepeatableRead"/> and <see cref="IsolationLevel.Snapshot"/>
        /// → <see cref="IsolationLevel.Serializable"/>.
        /// </para>
        /// </summary>
        /// <param name="level">The requested <see cref="IsolationLevel"/>.</param>
        /// <returns>A <see cref="SQLiteAdapterTransaction"/> representing the started transaction.</returns>
        public override TransactionBase BeginTransaction(IsolationLevel level)
        {
            // Microsoft.Data.Sqlite supports ReadCommitted and Serializable only.
            // Map unsupported levels to the nearest supported equivalent.
            var sqliteLevel = level switch
            {
                IsolationLevel.ReadUncommitted => IsolationLevel.ReadCommitted,
                IsolationLevel.RepeatableRead  => IsolationLevel.Serializable,
                IsolationLevel.Snapshot        => IsolationLevel.Serializable,
                _                              => level
            };
            return new SQLiteAdapterTransaction(_conn.BeginTransaction(sqliteLevel));
        }

        /// <summary>
        /// Creates a new <see cref="SQLiteAdapterCommand"/> for the given SQL text, bound
        /// to this connection. The command's timeout is inherited from
        /// <see cref="ConnectionBase.GetTimeout"/>.
        /// </summary>
        /// <param name="sql">The SQL text to execute. Stored-procedure names are not supported by SQLite.</param>
        /// <returns>A <see cref="SQLiteAdapterCommand"/> ready for parameter binding and execution.</returns>
        public override CommandBase CreateCommand(string sql)
            => new SQLiteAdapterCommand(sql, this);

        /// <summary>
        /// Exposes the underlying <see cref="SqliteConnection"/> so that provider-specific
        /// code (e.g. <see cref="SQLiteAdapterCommand"/> during initialisation) can bind
        /// commands directly to the native connection object.
        /// </summary>
        /// <returns>The native <see cref="SqliteConnection"/> managed by this adapter.</returns>
        public SqliteConnection GetNativeConnection() => _conn;

        /// <summary>
        /// Determines the transactional state of the given <paramref name="transaction"/>.
        /// <para>
        /// SQLite does not provide a server-side state query equivalent to SQL Server's
        /// <c>XACT_STATE()</c> or PostgreSQL's <c>txid_current_if_assigned()</c>. Because
        /// SQLite transactions are always in a committable state when present (SQLite uses
        /// deferred locking rather than statement-level error poisoning), this method
        /// returns <see cref="TransactionStates.CommittableTransaction"/> whenever
        /// <paramref name="transaction"/> is a <see cref="SQLiteAdapterTransaction"/>, and
        /// <see cref="TransactionStates.NoTransaction"/> otherwise.
        /// </para>
        /// </summary>
        /// <param name="transaction">The transaction whose state is to be inspected.</param>
        /// <returns>A <see cref="TransactionStates"/> value describing the current state.</returns>
        public override TransactionStates TransactionState(TransactionBase transaction)
        {
            // SQLite transactions are always committable when present.
            return transaction is SQLiteAdapterTransaction
                ? TransactionStates.CommittableTransaction
                : TransactionStates.NoTransaction;
        }
    }
}
