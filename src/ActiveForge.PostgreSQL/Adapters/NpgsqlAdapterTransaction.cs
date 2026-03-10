using System;
using Npgsql;

namespace ActiveForge.Adapters.PostgreSQL
{
    public class NpgsqlAdapterTransaction : TransactionBase
    {
        private readonly NpgsqlTransaction _tx;

        public NpgsqlAdapterTransaction(NpgsqlTransaction tx) { _tx = tx; }

        public NpgsqlTransaction GetNativeTransaction() => _tx;

        public override void Commit()   => _tx.Commit();
        public override void Rollback() => _tx.Rollback();
        public override void Dispose()  => _tx.Dispose();
    }
}
