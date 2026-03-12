using System;
using System.Collections.Generic;
using System.Data;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class that wraps a provider-specific database command.
    /// Decouples the ORM engine from any concrete ADO.NET provider by exposing a
    /// uniform command API. Concrete subclasses (e.g. <c>SqlAdapterCommand</c>,
    /// <c>NpgsqlAdapterCommand</c>, <c>SQLiteAdapterCommand</c>) delegate each
    /// operation to the underlying native command object.
    /// </summary>
    public abstract class BaseCommand : IDisposable
    {
        /// <summary>
        /// Represents a single bound parameter that will be passed to the database command.
        /// Instances are accumulated by <see cref="AddParameter(string, object, TargetFieldInfo)"/>
        /// and forwarded to the provider via <see cref="AddNativeParameter"/>.
        /// </summary>
        public class Parameter
        {
            /// <summary>Gets or sets the parameter name as it appears in the SQL text (e.g. <c>@ProductID</c>).</summary>
            public string Name  { get; set; }

            /// <summary>Gets or sets the parameter value. <see cref="DBNull.Value"/> is stored when the logical value is <see langword="null"/>.</summary>
            public object Value { get; set; }

            /// <summary>Gets or sets the <see cref="System.Data.DbType"/> inferred from the CLR type of the mapped field.</summary>
            public DbType DbType { get; set; }

            /// <summary>Gets or sets the maximum byte/character length of the parameter value, used for sized types such as <see cref="string"/>.</summary>
            public int    Size   { get; set; }
        }

        /// <summary>The SQL text (or stored-procedure name) that this command will execute.</summary>
        protected string        SQL;

        /// <summary>The connection through which this command will be executed.</summary>
        protected BaseConnection Connection;

        /// <summary>
        /// The ambient transaction to enlist in, or <see langword="null"/> when no transaction is active.
        /// Set by <see cref="SetTransaction"/>.
        /// </summary>
        protected BaseTransaction Transaction;

        /// <summary>The ordered list of parameters that have been added via <see cref="AddParameter(string, object, TargetFieldInfo)"/>.</summary>
        protected List<Parameter> _parameters = new List<Parameter>();

        /// <summary>
        /// Initialises the base state shared by all provider-specific command adapters.
        /// </summary>
        /// <param name="sql">The SQL text (or stored-procedure name) to execute.</param>
        /// <param name="connection">The connection adapter that owns this command.</param>
        protected BaseCommand(string sql, BaseConnection connection)
        {
            SQL        = sql;
            Connection = connection;
        }

        /// <summary>
        /// Attempts to cancel the execution of the command.
        /// The exact behaviour is provider-specific; some providers silently ignore the
        /// request when no command is in flight.
        /// </summary>
        public abstract void Cancel();

        /// <summary>
        /// Executes a statement that does not return rows (INSERT, UPDATE, DELETE, DDL, etc.)
        /// and returns the number of rows affected.
        /// </summary>
        /// <returns>The number of rows affected by the command.</returns>
        /// <exception cref="PersistenceException">Wraps any provider-level database exception.</exception>
        public abstract int ExecuteNonQuery();

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set in default (random-access) mode.
        /// </summary>
        /// <returns>A <see cref="BaseReader"/> positioned before the first row.</returns>
        /// <exception cref="PersistenceException">Wraps any provider-level database exception.</exception>
        public abstract BaseReader ExecuteReader();

        /// <summary>
        /// Executes the command and returns a <see cref="BaseReader"/> that streams the
        /// result set in sequential-access mode, which may improve performance for large
        /// result sets by avoiding buffering of column data.
        /// </summary>
        /// <returns>A <see cref="BaseReader"/> positioned before the first row in sequential-access mode.</returns>
        /// <exception cref="PersistenceException">Wraps any provider-level database exception.</exception>
        public abstract BaseReader ExecuteSequentialReader();

        /// <summary>
        /// Executes the command and returns the value of the first column of the first row
        /// in the result set. All other columns and rows are ignored.
        /// Commonly used to retrieve identity values or aggregate results.
        /// </summary>
        /// <returns>The scalar value, or <see langword="null"/> if the result set is empty.</returns>
        /// <exception cref="PersistenceException">Wraps any provider-level database exception.</exception>
        public abstract object ExecuteScalar();

        /// <summary>
        /// Switches the command to stored-procedure mode.
        /// After this call <see cref="SQL"/> is treated as a stored-procedure name rather
        /// than an inline SQL statement.
        /// Providers that do not support stored procedures (e.g. SQLite) throw
        /// <see cref="NotSupportedException"/>.
        /// </summary>
        public abstract void SetToStoredProcedure();

        /// <summary>
        /// Releases all resources held by the command, including the underlying native
        /// command object.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Associates a database transaction with this command so that subsequent execution
        /// calls participate in that transaction.
        /// </summary>
        /// <param name="tx">
        /// The <see cref="BaseTransaction"/> to enlist in, or <see langword="null"/> to
        /// remove the current transaction association.
        /// </param>
        public void SetTransaction(BaseTransaction tx) => Transaction = tx;

        /// <summary>
        /// Adds a named parameter to the command using only a name and value.
        /// Delegates to <see cref="AddParameter(string, object, TargetFieldInfo)"/> with
        /// a <see langword="null"/> <see cref="TargetFieldInfo"/>, so no size or type
        /// metadata is applied.
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text (e.g. <c>@Name</c>).</param>
        /// <param name="value">
        /// The parameter value. <see langword="null"/> is automatically converted to
        /// <see cref="DBNull.Value"/>.
        /// </param>
        public virtual void AddParameter(string name, object value)
            => AddParameter(name, value, null);

        /// <summary>
        /// Adds a named parameter to the command with full field-metadata support.
        /// The parameter is stored in <see cref="_parameters"/> and forwarded to the
        /// provider via <see cref="AddNativeParameter"/>. <see langword="null"/> values
        /// are replaced with <see cref="DBNull.Value"/> before storage and forwarding.
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text (e.g. <c>@Name</c>).</param>
        /// <param name="value">
        /// The parameter value. <see langword="null"/> is automatically converted to
        /// <see cref="DBNull.Value"/>.
        /// </param>
        /// <param name="info">
        /// Optional metadata about the mapped ORM field (CLR type, maximum length, etc.).
        /// Used by <see cref="MapDbType"/> to resolve the <see cref="DbType"/> and by
        /// provider implementations to set parameter size constraints.
        /// Pass <see langword="null"/> when metadata is unavailable.
        /// </param>
        public virtual void AddParameter(string name, object value, TargetFieldInfo info)
        {
            _parameters.Add(new Parameter
            {
                Name  = name,
                Value = value ?? DBNull.Value,
                DbType = MapDbType(info?.TargetType)
            });
            AddNativeParameter(name, value ?? DBNull.Value, info);
        }

        /// <summary>
        /// Provider-specific implementation that adds a parameter to the underlying native
        /// command object (e.g. <c>SqlCommand.Parameters</c>).
        /// Implementations must unwrap any <see cref="TField"/> wrapper before passing the
        /// value to the ADO.NET layer, because some providers (notably
        /// <c>Microsoft.Data.Sqlite</c>) reject non-primitive types.
        /// </summary>
        /// <param name="name">The parameter name as it appears in the SQL text.</param>
        /// <param name="value">
        /// The parameter value (already normalised to <see cref="DBNull.Value"/> when
        /// logically null by the caller).
        /// </param>
        /// <param name="info">
        /// Optional field metadata. May be <see langword="null"/> when the caller passes
        /// no metadata.
        /// </param>
        protected abstract void AddNativeParameter(string name, object value, TargetFieldInfo info);

        /// <summary>
        /// Maps a CLR type to the corresponding <see cref="DbType"/> enumeration value.
        /// Returns <see cref="DbType.Object"/> for any type that is not explicitly recognised.
        /// </summary>
        /// <param name="clrType">
        /// The CLR <see cref="Type"/> to map, or <see langword="null"/> to receive
        /// <see cref="DbType.Object"/> immediately.
        /// </param>
        /// <returns>The <see cref="DbType"/> that best represents <paramref name="clrType"/>.</returns>
        protected virtual DbType MapDbType(Type clrType)
        {
            if (clrType == null) return DbType.Object;
            return clrType switch
            {
                _ when clrType == typeof(int)      => DbType.Int32,
                _ when clrType == typeof(long)     => DbType.Int64,
                _ when clrType == typeof(short)    => DbType.Int16,
                _ when clrType == typeof(byte)     => DbType.Byte,
                _ when clrType == typeof(string)   => DbType.String,
                _ when clrType == typeof(bool)     => DbType.Boolean,
                _ when clrType == typeof(DateTime) => DbType.DateTime,
                _ when clrType == typeof(decimal)  => DbType.Decimal,
                _ when clrType == typeof(double)   => DbType.Double,
                _ when clrType == typeof(float)    => DbType.Single,
                _ when clrType == typeof(Guid)     => DbType.Guid,
                _ when clrType == typeof(byte[])   => DbType.Binary,
                _ => DbType.Object
            };
        }
    }
}
