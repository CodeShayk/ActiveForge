using System;
using System.Data;

namespace ActiveForge
{
    /// <summary>
    /// Adapter interface over a native database connection.
    /// Decouples the ORM engine from a specific ADO.NET provider.
    /// </summary>
    public abstract class ConnectionBase
    {
        public enum TransactionStates
        {
            NoTransaction,
            CommittableTransaction,
            NonCommittableTransaction
        }

        private int _timeout;

        public abstract void Open();
        public abstract void Close();
        public abstract TransactionBase BeginTransaction(IsolationLevel level);
        public abstract CommandBase     CreateCommand(string sql);
        public abstract bool            IsConnected();
        public abstract string          DatabaseName();
        public abstract TransactionStates TransactionState(TransactionBase transaction);

        public int  GetTimeout()              => _timeout;
        public void SetTimeout(int seconds)   => _timeout = seconds;
    }
}
