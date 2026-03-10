using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using ActiveForge.Adapters.SqlServer;

namespace ActiveForge
{
    /// <summary>
    /// SQL Server dialect implementation of <see cref="DBDataConnection"/>.
    /// Supplies the provider-specific primitives: parameter mark, quote characters,
    /// row limiting, identity retrieval via SCOPE_IDENTITY(), and schema introspection
    /// via the SYSOBJECTS/SYSCOLUMNS/SYSTYPES system views.
    /// </summary>
    public class SqlServerConnection : DBDataConnection
    {
        // ── Static schema cache (shared across instances for the same connect string) ──

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SqlFieldCacheEntry>> _nativeTypesCache
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, SqlFieldCacheEntry>>(StringComparer.OrdinalIgnoreCase);

        // ── Construction ──────────────────────────────────────────────────────────────

        public SqlServerConnection(string connectString) : base(connectString) { }

        public SqlServerConnection(string connectString, FactoryBase factory) : base(connectString, factory) { }

        // ── Dialect ───────────────────────────────────────────────────────────────────

        public override string GetParameterMark()          => "@";
        public override string GetLeftNameQuote()          => "[";
        public override string GetRightNameQuote()         => "]";
        public override string GetSourceNameSeparator()    => ".";
        public override string GetUpdateLock()             => "WITH (UPDLOCK)";
        public override bool   IsAutoIdentity()            => true;
        public override string GetStringConnectionOperator() => "+";
        public override string GetGeneratorOperator(TargetFieldInfo info) => "";

        public override string CreateConcatenateOperator(params string[] parts)
            => string.Join(GetStringConnectionOperator(), parts);

        public override string LimitRowCount(int count, string fieldsAndFrom)
            => $"SELECT TOP {count} {fieldsAndFrom}";

        public override string PreInsertIdentityCommand(string sourceName)
            => $"SET IDENTITY_INSERT {QuoteName(sourceName)} ON";

        public override string PostInsertIdentityCommand(string sourceName)
            => $"SET IDENTITY_INSERT {QuoteName(sourceName)} OFF";

        // ── Connection factory ────────────────────────────────────────────────────────

        protected override ConnectionBase CreateConnection(string connectString)
            => new SqlAdapterConnection(connectString);

        // ── Identity retrieval ────────────────────────────────────────────────────────

        /// <summary>
        /// Reads SCOPE_IDENTITY() after the first INSERT and assigns the new identity
        /// value to the identity field declared on <paramref name="obj"/>.
        /// Called by <see cref="DBDataConnection.Insert"/> after the first table insert.
        /// </summary>
        protected override string PopulateIdentity(Record obj, RecordBinding binding, CommandBase _)
        {
            // @@IDENTITY is connection-scoped and works even when the INSERT was executed
            // via sp_executesql (which ADO.NET uses for parameterized queries), unlike
            // SCOPE_IDENTITY() which is limited to the current scope.
            var cmd = CreateCommand("SELECT @@IDENTITY");
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

            object raw = cmd.ExecuteScalar();
            if (raw == null || raw == DBNull.Value) return "";

            string id = raw.ToString();

            foreach (var fb in binding.UpdateFields)
            {
                if (!fb.Info.IsIdentity) continue;

                // TField subclasses accept the raw value via SetValue which handles conversion internally
                fb.Info.SetValue(obj, raw);
                break;
            }

            return id;
        }

        // ── Schema introspection ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns schema metadata for a single column, populating the full table's
        /// metadata into cache on first access.
        /// </summary>
        public override TargetFieldInfo GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName)
        {
            TargetFieldInfo info = GetTargetFieldInfoFromCache(fullClassName, fieldName);
            if (info != null) return info;

            lock (_syncRoot)
            {
                info = GetTargetFieldInfoFromCache(fullClassName, fieldName);
                if (info != null) return info;

                Connect();
                LoadTableSchemaIntoCache(fullClassName, sourceName);
                return GetTargetFieldInfoFromCache(fullClassName, fieldName);
            }
        }

        /// <summary>Returns schema metadata for all columns in a table.</summary>
        public override List<TargetFieldInfo> GetTargetFieldInfo(string sourceName)
        {
            lock (_syncRoot)
            {
                Connect();
                var result     = new List<TargetFieldInfo>();
                var fieldCache = PopulateSQLFieldCache();
                var sqlConn    = ((SqlAdapterConnection)_connection).GetNativeConnection();

                var adapter = new SqlDataAdapter($"SELECT TOP 1 * FROM [{sourceName}]", sqlConn);
                var dataSet = new DataSet();
                AttachTransaction(adapter.SelectCommand);

                adapter.FillSchema(dataSet, SchemaType.Mapped, sourceName);
                var table = dataSet.Tables[sourceName];
                if (table == null) return result;

                var pks = PrimaryKeyFields(sourceName);
                foreach (DataColumn col in table.Columns)
                {
                    var tfi = BuildTargetFieldInfoFromSchema(col, sourceName, pks, fieldCache);
                    if (tfi != null) result.Add(tfi);
                }
                return result;
            }
        }

        /// <summary>
        /// Checks the schema cache to determine whether a table mapped by
        /// <paramref name="obj"/>'s type exists in the database.
        /// </summary>
        public override bool TableExists(RecordBase obj)
        {
            var cache  = PopulateSQLFieldCache();
            string src = RecordMetaDataCache.GetTypeMetaData(obj.GetType()).SourceName;
            string pfx = src + " ";
            foreach (var key in cache.Keys)
                if (key.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Flushes both the binding cache and the native type schema cache.</summary>
        public override void FlushSchema()
        {
            base.FlushSchema();
            _nativeTypesCache.TryRemove(_connectString, out _);
        }

        // ── SQL field-cache helpers ───────────────────────────────────────────────────

        private void LoadTableSchemaIntoCache(string fullClassName, string sourceName)
        {
            var fieldCache = PopulateSQLFieldCache();
            var sqlConn    = ((SqlAdapterConnection)_connection).GetNativeConnection();

            // Strip UDF parameter list (e.g. "dbo.fn_Foo(@p1)" → "dbo.fn_Foo")
            string cleanName = sourceName.Contains("(")
                ? sourceName.Substring(0, sourceName.IndexOf('('))
                : sourceName;

            var adapter = new SqlDataAdapter($"SELECT TOP 1 * FROM [{cleanName}]", sqlConn);
            var dataSet = new DataSet();
            AttachTransaction(adapter.SelectCommand);

            adapter.FillSchema(dataSet, SchemaType.Mapped, cleanName);
            var table = dataSet.Tables[cleanName];
            if (table == null) return;

            var pks = PrimaryKeyFields(cleanName);
            foreach (DataColumn col in table.Columns)
            {
                var tfi = BuildTargetFieldInfoFromSchema(col, cleanName, pks, fieldCache);
                if (tfi != null)
                    AddTargetFieldInfoToCache(fullClassName, col.ColumnName, tfi);
            }
        }

        private TargetFieldInfo BuildTargetFieldInfoFromSchema(
            DataColumn col,
            string sourceName,
            List<string> pks,
            ConcurrentDictionary<string, SqlFieldCacheEntry> fieldCache)
        {
            var tfi = new TargetFieldInfo
            {
                TargetName = col.ColumnName,
                SourceName = sourceName,
                TargetType = col.DataType,
                IsIdentity = col.AutoIncrement,
                MaxLength  = col.MaxLength,
                IsNullable = col.AllowDBNull,
                IsLarge    = col.MaxLength > 65536,
            };

            // PK membership
            if (pks.Count > 0)
            {
                foreach (var pk in pks)
                    if (string.Equals(col.ColumnName, pk, StringComparison.OrdinalIgnoreCase))
                        tfi.IsInPK = true;
            }
            else
            {
                foreach (DataColumn pkCol in col.Table.PrimaryKey)
                    if (col == pkCol) tfi.IsInPK = true;
            }

            // Native type details from the schema cache
            string key = sourceName + " " + col.ColumnName;
            if (fieldCache.TryGetValue(key, out var entry))
            {
                tfi.NativeTargetType = entry.NativeType;
                tfi.Length           = entry.Length;
                tfi.Scale            = entry.Scale;
                tfi.Precision        = entry.Precision;
            }

            return tfi;
        }

        /// <summary>
        /// Lazily populates and returns a connection-string-keyed cache of every
        /// column in every user table/view/function, populated in a single query
        /// against SYSOBJECTS / SYSCOLUMNS / SYSTYPES.
        /// </summary>
        private ConcurrentDictionary<string, SqlFieldCacheEntry> PopulateSQLFieldCache()
        {
            if (_nativeTypesCache.TryGetValue(_connectString, out var existing))
                return existing;

            lock (_syncRoot)
            {
                if (_nativeTypesCache.TryGetValue(_connectString, out existing))
                    return existing;

                Connect();

                const string sql =
                    "SELECT DISTINCT SYSOBJECTS.NAME AS tablename, SYSCOLUMNS.name AS columnname, " +
                    "SYSTYPES.name AS typename, SYSCOLUMNS.length AS length, " +
                    "SYSCOLUMNS.isnullable AS isnullable, " +
                    "SYSCOLUMNS.prec AS [precision], SYSCOLUMNS.scale AS scale " +
                    "FROM SYSOBJECTS " +
                    "INNER JOIN SYSCOLUMNS ON SYSCOLUMNS.ID = SYSOBJECTS.ID " +
                    "INNER JOIN SYSTYPES ON SYSCOLUMNS.xtype = SYSTYPES.xtype " +
                    "  AND SYSTYPES.xtype = SYSTYPES.xusertype " +
                    "WHERE SYSOBJECTS.TYPE IN ('U','V','TF','IF')";

                var lookup = new ConcurrentDictionary<string, SqlFieldCacheEntry>(StringComparer.OrdinalIgnoreCase);
                var cmd    = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string tableName  = reader.ColumnValue("tablename")?.ToString()  ?? "";
                    string colName    = reader.ColumnValue("columnname")?.ToString() ?? "";
                    string nativeType = reader.ColumnValue("typename")?.ToString()   ?? "";

                    if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(colName)) continue;
                    if (string.Equals(nativeType, "sysname", StringComparison.OrdinalIgnoreCase)) continue;

                    bool nullable = reader.ColumnValue("isnullable")?.ToString() == "1";
                    int.TryParse(reader.ColumnValue("precision")?.ToString(), out int precision);
                    int.TryParse(reader.ColumnValue("scale")?.ToString(), out int scale);
                    int.TryParse(reader.ColumnValue("length")?.ToString(), out int length);

                    string cacheKey = tableName + " " + colName;
                    lookup.TryAdd(cacheKey,
                        new SqlFieldCacheEntry(tableName, colName, nativeType, nullable, length, precision, scale));
                }

                _nativeTypesCache[_connectString] = lookup;
                return lookup;
            }
        }

        /// <summary>
        /// Returns the primary-key column names for a table by calling <c>sp_pkeys</c>.
        /// </summary>
        private List<string> PrimaryKeyFields(string sourceName)
        {
            var result  = new List<string>();
            var sqlConn = ((SqlAdapterConnection)_connection).GetNativeConnection();

            var adapter = new SqlDataAdapter("sp_pkeys", sqlConn);
            adapter.SelectCommand.CommandType = CommandType.StoredProcedure;
            adapter.SelectCommand.Parameters.Add(new SqlParameter("@table_name", SqlDbType.VarChar, 120));

            AttachTransaction(adapter.SelectCommand);

            adapter.SelectCommand.Parameters["@table_name"].Value = sourceName.Trim();

            var keySet = new DataSet();
            try
            {
                adapter.Fill(keySet, sourceName);
            }
            catch (Exception e)
            {
                throw new PersistenceException($"Could not determine primary key fields for {sourceName}", e);
            }

            var keyTable = keySet.Tables[sourceName];
            if (keyTable != null)
                foreach (DataRow row in keyTable.Rows)
                    result.Add(row["COLUMN_NAME"].ToString());

            return result;
        }

        // ── Type mapping ──────────────────────────────────────────────────────────────

        /// <summary>Maps a SQL Server native type name to the corresponding CLR type.</summary>
        public Type MapNativeType(string nativeType)
        {
            return (nativeType ?? "").ToLowerInvariant() switch
            {
                "binary" or "image" or "timestamp" or "varbinary"
                    => typeof(byte[]),
                "char" or "nchar" or "ntext" or "nvarchar" or "text" or "varchar"
                    => typeof(string),
                "datetime" or "smalldatetime"
                    => typeof(DateTime),
                "bigint" or "decimal" or "float" or "int" or "money"
                    or "numeric" or "real" or "smallint" or "smallmoney" or "tinyint"
                    => typeof(decimal),
                "bit"
                    => typeof(bool),
                "uniqueidentifier"
                    => typeof(Guid),
                _   => null
            };
        }

        // ── Utility ───────────────────────────────────────────────────────────────────

        private void AttachTransaction(SqlCommand cmd)
        {
            if (_transactionDepth > 0 && _transaction is SqlAdapterTransaction sat)
                cmd.Transaction = sat.GetNativeTransaction();
        }

        // ── Inner type: SQL field cache entry ─────────────────────────────────────────

        /// <summary>Cached native-type metadata for a single database column.</summary>
        public sealed class SqlFieldCacheEntry
        {
            public string TableName  { get; }
            public string ColumnName { get; }
            public string NativeType { get; }
            public bool   IsNullable { get; }
            public int    Length     { get; }
            public int    Precision  { get; }
            public int    Scale      { get; }

            public SqlFieldCacheEntry(
                string tableName, string columnName, string nativeType,
                bool nullable, int length, int precision, int scale)
            {
                TableName  = tableName;
                ColumnName = columnName;
                NativeType = nativeType;
                IsNullable = nullable;
                Precision  = precision;
                Scale      = scale;
                // SQL Server reports nchar/nvarchar byte lengths; divide by 2 for char count
                Length = (nativeType.ToLowerInvariant() is "nvarchar" or "nchar") && length != -1
                    ? length / 2
                    : length;
            }
        }
    }
}
