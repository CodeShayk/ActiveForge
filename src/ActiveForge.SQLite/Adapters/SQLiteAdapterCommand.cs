using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace ActiveForge.Adapters.SQLite
{
    /// <summary>
    /// SQLite implementation of <see cref="BaseCommand"/> backed by
    /// <see cref="SqliteCommand"/> from <c>Microsoft.Data.Sqlite</c>.
    /// <para>
    /// Each instance wraps a single <see cref="SqliteCommand"/> that is initialised at
    /// construction time. The command is bound to the native <see cref="SqliteConnection"/>
    /// extracted from the supplied <see cref="SQLiteAdapterConnection"/>, and its timeout
    /// is inherited from <see cref="BaseConnection.GetTimeout"/>.
    /// </para>
    /// <para>
    /// Provider-specific behaviour — TField unwrapping: <c>Microsoft.Data.Sqlite</c> is
    /// stricter than <c>Microsoft.Data.SqlClient</c> and rejects any non-primitive object
    /// as a parameter value. <see cref="AddNativeParameter"/> therefore unwraps
    /// <see cref="TField"/> wrappers in two steps: if the field is null
    /// (<c>TField.IsNull()</c> returns <see langword="true"/>) it substitutes
    /// <see cref="DBNull.Value"/>; otherwise it calls <c>TField.GetValue()</c> to obtain
    /// the underlying CLR primitive. This differs from the SQL Server and PostgreSQL
    /// adapters, which check only for a non-null <see cref="TField"/> reference.
    /// </para>
    /// <para>
    /// Stored procedures are not supported by SQLite; calling
    /// <see cref="SetToStoredProcedure"/> throws <see cref="NotSupportedException"/>.
    /// </para>
    /// </summary>
    public class SQLiteAdapterCommand : BaseCommand
    {
        /// <summary>The underlying SQLite command object managed by this adapter.</summary>
        private SqliteCommand _cmd;

        /// <summary>
        /// Initialises a new <see cref="SQLiteAdapterCommand"/> for the given SQL text and
        /// connection. The underlying <see cref="SqliteCommand"/> is created immediately
        /// and its <c>CommandTimeout</c> is set from the connection's configured timeout.
        /// </summary>
        /// <param name="sql">The SQL text to execute. Stored-procedure names are not accepted; use inline SQL only.</param>
        /// <param name="connection">The <see cref="SQLiteAdapterConnection"/> through which the command will execute.</param>
        public SQLiteAdapterCommand(string sql, SQLiteAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        /// <summary>
        /// Creates the underlying <see cref="SqliteCommand"/>, binding it to the native
        /// <see cref="SqliteConnection"/> and applying the connection timeout.
        /// </summary>
        private void InitCommand()
        {
            _cmd = new SqliteCommand(SQL, ((SQLiteAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        /// <summary>
        /// Attempts to cancel an in-progress execution of the command.
        /// Delegates directly to <see cref="SqliteCommand.Cancel"/>.
        /// </summary>
        public override void Cancel() => _cmd.Cancel();

        /// <summary>
        /// Executes a non-query SQL statement (INSERT, UPDATE, DELETE, DDL, etc.) and
        /// returns the number of rows affected. Enlists the current ambient transaction
        /// before execution via <c>AttachTransaction</c>.
        /// </summary>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqliteException"/> is raised by the provider, wrapping
        /// the original exception with a <c>"SQLite error: …"</c> message prefix.
        /// </exception>
        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set using <see cref="CommandBehavior.Default"/> (random-access column
        /// reads). Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="SQLiteAdapterReader"/> wrapping the <see cref="SqliteDataReader"/>.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqliteException"/> is raised by the provider.
        /// </exception>
        public override BaseReader ExecuteReader()
        {
            AttachTransaction();
            try   { return new SQLiteAdapterReader(_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set using <see cref="CommandBehavior.SequentialAccess"/>. Enlists the
        /// current ambient transaction before execution.
        /// </summary>
        /// <returns>A <see cref="SQLiteAdapterReader"/> wrapping the <see cref="SqliteDataReader"/> in sequential-access mode.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqliteException"/> is raised by the provider.
        /// </exception>
        public override BaseReader ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new SQLiteAdapterReader(_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        /// <summary>
        /// Executes the command and returns the value of the first column of the first row.
        /// Commonly used with <c>last_insert_rowid()</c> to retrieve auto-increment values
        /// after an INSERT. Enlists the current ambient transaction before execution.
        /// </summary>
        /// <returns>
        /// The scalar result as an <see cref="object"/>, or <see langword="null"/> if the
        /// result set is empty.
        /// </returns>
        /// <exception cref="PersistenceException">
        /// Thrown when <see cref="SqliteException"/> is raised by the provider.
        /// </exception>
        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        /// <summary>
        /// SQLite does not support stored procedures. Calling this method always throws
        /// <see cref="NotSupportedException"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void SetToStoredProcedure()
            => throw new NotSupportedException("SQLite does not support stored procedures.");

        /// <summary>
        /// Disposes the underlying <see cref="SqliteCommand"/>, releasing all resources
        /// associated with it.
        /// </summary>
        public override void Dispose() => _cmd?.Dispose();

        /// <summary>
        /// Adds a parameter to the underlying <see cref="SqliteCommand"/>.
        /// <para>
        /// <b>TField unwrapping (stricter than other providers):</b>
        /// <c>Microsoft.Data.Sqlite</c> requires CLR primitive types and rejects any
        /// object it does not recognise, including <see cref="TField"/> wrappers. This
        /// method therefore performs a two-stage unwrap: if <paramref name="value"/> is a
        /// <see cref="TField"/>, it checks <c>TField.IsNull()</c> and substitutes
        /// <see cref="DBNull.Value"/> for null fields, or calls <c>TField.GetValue()</c>
        /// for non-null fields to obtain the underlying CLR primitive. This is more
        /// conservative than the SQL Server / PostgreSQL adapters, which only check
        /// whether the reference is non-null before calling <c>GetValue()</c>.
        /// </para>
        /// <para>
        /// <b>No size constraint:</b> unlike the SQL Server and PostgreSQL adapters,
        /// this implementation does not set a size on the <see cref="SqliteParameter"/>
        /// because SQLite uses dynamic typing and does not enforce column-level length
        /// constraints.
        /// </para>
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text (e.g. <c>@name</c> or <c>$name</c>).</param>
        /// <param name="value">
        /// The parameter value, already normalised to <see cref="DBNull.Value"/> when
        /// logically null by the base-class caller. Any <see cref="TField"/> wrapper is
        /// unwrapped here with null-awareness.
        /// </param>
        /// <param name="info">
        /// Optional field metadata. Accepted but not used by this implementation because
        /// SQLite uses dynamic typing.
        /// </param>
        protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
        {
            // Microsoft.Data.Sqlite requires CLR primitive types — unwrap TField wrappers.
            if (value is TField tf)
                value = tf.IsNull() ? (object)DBNull.Value : tf.GetValue();

            var p = new SqliteParameter(name, value ?? DBNull.Value);
            _cmd.Parameters.Add(p);
        }

        /// <summary>
        /// Enlists the command in the current ambient transaction by assigning the native
        /// <see cref="SqliteTransaction"/> to <see cref="SqliteCommand.Transaction"/>.
        /// Does nothing if no transaction has been set via <see cref="BaseCommand.SetTransaction"/>.
        /// </summary>
        private void AttachTransaction()
        {
            if (Transaction is SQLiteAdapterTransaction sat)
                _cmd.Transaction = sat.GetNativeTransaction();
        }
    }
}
