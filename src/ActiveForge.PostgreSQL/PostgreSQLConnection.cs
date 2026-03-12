using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using NpgsqlTypes;
using ActiveForge.Adapters.PostgreSQL;

namespace ActiveForge
{
    /// <summary>
    /// PostgreSQL dialect implementation of <see cref="DBDataConnection"/>.
    /// Supplies provider-specific primitives: parameter mark, quote characters,
    /// row limiting via LIMIT, identity retrieval via LASTVAL(), and schema
    /// introspection via <c>information_schema</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Naming:</b> PostgreSQL folds unquoted identifiers to lower-case.
    /// Entity <c>[Table]</c> and <c>[Column]</c> attribute values should therefore
    /// use lower-case names, or the underlying tables must be created with quoted
    /// identifiers.</para>
    ///
    /// <para><b>Row locking:</b> PostgreSQL uses a <c>FOR UPDATE</c> clause at the
    /// end of a SELECT rather than an inline table hint.  Because <see cref="DBDataConnection"/>
    /// places the value returned by <see cref="GetUpdateLock"/> immediately after the
    /// table name, this method returns an empty string.  Use explicit transactions with
    /// <c>FOR UPDATE</c> in raw SQL when advisory locking is required.</para>
    ///
    /// <para><b>Identity insert:</b> PostgreSQL does not have an equivalent of
    /// <c>SET IDENTITY_INSERT … ON/OFF</c>.  You can insert explicit values into
    /// <c>SERIAL</c> / <c>GENERATED … AS IDENTITY</c> columns at any time without
    /// any special command; the base-class implementations return empty strings.</para>
    /// </remarks>
    public class PostgreSQLConnection : DBDataConnection
    {
        // ── Static schema cache ───────────────────────────────────────────────────────

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PgFieldCacheEntry>> _nativeTypesCache
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, PgFieldCacheEntry>>(StringComparer.OrdinalIgnoreCase);

        // ── Construction ──────────────────────────────────────────────────────────────

        public PostgreSQLConnection(string connectString) : base(connectString) { }

        public PostgreSQLConnection(string connectString, FactoryBase factory) : base(connectString, factory) { }

        // ── Dialect ───────────────────────────────────────────────────────────────────

        /// <summary>Named parameters use <c>@name</c> with Npgsql.</summary>
        public override string GetParameterMark()          => "@";

        /// <summary>PostgreSQL quotes identifiers with double-quotes.</summary>
        public override string GetLeftNameQuote()          => "\"";
        public override string GetRightNameQuote()         => "\"";
        public override string GetSourceNameSeparator()    => ".";

        /// <summary>
        /// Returns an empty string. PostgreSQL row locking uses <c>FOR UPDATE</c>
        /// at the end of a SELECT, not an inline table hint.
        /// </summary>
        public override string GetUpdateLock()             => "";

        public override bool   IsAutoIdentity()            => true;

        /// <summary>PostgreSQL concatenation operator is <c>||</c>.</summary>
        public override string GetStringConnectionOperator() => "||";

        public override string GetGeneratorOperator(TargetFieldInfo info) => "";

        public override string CreateConcatenateOperator(params string[] parts)
            => string.Join(GetStringConnectionOperator(), parts);

        /// <summary>
        /// Returns the SELECT stub unchanged; the row limit is appended after ORDER BY
        /// via <see cref="GetPageSuffix"/> so that LIMIT always follows ORDER BY.
        /// </summary>
        public override string LimitRowCount(int count, string fieldsAndFrom)
            => $"SELECT {fieldsAndFrom}";

        /// <summary>
        /// Returns a <c>LIMIT count [OFFSET start]</c> clause appended after the full
        /// WHERE and ORDER BY SQL.  PostgreSQL handles pagination entirely as a suffix.
        /// </summary>
        protected override string GetPageSuffix(int start, int count)
        {
            if (count <= 0 || count >= int.MaxValue) return "";
            return start > 0 ? $" LIMIT {count} OFFSET {start}" : $" LIMIT {count}";
        }

        // ── Connection factory ────────────────────────────────────────────────────────

        protected override ConnectionBase CreateConnection(string connectString)
            => new NpgsqlAdapterConnection(connectString);

        // ── Identity retrieval ────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the last inserted sequence value for the current session via
        /// <c>SELECT LASTVAL()</c> and writes it back to the identity field on
        /// <paramref name="obj"/>.
        /// </summary>
        /// <remarks>
        /// <c>LASTVAL()</c> is session-scoped and returns the value most recently
        /// obtained from any sequence in the current session.  It requires that at
        /// least one sequence-advancing statement (INSERT into a SERIAL / IDENTITY
        /// column, or explicit <c>nextval()</c>) has been issued in the current session.
        /// </remarks>
        protected override string PopulateIdentity(Record obj, RecordBinding binding, CommandBase _)
        {
            var cmd = CreateCommand("SELECT LASTVAL()");
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

            object raw = cmd.ExecuteScalar();
            if (raw == null || raw == DBNull.Value) return "";

            string id = raw.ToString();

            foreach (var fb in binding.UpdateFields)
            {
                if (!fb.Info.IsIdentity) continue;
                fb.Info.SetValue(obj, raw);
                break;
            }

            return id;
        }

        protected override string GetReadForUpdateSQL(Record obj, RecordBinding binding, List<FieldBinding> fieldBindingSubset, FieldSubset fieldSubset)
        {
            var fields   = new System.Text.StringBuilder();
            string criteria = "";
            string joins  = GetJoinSQL(binding, fieldSubset, true);

            foreach (var fb in fieldBindingSubset)
            {
                var tfi  = fb.Info;
                var node = fb.MapNode;
                if (node == null) continue;

                if (tfi.IsInPK && binding.UseAsPK(tfi) && binding.UpdateTableAliases.Contains(node.Alias))
                {
                    if (criteria.Length > 0) criteria += " AND ";
                    if (node.Alias.Length > 0)
                        criteria += node.Alias + GetSourceNameSeparator();
                    criteria += QuoteName(tfi.TargetName) + "=" + GetParameterMark() + tfi.TargetName;
                }
                else
                {
                    if (fields.Length > 0) fields.Append(',');
                    if (node.Alias.Length > 0)
                        fields.Append(node.Alias).Append(GetSourceNameSeparator());
                    fields.Append(QuoteName(tfi.TargetName));
                    if (fb.Alias.Length > 0)
                        fields.Append(' ').Append(fb.Alias);
                }
            }

            if (fields.Length == 0) fields.Append('*');
            return $"SELECT {fields} FROM {ResolveFullyQualifiedName(binding.SourceName, binding.Function)} {binding.GetRootAlias()}{joins} WHERE {criteria} FOR UPDATE";
        }

        // ── Schema introspection ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns schema metadata for a single column, loading the full table schema
        /// into cache from <c>information_schema</c> on first access.
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
                var pgConn     = ((NpgsqlAdapterConnection)_connection).GetNativeConnection();

                // Use DataAdapter.FillSchema to obtain ADO.NET DataColumn metadata.
                var adapter = new NpgsqlDataAdapter($"SELECT * FROM \"{sourceName}\" LIMIT 1", pgConn);
                if (_transactionDepth > 0 && _transaction is NpgsqlAdapterTransaction nat)
                    adapter.SelectCommand.Transaction = nat.GetNativeTransaction();

                var dataSet = new DataSet();
                adapter.FillSchema(dataSet, SchemaType.Mapped);

                var table = dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
                if (table == null) return result;

                var pks = PrimaryKeyFields(sourceName, pgConn);
                foreach (DataColumn col in table.Columns)
                {
                    var tfi = BuildTargetFieldInfoFromSchema(col, sourceName, pks, fieldCache);
                    if (tfi != null) result.Add(tfi);
                }

                return result;
            }
        }

        /// <summary>
        /// Checks the schema cache to determine whether a table exists in the database.
        /// </summary>
        public override bool TableExists(RecordBase obj)
        {
            var cache  = PopulateSQLFieldCache();
            string src = RecordMetaDataCache.GetTypeMetaData(obj.GetType()).SourceName;
            string pfx = src.ToLowerInvariant() + " ";
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

        // ── Schema-cache helpers ──────────────────────────────────────────────────────

        private void LoadTableSchemaIntoCache(string fullClassName, string sourceName)
        {
            var fieldCache = PopulateSQLFieldCache();
            var pgConn     = ((NpgsqlAdapterConnection)_connection).GetNativeConnection();

            // Strip function parameter list if present (e.g. "schema.fn_Foo(@p)" → "schema.fn_Foo")
            string cleanName = sourceName.Contains("(")
                ? sourceName.Substring(0, sourceName.IndexOf('('))
                : sourceName;

            var adapter = new NpgsqlDataAdapter($"SELECT * FROM \"{cleanName}\" LIMIT 1", pgConn);
            if (_transactionDepth > 0 && _transaction is NpgsqlAdapterTransaction nat)
                adapter.SelectCommand.Transaction = nat.GetNativeTransaction();

            var dataSet = new DataSet();
            adapter.FillSchema(dataSet, SchemaType.Mapped);

            var table = dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
            if (table == null) return;

            var pks = PrimaryKeyFields(cleanName, pgConn);
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
            ConcurrentDictionary<string, PgFieldCacheEntry> fieldCache)
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

            // Native type details from the information_schema cache
            string key = sourceName.ToLowerInvariant() + " " + col.ColumnName.ToLowerInvariant();
            if (fieldCache.TryGetValue(key, out var entry))
            {
                tfi.NativeTargetType = entry.NativeType;
                tfi.Length           = entry.Length;
                tfi.Scale            = entry.Scale;
                tfi.Precision        = entry.Precision;
                // SERIAL / GENERATED AS IDENTITY columns have a nextval() default
                if (entry.IsSerial) tfi.IsIdentity = true;
            }

            return tfi;
        }

        /// <summary>
        /// Lazily populates and returns a connection-string-keyed cache of every column
        /// in every user table and view, built from a single query against
        /// <c>information_schema.columns</c>.
        /// </summary>
        private ConcurrentDictionary<string, PgFieldCacheEntry> PopulateSQLFieldCache()
        {
            if (_nativeTypesCache.TryGetValue(_connectString, out var existing))
                return existing;

            lock (_syncRoot)
            {
                if (_nativeTypesCache.TryGetValue(_connectString, out existing))
                    return existing;

                Connect();

                const string sql =
                    "SELECT c.table_name, c.column_name, c.data_type, " +
                    "       c.character_maximum_length, c.is_nullable, " +
                    "       c.numeric_precision, c.numeric_scale, c.column_default " +
                    "FROM information_schema.columns c " +
                    "JOIN information_schema.tables t " +
                    "     ON t.table_name = c.table_name AND t.table_schema = c.table_schema " +
                    "WHERE c.table_schema = 'public' " +
                    "AND   t.table_type IN ('BASE TABLE', 'VIEW') " +
                    "ORDER BY c.table_name, c.ordinal_position";

                var lookup = new ConcurrentDictionary<string, PgFieldCacheEntry>(StringComparer.OrdinalIgnoreCase);
                var cmd    = CreateCommand(sql);
                if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string tableName  = reader.ColumnValue("table_name")?.ToString() ?? "";
                    string colName    = reader.ColumnValue("column_name")?.ToString() ?? "";
                    string nativeType = reader.ColumnValue("data_type")?.ToString() ?? "";

                    bool nullable      = !string.Equals(reader.ColumnValue("is_nullable")?.ToString(), "NO",
                                            StringComparison.OrdinalIgnoreCase);
                    string colDefault  = reader.ColumnValue("column_default")?.ToString() ?? "";
                    bool isSerial      = colDefault.StartsWith("nextval", StringComparison.OrdinalIgnoreCase);

                    int.TryParse(reader.ColumnValue("character_maximum_length")?.ToString(), out int length);
                    int.TryParse(reader.ColumnValue("numeric_precision")?.ToString(), out int precision);
                    int.TryParse(reader.ColumnValue("numeric_scale")?.ToString(), out int scale);

                    // Cache key is lower-cased; lookup is OrdinalIgnoreCase so mixed-case works too.
                    string cacheKey = tableName.ToLowerInvariant() + " " + colName.ToLowerInvariant();
                    lookup.TryAdd(cacheKey,
                        new PgFieldCacheEntry(tableName, colName, nativeType, nullable, isSerial, length, precision, scale));
                }

                _nativeTypesCache[_connectString] = lookup;
                return lookup;
            }
        }

        /// <summary>
        /// Returns the primary-key column names for a table by querying
        /// <c>information_schema.table_constraints</c> and <c>key_column_usage</c>.
        /// </summary>
        private List<string> PrimaryKeyFields(string sourceName, NpgsqlConnection pgConn)
        {
            var result = new List<string>();

            const string sql =
                "SELECT kcu.column_name " +
                "FROM information_schema.table_constraints tc " +
                "JOIN information_schema.key_column_usage kcu " +
                "     ON tc.constraint_name = kcu.constraint_name " +
                "     AND tc.table_schema   = kcu.table_schema " +
                "WHERE tc.constraint_type = 'PRIMARY KEY' " +
                "AND   LOWER(tc.table_name) = LOWER(@table_name) " +
                "AND   tc.table_schema = 'public' " +
                "ORDER BY kcu.ordinal_position";

            try
            {
                using var cmd = new NpgsqlCommand(sql, pgConn);
                cmd.Parameters.AddWithValue("table_name", sourceName.Trim());

                if (_transactionDepth > 0 && _transaction is NpgsqlAdapterTransaction nat)
                    cmd.Transaction = nat.GetNativeTransaction();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    result.Add(reader.GetString(0));
            }
            catch (Exception e)
            {
                throw new PersistenceException(
                    $"Could not determine primary key fields for {sourceName}", e);
            }

            return result;
        }

        // ── Type mapping ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps a PostgreSQL <c>information_schema</c> type name to the corresponding CLR type.
        /// </summary>
        public Type MapNativeType(string nativeType)
        {
            return (nativeType ?? "").ToLowerInvariant() switch
            {
                "bytea"
                    => typeof(byte[]),

                "character" or "character varying" or "text" or "name" or "citext"
                    => typeof(string),

                "timestamp without time zone" or "timestamp with time zone" or "timestamp"
                    => typeof(DateTime),

                "date"
                    => typeof(DateTime),

                "time without time zone" or "time with time zone" or "time"
                    => typeof(TimeSpan),

                "integer" or "bigint" or "smallint" or "numeric" or "decimal"
                    or "real" or "double precision" or "money" or "oid"
                    => typeof(decimal),

                "boolean"
                    => typeof(bool),

                "uuid"
                    => typeof(Guid),

                "interval"
                    => typeof(TimeSpan),

                _   => null
            };
        }

        // ── Inner type: PostgreSQL field cache entry ───────────────────────────────────

        /// <summary>Cached native-type metadata for a single PostgreSQL database column.</summary>
        public sealed class PgFieldCacheEntry
        {
            public string TableName  { get; }
            public string ColumnName { get; }
            public string NativeType { get; }
            public bool   IsNullable { get; }
            /// <summary>True when the column default is a <c>nextval()</c> call (SERIAL / IDENTITY).</summary>
            public bool   IsSerial   { get; }
            public int    Length     { get; }
            public int    Precision  { get; }
            public int    Scale      { get; }

            public PgFieldCacheEntry(
                string tableName, string columnName, string nativeType,
                bool nullable, bool isSerial, int length, int precision, int scale)
            {
                TableName  = tableName;
                ColumnName = columnName;
                NativeType = nativeType;
                IsNullable = nullable;
                IsSerial   = isSerial;
                Length     = length;
                Precision  = precision;
                Scale      = scale;
            }
        }
    }
}
