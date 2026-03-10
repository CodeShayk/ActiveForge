using System;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    public class SqlAdapterTransaction : TransactionBase
    {
        private readonly SqlTransaction _tx;

        public SqlAdapterTransaction(SqlTransaction tx) { _tx = tx; }

        public SqlTransaction GetNativeTransaction() => _tx;

        public override void Commit()   => _tx.Commit();
        public override void Rollback() => _tx.Rollback();
        public override void Dispose()  => _tx.Dispose();
    }
}
