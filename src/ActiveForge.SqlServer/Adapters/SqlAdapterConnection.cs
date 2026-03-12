using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    /// <summary>
    /// SQL Server implementation of <see cref="BaseConnection"/> backed by
    /// <see cref="SqlConnection"/> from <c>Microsoft.Data.SqlClient</c>.
    /// <para>
    /// Wraps the full lifecycle of a SQL Server connection — open, close, transaction
    /// management, command creation, and transaction-state inspection — behind the
    /// provider-agnostic <see cref="BaseConnection"/> API consumed by the ORM engine.
    /// </para>
    /// <para>
    /// Transaction-state inspection is performed by executing
    /// <c>SELECT XACT_STATE()</c> against the server. The return values map as follows:
    /// <list type="bullet">
    ///   <item><description><c>1</c> → <see cref="TransactionStates.CommittableTransaction"/></description></item>
    ///   <item><description><c>0</c> → <see cref="TransactionStates.NoTransaction"/></description></item>
    ///   <item><description><c>-1</c> → <see cref="TransactionStates.NonCommittableTransaction"/></description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class SqlAdapterConnection : BaseConnection
    {
        /// <summary>The underlying SQL Server connection managed by this adapter.</summary>
        private readonly SqlConnection _conn;

        /// <summary>
        /// Initialises a new <see cref="SqlAdapterConnection"/> using the supplied
        /// ADO.NET connection string. The underlying <see cref="SqlConnection"/> is created
        /// immediately but not opened; call <see cref="Open"/> before executing any commands.
        /// </summary>
        /// <param name="connectionString">
        /// A valid SQL Server connection string (e.g.
        /// <c>"Server=.;Database=MyDb;Integrated Security=true;"</c>).
        /// </param>
        public SqlAdapterConnection(string connectionString)
        {
            _conn = new SqlConnection(connectionString);
        }

        /// <summary>
        /// Opens the underlying <see cref="SqlConnection"/>, establishing a physical
        /// connection to the SQL Server instance (or retrieving one from the connection pool).
        /// </summary>
        public override void Open()  => _conn.Open();

        /// <summary>
        /// Closes the underlying <see cref="SqlConnection"/>, returning the connection to
        /// the pool if connection pooling is enabled.
        /// </summary>
        public override void Close() => _conn.Close();

        /// <summary>
        /// Returns <see langword="true"/> when the underlying <see cref="SqlConnection"/>
        /// is in the <see cref="ConnectionState.Open"/> state.
        /// </summary>
        /// <returns><see langword="true"/> if the connection is open.</returns>
        public override bool IsConnected() => _conn.State == ConnectionState.Open;

        /// <summary>
        /// Returns the name of the database (catalogue) that the connection is currently
        /// targeting, as reported by <see cref="SqlConnection.Database"/>.
        /// </summary>
        /// <returns>The database name string.</returns>
        public override string DatabaseName() => _conn.Database;

        /// <summary>
        /// Starts a new SQL Server transaction at the specified isolation level and returns
        /// a <see cref="SqlAdapterTransaction"/> wrapping the native <see cref="SqlTransaction"/>.
        /// </summary>
        /// <param name="level">The <see cref="IsolationLevel"/> for the new transaction.</param>
        /// <returns>A <see cref="SqlAdapterTransaction"/> representing the started transaction.</returns>
        public override BaseTransaction BeginTransaction(IsolationLevel level)
            => new SqlAdapterTransaction(_conn.BeginTransaction(level));

        /// <summary>
        /// Creates a new <see cref="SqlAdapterCommand"/> for the given SQL text, bound to
        /// this connection. The command's timeout is inherited from <see cref="BaseConnection.GetTimeout"/>.
        /// </summary>
        /// <param name="sql">The SQL text (or stored-procedure name) to execute.</param>
        /// <returns>A <see cref="SqlAdapterCommand"/> ready for parameter binding and execution.</returns>
        public override BaseCommand CreateCommand(string sql)
            => new SqlAdapterCommand(sql, this);

        /// <summary>
        /// Exposes the underlying <see cref="SqlConnection"/> so that provider-specific
        /// code (e.g. <see cref="SqlAdapterCommand"/> during initialisation) can bind
        /// commands directly to the native connection object.
        /// </summary>
        /// <returns>The native <see cref="SqlConnection"/> managed by this adapter.</returns>
        public SqlConnection GetNativeConnection() => _conn;

        /// <summary>
        /// Determines the transactional state of the given <paramref name="transaction"/>
        /// by executing <c>SELECT XACT_STATE()</c> on the server within the transaction's
        /// scope. Returns <see cref="TransactionStates.NoTransaction"/> when
        /// <paramref name="transaction"/> is not a <see cref="SqlAdapterTransaction"/>.
        /// <para>
        /// SQL Server <c>XACT_STATE()</c> return values:
        /// <list type="table">
        ///   <listheader><term>Value</term><description>Meaning</description></listheader>
        ///   <item><term><c>1</c></term><description>Active, committable transaction.</description></item>
        ///   <item><term><c>0</c></term><description>No active transaction.</description></item>
        ///   <item><term><c>-1</c></term><description>Active but non-committable (doomed) transaction.</description></item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="transaction">The transaction whose state is to be inspected.</param>
        /// <returns>A <see cref="TransactionStates"/> value describing the current state.</returns>
        public override TransactionStates TransactionState(BaseTransaction transaction)
        {
            if (transaction is SqlAdapterTransaction sat)
            {
                using var cmd = new SqlCommand("SELECT XACT_STATE();", _conn)
                { Transaction = sat.GetNativeTransaction() };
                int state = Convert.ToInt32(cmd.ExecuteScalar());
                return state switch
                {
                    -1 => TransactionStates.NonCommittableTransaction,
                     0 => TransactionStates.NoTransaction,
                    _  => TransactionStates.CommittableTransaction
                };
            }
            return TransactionStates.NoTransaction;
        }
    }
}
