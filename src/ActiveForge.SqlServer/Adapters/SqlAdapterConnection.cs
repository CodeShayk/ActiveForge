using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    public class SqlAdapterConnection : ConnectionBase
    {
        private readonly SqlConnection _conn;

        public SqlAdapterConnection(string connectionString)
        {
            _conn = new SqlConnection(connectionString);
        }

        public override void   Open()           => _conn.Open();
        public override void   Close()          => _conn.Close();
        public override bool   IsConnected()    => _conn.State == ConnectionState.Open;
        public override string DatabaseName()   => _conn.Database;

        public override TransactionBase BeginTransaction(IsolationLevel level)
            => new SqlAdapterTransaction(_conn.BeginTransaction(level));

        public override CommandBase CreateCommand(string sql)
            => new SqlAdapterCommand(sql, this);

        public SqlConnection GetNativeConnection() => _conn;

        public override TransactionStates TransactionState(TransactionBase transaction)
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
