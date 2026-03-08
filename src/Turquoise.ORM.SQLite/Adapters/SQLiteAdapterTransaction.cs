using System;
using Microsoft.Data.Sqlite;

namespace Turquoise.ORM.Adapters.SQLite
{
    public class SQLiteAdapterTransaction : TransactionBase
    {
        private readonly SqliteTransaction _tx;

        public SQLiteAdapterTransaction(SqliteTransaction tx) { _tx = tx; }

        public SqliteTransaction GetNativeTransaction() => _tx;

        public override void Commit()   => _tx.Commit();
        public override void Rollback() => _tx.Rollback();
        public override void Dispose()  => _tx.Dispose();
    }
}
