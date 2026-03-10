using System;
using System.Data;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    public class NpgsqlAdapterConnection : ConnectionBase
    {
        private readonly NpgsqlConnection _conn;

        public NpgsqlAdapterConnection(string connectionString)
        {
            _conn = new NpgsqlConnection(connectionString);
        }

        public override void   Open()           => _conn.Open();
        public override void   Close()          => _conn.Close();
        public override bool   IsConnected()    => _conn.State == ConnectionState.Open;
        public override string DatabaseName()   => _conn.Database;

        public override TransactionBase BeginTransaction(IsolationLevel level)
            => new NpgsqlAdapterTransaction(_conn.BeginTransaction(level));

        public override CommandBase CreateCommand(string sql)
            => new NpgsqlAdapterCommand(sql, this);

        public NpgsqlConnection GetNativeConnection() => _conn;

        public override TransactionStates TransactionState(TransactionBase transaction)
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
