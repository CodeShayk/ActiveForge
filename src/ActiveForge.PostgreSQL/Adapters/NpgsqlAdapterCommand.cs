using System;
using System.Data;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="BaseCommand"/> backed by
    /// <see cref="NpgsqlCommand"/> from the <c>Npgsql</c> library.
    /// <para>
    /// Each instance wraps a single <see cref="NpgsqlCommand"/> that is initialised at
    /// construction time. The command is bound to the native <see cref="NpgsqlConnection"/>
    /// extracted from the supplied <see cref="NpgsqlAdapterConnection"/>, and its timeout
    /// is inherited from <see cref="BaseConnection.GetTimeout"/>.
    /// </para>
    /// <para>
    /// Provider-specific behaviour: <see cref="AddNativeParameter"/> unwraps any
    /// <see cref="TField"/> wrapper by calling <c>TField.GetValue()</c> before creating
    /// the <see cref="NpgsqlParameter"/>. String parameters whose
    /// <see cref="TargetFieldInfo.MaxLength"/> is positive also have their
    /// <see cref="NpgsqlParameter.Size"/> set to avoid implicit widening to unbounded
    /// <c>text</c> on the server. PostgreSQL exceptions are caught and re-thrown as
    /// <see cref="PersistenceException"/> with a <c>"PostgreSQL error: …"</c> prefix.
    /// </para>
    /// </summary>
    public class NpgsqlAdapterCommand : BaseCommand
    {
        /// <summary>The underlying Npgsql command object managed by this adapter.</summary>
        private NpgsqlCommand _cmd;

        /// <summary>
        /// Initialises a new <see cref="NpgsqlAdapterCommand"/> for the given SQL text and
        /// connection. The underlying <see cref="NpgsqlCommand"/> is created immediately
        /// and its <c>CommandTimeout</c> is set from the connection's configured timeout.
        /// </summary>
        /// <param name="sql">The SQL text (or stored-procedure name) to execute.</param>
        /// <param name="connection">The <see cref="NpgsqlAdapterConnection"/> through which the command will execute.</param>
        public NpgsqlAdapterCommand(string sql, NpgsqlAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        /// <summary>
        /// Creates the underlying <see cref="NpgsqlCommand"/>, binding it to the native
        /// <see cref="NpgsqlConnection"/> and applying the connection timeout.
        /// </summary>
        private void InitCommand()
        {
            _cmd = new NpgsqlCommand(SQL, ((NpgsqlAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        /// <summary>
        /// Attempts to cancel an in-progress execution of the command.
        /// Delegates directly to <see cref="NpgsqlCommand.Cancel"/>.
        /// </summary>
        public override void Cancel() => _cmd.Cancel();

        /// <summary>
        /// Executes a non-query SQL statement (INSERT, UPDATE, DELETE, DDL, etc.) and
        /// returns the number of rows affected. Enlists the current ambient transaction
        /// before execution via <c>AttachTransaction</c>.
        /// </summary>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="NpgsqlException"/> is raised by the provider, wrapping
        /// the original exception with a <c>"PostgreSQL error: …"</c> message prefix.
        /// </exception>
        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set using <see cref="CommandBehavior.Default"/> (random-access column
        /// reads). Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="NpgsqlAdapterReader"/> wrapping the <see cref="NpgsqlDataReader"/>.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="NpgsqlException"/> is raised by the provider.
        /// </exception>
        public override BaseReader ExecuteReader()
        {
            AttachTransaction();
            try   { return new NpgsqlAdapterReader((NpgsqlDataReader)_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set using <see cref="CommandBehavior.SequentialAccess"/>, which avoids
        /// buffering column data and can reduce memory pressure for large result sets.
        /// Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="NpgsqlAdapterReader"/> wrapping the <see cref="NpgsqlDataReader"/> in sequential-access mode.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="NpgsqlException"/> is raised by the provider.
        /// </exception>
        public override BaseReader ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new NpgsqlAdapterReader((NpgsqlDataReader)_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns the value of the first column of the first row.
        /// Commonly used to retrieve sequences or aggregate results.
        /// Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>
        /// The scalar result as an <see cref="object"/>, or <see langword="null"/> if the
        /// result set is empty.
        /// </returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="NpgsqlException"/> is raised by the provider.
        /// </exception>
        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Switches the underlying <see cref="NpgsqlCommand"/> to stored-procedure mode by
        /// setting its <c>CommandType</c> to <see cref="CommandType.StoredProcedure"/>.
        /// After this call the SQL text is treated as a stored-procedure (function) name.
        /// </summary>
        public override void SetToStoredProcedure() => _cmd.CommandType = CommandType.StoredProcedure;

        /// <summary>
        /// Disposes the underlying <see cref="NpgsqlCommand"/>, releasing all server-side
        /// resources associated with it.
        /// </summary>
        public override void Dispose() => _cmd?.Dispose();

        /// <summary>
        /// Adds a parameter to the underlying <see cref="NpgsqlCommand"/>.
        /// <para>
        /// <b>TField unwrapping:</b> if <paramref name="value"/> is a <see cref="TField"/>
        /// instance it is unwrapped by calling <c>TField.GetValue()</c> before the
        /// <see cref="NpgsqlParameter"/> is created. This is required because Npgsql does
        /// not recognise <see cref="TField"/> as a valid parameter value type.
        /// </para>
        /// <para>
        /// <b>Size constraint:</b> when <paramref name="info"/> indicates a string field
        /// with a positive <see cref="TargetFieldInfo.MaxLength"/>, the parameter's
        /// <see cref="NpgsqlParameter.Size"/> is set accordingly.
        /// </para>
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text (e.g. <c>@name</c> or <c>:name</c>).</param>
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
            var p = new NpgsqlParameter(name, value ?? DBNull.Value);
            if (info?.MaxLength > 0 && info.TargetType == typeof(string))
                p.Size = info.MaxLength;
            _cmd.Parameters.Add(p);
        }

        /// <summary>
        /// Enlists the command in the current ambient transaction by assigning the native
        /// <see cref="NpgsqlTransaction"/> to <see cref="NpgsqlCommand.Transaction"/>.
        /// Does nothing if no transaction has been set via <see cref="BaseCommand.SetTransaction"/>.
        /// </summary>
        private void AttachTransaction()
        {
            if (Transaction is NpgsqlAdapterTransaction nat)
                _cmd.Transaction = nat.GetNativeTransaction();
        }
    }
}
