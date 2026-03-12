using System;
using System.Data;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="BaseConnection"/> backed by
    /// <see cref="NpgsqlConnection"/> from the <c>Npgsql</c> library.
    /// <para>
    /// Wraps the full lifecycle of a PostgreSQL connection — open, close, transaction
    /// management, command creation, and transaction-state inspection — behind the
    /// provider-agnostic <see cref="BaseConnection"/> API consumed by the ORM engine.
    /// </para>
    /// <para>
    /// Transaction-state inspection is performed by executing
    /// <c>SELECT txid_current_if_assigned() IS NOT NULL</c> within the transaction's
    /// scope. Because PostgreSQL does not expose a direct equivalent of SQL Server's
    /// <c>XACT_STATE()</c>, this query returns <see langword="true"/> when a transaction
    /// ID has been assigned (i.e. the transaction is active and committable) and
    /// <see langword="false"/> otherwise. If the query itself raises an exception (which
    /// happens when the transaction is already in an error/aborted state), the method
    /// returns <see cref="TransactionStates.NonCommittableTransaction"/>.
    /// </para>
    /// </summary>
    public class NpgsqlAdapterConnection : BaseConnection
    {
        /// <summary>The underlying PostgreSQL connection managed by this adapter.</summary>
        private readonly NpgsqlConnection _conn;

        /// <summary>
        /// Initialises a new <see cref="NpgsqlAdapterConnection"/> using the supplied
        /// ADO.NET connection string. The underlying <see cref="NpgsqlConnection"/> is
        /// created immediately but not opened; call <see cref="Open"/> before executing
        /// any commands.
        /// </summary>
        /// <param name="connectionString">
        /// A valid Npgsql connection string (e.g.
        /// <c>"Host=localhost;Database=mydb;Username=postgres;Password=secret;"</c>).
        /// </param>
        public NpgsqlAdapterConnection(string connectionString)
        {
            _conn = new NpgsqlConnection(connectionString);
        }

        /// <summary>
        /// Opens the underlying <see cref="NpgsqlConnection"/>, establishing a physical
        /// connection to the PostgreSQL server (or retrieving one from the connection pool).
        /// </summary>
        public override void Open()  => _conn.Open();

        /// <summary>
        /// Closes the underlying <see cref="NpgsqlConnection"/>, returning the connection
        /// to the pool if connection pooling is enabled.
        /// </summary>
        public override void Close() => _conn.Close();

        /// <summary>
        /// Returns <see langword="true"/> when the underlying <see cref="NpgsqlConnection"/>
        /// is in the <see cref="ConnectionState.Open"/> state.
        /// </summary>
        /// <returns><see langword="true"/> if the connection is open.</returns>
        public override bool IsConnected() => _conn.State == ConnectionState.Open;

        /// <summary>
        /// Returns the name of the database (catalogue) that the connection is currently
        /// targeting, as reported by <see cref="NpgsqlConnection.Database"/>.
        /// </summary>
        /// <returns>The database name string.</returns>
        public override string DatabaseName() => _conn.Database;

        /// <summary>
        /// Starts a new PostgreSQL transaction at the specified isolation level and returns
        /// a <see cref="NpgsqlAdapterTransaction"/> wrapping the native
        /// <see cref="NpgsqlTransaction"/>.
        /// </summary>
        /// <param name="level">The <see cref="IsolationLevel"/> for the new transaction.</param>
        /// <returns>A <see cref="NpgsqlAdapterTransaction"/> representing the started transaction.</returns>
        public override BaseTransaction BeginTransaction(IsolationLevel level)
            => new NpgsqlAdapterTransaction(_conn.BeginTransaction(level));

        /// <summary>
        /// Creates a new <see cref="NpgsqlAdapterCommand"/> for the given SQL text, bound
        /// to this connection. The command's timeout is inherited from
        /// <see cref="BaseConnection.GetTimeout"/>.
        /// </summary>
        /// <param name="sql">The SQL text (or function name) to execute.</param>
        /// <returns>A <see cref="NpgsqlAdapterCommand"/> ready for parameter binding and execution.</returns>
        public override BaseCommand CreateCommand(string sql)
            => new NpgsqlAdapterCommand(sql, this);

        /// <summary>
        /// Exposes the underlying <see cref="NpgsqlConnection"/> so that provider-specific
        /// code (e.g. <see cref="NpgsqlAdapterCommand"/> during initialisation) can bind
        /// commands directly to the native connection object.
        /// </summary>
        /// <returns>The native <see cref="NpgsqlConnection"/> managed by this adapter.</returns>
        public NpgsqlConnection GetNativeConnection() => _conn;

        /// <summary>
        /// Determines the transactional state of the given <paramref name="transaction"/>
        /// by executing <c>SELECT txid_current_if_assigned() IS NOT NULL</c> on the server
        /// within the transaction's scope.
        /// <para>
        /// PostgreSQL does not expose a direct <c>XACT_STATE()</c> equivalent:
        /// <list type="bullet">
        ///   <item><description>
        ///     If the query returns <see langword="true"/>, a transaction ID has been
        ///     assigned and the transaction is committable →
        ///     <see cref="TransactionStates.CommittableTransaction"/>.
        ///   </description></item>
        ///   <item><description>
        ///     If the query returns <see langword="false"/>, no transaction is active →
        ///     <see cref="TransactionStates.NoTransaction"/>.
        ///   </description></item>
        ///   <item><description>
        ///     If the query itself throws an exception (because the transaction is in an
        ///     aborted/error state), the method returns
        ///     <see cref="TransactionStates.NonCommittableTransaction"/>.
        ///   </description></item>
        /// </list>
        /// Returns <see cref="TransactionStates.NoTransaction"/> when
        /// <paramref name="transaction"/> is not a <see cref="NpgsqlAdapterTransaction"/>.
        /// </para>
        /// </summary>
        /// <param name="transaction">The transaction whose state is to be inspected.</param>
        /// <returns>A <see cref="TransactionStates"/> value describing the current state.</returns>
        public override TransactionStates TransactionState(BaseTransaction transaction)
        {
            if (transaction is NpgsqlAdapterTransaction nat)
            {
                // PostgreSQL does not expose XACT_STATE() directly.
                // We check whether the underlying transaction is still active by
                // querying txid_current_if_assigned(): NULL means no active transaction.
                try
                {
                    using var cmd = new NpgsqlCommand(
                        "SELECT txid_current_if_assigned() IS NOT NULL", _conn)
                    { Transaction = nat.GetNativeTransaction() };

                    bool inTx = (bool)cmd.ExecuteScalar();
                    return inTx
                        ? TransactionStates.CommittableTransaction
                        : TransactionStates.NoTransaction;
                }
                catch
                {
                    // If the transaction is already in an error state the query itself
                    // will throw; treat that as a non-committable transaction.
                    return TransactionStates.NonCommittableTransaction;
                }
            }

            return TransactionStates.NoTransaction;
        }
    }
}
