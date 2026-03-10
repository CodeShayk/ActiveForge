using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    /// <summary>
    /// SQL Server implementation of <see cref="CommandBase"/> backed by
    /// <see cref="SqlCommand"/> from <c>Microsoft.Data.SqlClient</c>.
    /// <para>
    /// Each instance wraps a single <see cref="SqlCommand"/> that is initialised at
    /// construction time. The command is bound to the native <see cref="SqlConnection"/>
    /// extracted from the supplied <see cref="SqlAdapterConnection"/>, and its timeout is
    /// inherited from <see cref="ConnectionBase.GetTimeout"/>.
    /// </para>
    /// <para>
    /// Provider-specific behaviour: <see cref="AddNativeParameter"/> unwraps any
    /// <see cref="TField"/> wrapper by calling <c>TField.GetValue()</c> before creating
    /// the <see cref="SqlParameter"/>. This is necessary because <c>SqlClient</c> does not
    /// know how to serialise <see cref="TField"/> instances directly. String parameters
    /// whose <see cref="TargetFieldInfo.MaxLength"/> is positive also have their
    /// <see cref="SqlParameter.Size"/> set, which prevents implicit type widening on the
    /// server.
    /// </para>
    /// </summary>
    public class SqlAdapterCommand : CommandBase
    {
        /// <summary>The underlying SQL Server command object managed by this adapter.</summary>
        private SqlCommand _cmd;

        /// <summary>
        /// Initialises a new <see cref="SqlAdapterCommand"/> for the given SQL text and
        /// connection. The underlying <see cref="SqlCommand"/> is created immediately and
        /// its <c>CommandTimeout</c> is set from the connection's configured timeout.
        /// </summary>
        /// <param name="sql">The SQL text (or stored-procedure name) to execute.</param>
        /// <param name="connection">The <see cref="SqlAdapterConnection"/> through which the command will execute.</param>
        public SqlAdapterCommand(string sql, SqlAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        /// <summary>
        /// Creates the underlying <see cref="SqlCommand"/>, binding it to the native
        /// <see cref="SqlConnection"/> and applying the connection timeout.
        /// </summary>
        private void InitCommand()
        {
            _cmd = new SqlCommand(SQL, ((SqlAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        /// <summary>
        /// Attempts to cancel an in-progress execution of the command.
        /// Delegates directly to <see cref="SqlCommand.Cancel"/>.
        /// </summary>
        public override void Cancel() => _cmd.Cancel();

        /// <summary>
        /// Executes a non-query SQL statement (INSERT, UPDATE, DELETE, DDL, etc.) and
        /// returns the number of rows affected. Enlists the current ambient transaction
        /// before execution via <c>AttachTransaction</c>.
        /// </summary>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqlException"/> is raised by the provider, wrapping the
        /// original exception with its message.
        /// </exception>
        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="ReaderBase"/> that streams the
        /// result set using <see cref="CommandBehavior.Default"/> (random-access column
        /// reads). Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="SqlAdapterReader"/> wrapping the <see cref="SqlDataReader"/>.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqlException"/> is raised by the provider.
        /// </exception>
        public override ReaderBase ExecuteReader()
        {
            AttachTransaction();
            try   { return new SqlAdapterReader(_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="ReaderBase"/> that streams the
        /// result set using <see cref="CommandBehavior.SequentialAccess"/>, which avoids
        /// buffering column data and can reduce memory pressure for large result sets.
        /// Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="SqlAdapterReader"/> wrapping the <see cref="SqlDataReader"/> in sequential-access mode.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqlException"/> is raised by the provider.
        /// </exception>
        public override ReaderBase ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new SqlAdapterReader(_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns the value of the first column of the first row.
        /// Commonly used to retrieve identity values generated by INSERT statements.
        /// Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>
        /// The scalar result as an <see cref="object"/>, or <see langword="null"/> if the
        /// result set is empty.
        /// </returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqlException"/> is raised by the provider.
        /// </exception>
        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Switches the underlying <see cref="SqlCommand"/> to stored-procedure mode by
        /// setting its <c>CommandType</c> to <see cref="CommandType.StoredProcedure"/>.
        /// After this call the SQL text is treated as a stored-procedure name.
        /// </summary>
        public override void SetToStoredProcedure() => _cmd.CommandType = CommandType.StoredProcedure;

        /// <summary>
        /// Disposes the underlying <see cref="SqlCommand"/>, releasing all server-side
        /// resources associated with it.
        /// </summary>
        public override void Dispose() => _cmd?.Dispose();

        /// <summary>
        /// Adds a parameter to the underlying <see cref="SqlCommand"/>.
        /// <para>
        /// <b>TField unwrapping:</b> if <paramref name="value"/> is a <see cref="TField"/>
        /// instance it is unwrapped by calling <c>TField.GetValue()</c> before the
        /// <see cref="SqlParameter"/> is created. This is required because
        /// <c>Microsoft.Data.SqlClient</c> does not recognise <see cref="TField"/> as a
        /// valid parameter value type.
        /// </para>
        /// <para>
        /// <b>Size constraint:</b> when <paramref name="info"/> indicates a string field
        /// with a positive <see cref="TargetFieldInfo.MaxLength"/>, the parameter's
        /// <see cref="SqlParameter.Size"/> is set accordingly to prevent implicit widening
        /// to <c>nvarchar(max)</c> on the server.
        /// </para>
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text (e.g. <c>@Name</c>).</param>
        /// <param name="value">
        /// The parameter value, already normalised to <see cref="DBNull.Value"/> when
        /// logically null by the base-class caller. Any <see cref="TField"/> wrapper is
        /// unwrapped here.
        /// </param>
        /// <param name="info">
        /// Optional field metadata providing the CLR type and maximum length. May be
        /// <see langword="null"/>.
        /// </param>
        protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
        {
            // Unwrap TField wrappers to their underlying CLR values
            if (value is TField tf) value = tf.GetValue();
            var p = new SqlParameter(name, value ?? DBNull.Value);
            if (info?.MaxLength > 0 && info.TargetType == typeof(string))
                p.Size = info.MaxLength;
            _cmd.Parameters.Add(p);
        }

        /// <summary>
        /// Enlists the command in the current ambient transaction by assigning the native
        /// <see cref="SqlTransaction"/> to <see cref="SqlCommand.Transaction"/>.
        /// Does nothing if no transaction has been set via <see cref="CommandBase.SetTransaction"/>.
        /// </summary>
        private void AttachTransaction()
        {
            if (Transaction is SqlAdapterTransaction sat)
                _cmd.Transaction = sat.GetNativeTransaction();
        }
    }
}
