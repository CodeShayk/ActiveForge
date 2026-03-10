using System;
using System.Collections.Generic;
using System.Data;

namespace ActiveForge
{
    /// <summary>Wraps a provider-specific database command.</summary>
    public abstract class CommandBase : IDisposable
    {
        public class Parameter
        {
            public string Name  { get; set; }
            public object Value { get; set; }
            public DbType DbType { get; set; }
            public int    Size   { get; set; }
        }

        protected string        SQL;
        protected ConnectionBase Connection;
        protected TransactionBase Transaction;
        protected List<Parameter> _parameters = new List<Parameter>();

        protected CommandBase(string sql, ConnectionBase connection)
        {
            SQL        = sql;
            Connection = connection;
        }

        public abstract void       Cancel();
        public abstract int        ExecuteNonQuery();
        public abstract ReaderBase ExecuteReader();
        public abstract ReaderBase ExecuteSequentialReader();
        public abstract object     ExecuteScalar();
        public abstract void       SetToStoredProcedure();
        public abstract void       Dispose();

        public void SetTransaction(TransactionBase tx) => Transaction = tx;

        public virtual void AddParameter(string name, object value)
            => AddParameter(name, value, null);

        public virtual void AddParameter(string name, object value, TargetFieldInfo info)
        {
            _parameters.Add(new Parameter
            {
                Name  = name,
                Value = value ?? DBNull.Value,
                DbType = MapDbType(info?.TargetType)
            });
            AddNativeParameter(name, value ?? DBNull.Value, info);
        }

        protected abstract void AddNativeParameter(string name, object value, TargetFieldInfo info);

        protected virtual DbType MapDbType(Type clrType)
        {
            if (clrType == null) return DbType.Object;
            return clrType switch
            {
                _ when clrType == typeof(int)      => DbType.Int32,
                _ when clrType == typeof(long)     => DbType.Int64,
                _ when clrType == typeof(short)    => DbType.Int16,
                _ when clrType == typeof(byte)     => DbType.Byte,
                _ when clrType == typeof(string)   => DbType.String,
                _ when clrType == typeof(bool)     => DbType.Boolean,
                _ when clrType == typeof(DateTime) => DbType.DateTime,
                _ when clrType == typeof(decimal)  => DbType.Decimal,
                _ when clrType == typeof(double)   => DbType.Double,
                _ when clrType == typeof(float)    => DbType.Single,
                _ when clrType == typeof(Guid)     => DbType.Guid,
                _ when clrType == typeof(byte[])   => DbType.Binary,
                _ => DbType.Object
            };
        }
    }
}
