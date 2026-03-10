using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ActiveForge.Adapters.SqlServer
{
    public class SqlAdapterReader : ReaderBase
    {
        private readonly SqlDataReader _reader;

        public SqlAdapterReader(SqlDataReader reader) { _reader = reader; }

        public override bool   Read()                  => _reader.Read();
        public override void   Close()                 => _reader.Close();
        public override void   Dispose()               => _reader.Dispose();
        public override object GetValue(int ordinal)   => _reader.GetValue(ordinal);
        public override bool   IsDBNull(int ordinal)   => _reader.IsDBNull(ordinal);
        public override int    GetOrdinal(string name) => _reader.GetOrdinal(name);
        public override int    FieldCount              => _reader.FieldCount;
        public override string GetName(int ordinal)    => _reader.GetName(ordinal);
        public override IDataRecord Record             => _reader;

        public SqlDataReader GetNativeReader() => _reader;
    }
}
