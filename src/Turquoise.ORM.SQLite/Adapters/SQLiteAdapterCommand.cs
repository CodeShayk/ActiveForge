using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Turquoise.ORM.Adapters.SQLite
{
    public class SQLiteAdapterCommand : CommandBase
    {
        private SqliteCommand _cmd;

        public SQLiteAdapterCommand(string sql, SQLiteAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        private void InitCommand()
        {
            _cmd = new SqliteCommand(SQL, ((SQLiteAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        public override void Cancel() => _cmd.Cancel();

        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteReader()
        {
            AttachTransaction();
            try   { return new SQLiteAdapterReader(_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new SQLiteAdapterReader(_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (SqliteException ex) { throw new PersistenceException($"SQLite error: {ex.Message}", ex); }
        }

        public override void SetToStoredProcedure()
            => throw new NotSupportedException("SQLite does not support stored procedures.");

        public override void Dispose() => _cmd?.Dispose();

        protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
        {
            // Microsoft.Data.Sqlite requires CLR primitive types — unwrap TField wrappers.
            if (value is TField tf)
                value = tf.IsNull() ? (object)DBNull.Value : tf.GetValue();

            var p = new SqliteParameter(name, value ?? DBNull.Value);
            _cmd.Parameters.Add(p);
        }

        private void AttachTransaction()
        {
            if (Transaction is SQLiteAdapterTransaction sat)
                _cmd.Transaction = sat.GetNativeTransaction();
        }
    }
}
