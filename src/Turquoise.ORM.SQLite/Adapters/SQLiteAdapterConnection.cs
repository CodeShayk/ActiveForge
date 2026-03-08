using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Turquoise.ORM.Adapters.SQLite
{
    public class SQLiteAdapterConnection : ConnectionBase
    {
        private readonly SqliteConnection _conn;

        public SQLiteAdapterConnection(string connectionString)
        {
            _conn = new SqliteConnection(connectionString);
        }

        public override void   Open()           => _conn.Open();
        public override void   Close()          => _conn.Close();
        public override bool   IsConnected()    => _conn.State == ConnectionState.Open;
        public override string DatabaseName()   => _conn.Database;

        public override TransactionBase BeginTransaction(IsolationLevel level)
        {
            // Microsoft.Data.Sqlite supports ReadCommitted and Serializable only.
            // Map unsupported levels to the nearest supported equivalent.
            var sqliteLevel = level switch
            {
                IsolationLevel.ReadUncommitted => IsolationLevel.ReadCommitted,
                IsolationLevel.RepeatableRead  => IsolationLevel.Serializable,
                IsolationLevel.Snapshot        => IsolationLevel.Serializable,
                _                              => level
            };
            return new SQLiteAdapterTransaction(_conn.BeginTransaction(sqliteLevel));
        }

        public override CommandBase CreateCommand(string sql)
            => new SQLiteAdapterCommand(sql, this);

        public SqliteConnection GetNativeConnection() => _conn;

        public override TransactionStates TransactionState(TransactionBase transaction)
        {
            // SQLite transactions are always committable when present.
            return transaction is SQLiteAdapterTransaction
                ? TransactionStates.CommittableTransaction
                : TransactionStates.NoTransaction;
        }
    }
}
