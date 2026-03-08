using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Turquoise.ORM.Adapters.SqlServer
{
    public class SqlAdapterCommand : CommandBase
    {
        private SqlCommand _cmd;

        public SqlAdapterCommand(string sql, SqlAdapterConnection connection)
            : base(sql, connection)
        {
            InitCommand();
        }

        private void InitCommand()
        {
            _cmd = new SqlCommand(SQL, ((SqlAdapterConnection)Connection).GetNativeConnection())
            {
                CommandTimeout = Connection.GetTimeout()
            };
        }

        public override void Cancel() => _cmd.Cancel();

        public override int ExecuteNonQuery()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteNonQuery(); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteReader()
        {
            AttachTransaction();
            try   { return new SqlAdapterReader(_cmd.ExecuteReader(CommandBehavior.Default)); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        public override ReaderBase ExecuteSequentialReader()
        {
            AttachTransaction();
            try   { return new SqlAdapterReader(_cmd.ExecuteReader(CommandBehavior.SequentialAccess)); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        public override object ExecuteScalar()
        {
            AttachTransaction();
            try   { return _cmd.ExecuteScalar(); }
            catch (SqlException ex) { throw new PersistenceException($"SQL error: {ex.Message}", ex); }
        }

        public override void SetToStoredProcedure() => _cmd.CommandType = CommandType.StoredProcedure;

        public override void Dispose() => _cmd?.Dispose();

        protected override void AddNativeParameter(string name, object value, TargetFieldInfo info)
        {
            var p = new SqlParameter(name, value ?? DBNull.Value);
            if (info?.MaxLength > 0 && info.TargetType == typeof(string))
                p.Size = info.MaxLength;
            _cmd.Parameters.Add(p);
        }

        private void AttachTransaction()
        {
            if (Transaction is SqlAdapterTransaction sat)
                _cmd.Transaction = sat.GetNativeTransaction();
        }
    }
}
