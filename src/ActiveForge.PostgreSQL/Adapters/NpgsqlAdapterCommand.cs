using System;
using System.Data;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    public class NpgsqlAdapterCommand : CommandBase
    {
        private NpgsqlCommand _cmd;

        public NpgsqlAdapterCommand(string sql, NpgsqlAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        private void InitCommand()
        {
            _cmd = new NpgsqlCommand(SQL, ((NpgsqlAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        public override void Cancel() => _cmd.Cancel();

        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteReader()
        {
            AttachTransaction();
            try   { return new NpgsqlAdapterReader((NpgsqlDataReader)_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new NpgsqlAdapterReader((NpgsqlDataReader)_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (NpgsqlException ex) { throw new PersistenceException($"PostgreSQL error: {ex.Message}", ex); }
        }

        public override void SetToStoredProcedure() => _cmd.CommandType = CommandType.StoredProcedure;

        public override void Dispose() => _cmd?.Dispose();

        protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
        {
            // Unwrap TField wrappers to their underlying CLR values
            if (value is TField tf) value = tf.GetValue();
            var p = new NpgsqlParameter(name, value ?? DBNull.Value);
            if (info?.MaxLength > 0 && info.TargetType == typeof(string))
                p.Size = info.MaxLength;
            _cmd.Parameters.Add(p);
        }

        private void AttachTransaction()
        {
            if (Transaction is NpgsqlAdapterTransaction nat)
                _cmd.Transaction = nat.GetNativeTransaction();
        }
    }
}
