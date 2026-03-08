using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using Turquoise.ORM.Adapters.SQLite;

namespace Turquoise.ORM
{
    /// <summary>
    /// SQLite dialect implementation of <see cref="DBDataConnection"/>.
    /// Supplies the provider-specific primitives: parameter mark, quote characters,
    /// row limiting via <c>LIMIT N</c>, identity retrieval via <c>last_insert_rowid()</c>,
    /// and schema introspection via <c>PRAGMA table_info</c>.
    /// </summary>
    /// <remarks>
    /// In-memory databases are fully supported — use <c>Data Source=:memory:</c> as the
    /// connection string.  Note that in-memory databases are destroyed when the last
    /// connection to them closes, so keep the connection open for the lifetime of the test
    /// or use a named shared-cache connection string such as
    /// <c>Data Source=mydb;Mode=Memory;Cache=Shared</c>.
    /// </remarks>
    public class SQLiteConnection : DBDataConnection
    {
        // ── Static schema cache (shared across instances for the same connect string) ──

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SQLiteFieldCacheEntry>> _schemaCache
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, SQLiteFieldCacheEntry>>(StringComparer.OrdinalIgnoreCase);

        // ── Construction ──────────────────────────────────────────────────────────────

        public SQLiteConnection(string connectString) : base(connectString) { }

        public SQLiteConnection(string connectString, FactoryBase factory) : base(connectString, factory) { }

        // ── Dialect ───────────────────────────────────────────────────────────────────

        public override string GetParameterMark()            => "@";
        public override string GetLeftNameQuote()            => "\"";
        public override string GetRightNameQuote()           => "\"";
        public override string GetSourceNameSeparator()      => ".";
        public override string GetUpdateLock()               => "";          // SQLite has no row-level locks
        public override bool   IsAutoIdentity()              => true;
        public override string GetStringConnectionOperator() => "||";

        public override string GetGeneratorOperator(TargetFieldInfo info) => "";

        public override string CreateConcatenateOperator(params string[] parts)
            => string.Join(GetStringConnectionOperator(), parts);

        /// <summary>
        /// SQLite uses <c>LIMIT N</c> appended at the end of the query.
        /// The <paramref name="fieldsAndFrom"/> argument already contains the fields list
        /// and FROM clause; this method wraps it in a sub-select so that LIMIT applies
        /// correctly regardless of other clauses appended later.
        /// </summary>
        public override string LimitRowCount(int count, string fieldsAndFrom)
            => $"SELECT {fieldsAndFrom} LIMIT {count}";

        /// <summary>SQLite does not require IDENTITY_INSERT toggling.</summary>
        public override string PreInsertIdentityCommand(string sourceName)  => "";

        /// <summary>SQLite does not require IDENTITY_INSERT toggling.</summary>
        public override string PostInsertIdentityCommand(string sourceName) => "";

        // ── Connection factory ────────────────────────────────────────────────────────

        protected override ConnectionBase CreateConnection(string connectString)
            => new SQLiteAdapterConnection(connectString);

        // ── Identity retrieval ────────────────────────────────────────────────────────

        /// <summary>
        /// Reads <c>last_insert_rowid()</c> after an INSERT and assigns the new identity
        /// value to the identity field declared on <paramref name="obj"/>.
        /// </summary>
        protected override string PopulateIdentity(DataObject obj, ObjectBinding binding, CommandBase _)
        {
            var cmd = CreateCommand("SELECT last_insert_rowid()");
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
                return ReadPragmaTableInfo(sourceName, fullClassName: null, addToCache: false);
            }
        }

        /// <summary>
        /// Checks whether a table exists using <c>sqlite_master</c>.
        /// </summary>
        public override bool TableExists(ObjectBase obj)
        {
            string src = DataObjectMetaDataCache.GetTypeMetaData(obj.GetType()).SourceName;
            Connect();

            var cmd = CreateCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tbl");
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);
            cmd.AddParameter("@tbl", src, null);

            object result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt64(result) > 0;
        }

        /// <summary>Flushes both the binding cache and the schema cache.</summary>
        public override void FlushSchema()
        {
            base.FlushSchema();
            _schemaCache.TryRemove(_connectString, out _);
        }

        // ── Schema cache helpers ──────────────────────────────────────────────────────

        private void LoadTableSchemaIntoCache(string fullClassName, string sourceName)
        {
            var entries = ReadPragmaTableInfo(sourceName, fullClassName, addToCache: true);
            // entries are already added to cache inside ReadPragmaTableInfo when addToCache=true
        }

        /// <summary>
        /// Reads <c>PRAGMA table_info(sourceName)</c> and returns a list of
        /// <see cref="TargetFieldInfo"/> objects.  When <paramref name="addToCache"/> is
        /// <c>true</c>, each entry is also added to the binding cache under
        /// <paramref name="fullClassName"/>.
        /// </summary>
        private List<TargetFieldInfo> ReadPragmaTableInfo(
            string sourceName, string fullClassName, bool addToCache)
        {
            var result    = new List<TargetFieldInfo>();
            var fieldCache = GetOrBuildFieldCache(sourceName);

            var cmd = CreateCommand($"PRAGMA table_info(\"{sourceName}\")");
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string colName    = reader.ColumnValue("name").ToString();
                string typeName   = reader.ColumnValue("type").ToString();
                bool   notNull    = reader.ColumnValue("notnull").ToString() == "1";
                int    pkSeq      = Convert.ToInt32(reader.ColumnValue("pk"));

                var tfi = new TargetFieldInfo
                {
                    TargetName = colName,
                    SourceName = sourceName,
                    TargetType = MapNativeType(typeName),
                    IsNullable = !notNull,
                    IsInPK     = pkSeq > 0,
                    // INTEGER PRIMARY KEY in SQLite is the rowid alias — it auto-increments
                    IsIdentity = pkSeq == 1 &&
                                 string.Equals(typeName, "INTEGER", StringComparison.OrdinalIgnoreCase),
                };

                if (fieldCache.TryGetValue(colName, out var cached))
                {
                    tfi.NativeTargetType = cached.NativeType;
                    tfi.MaxLength        = cached.MaxLength;
                }

                result.Add(tfi);

                if (addToCache && fullClassName != null)
                    AddTargetFieldInfoToCache(fullClassName, colName, tfi);
            }

            return result;
        }

        /// <summary>
        /// Returns (or builds) a connection-string-keyed column type cache for the given
        /// table.  The cache is populated on first access by querying
        /// <c>PRAGMA table_info</c>.
        /// </summary>
        private ConcurrentDictionary<string, SQLiteFieldCacheEntry> GetOrBuildFieldCache(string sourceName)
        {
            var connCache = _schemaCache.GetOrAdd(_connectString,
                _ => new ConcurrentDictionary<string, SQLiteFieldCacheEntry>(StringComparer.OrdinalIgnoreCase));

            // Keys are stored as "tableName columnName"
            // If the table's columns are not yet in the cache, populate them.
            string sentinel = sourceName + " __loaded__";
            if (connCache.ContainsKey(sentinel))
                return connCache;

            var cmd = CreateCommand($"PRAGMA table_info(\"{sourceName}\")");
            if (_transactionDepth > 0) cmd.SetTransaction(_transaction);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string colName  = reader.ColumnValue("name").ToString();
                string typeName = reader.ColumnValue("type").ToString();
                int maxLen      = ParseMaxLength(typeName);
                string key      = sourceName + " " + colName;
                connCache.TryAdd(key, new SQLiteFieldCacheEntry(colName, typeName, maxLen));
            }

            connCache.TryAdd(sentinel, new SQLiteFieldCacheEntry("__loaded__", "", 0));
            return connCache;
        }

        private static int ParseMaxLength(string typeName)
        {
            // e.g. "VARCHAR(255)" → 255
            int start = typeName.IndexOf('(');
            int end   = typeName.IndexOf(')');
            if (start >= 0 && end > start)
            {
                if (int.TryParse(typeName.Substring(start + 1, end - start - 1), out int len))
                    return len;
            }
            return 0;
        }

        // ── Type mapping ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps a SQLite type affinity / declared type to the closest CLR type.
        /// SQLite uses type affinity, so a best-effort mapping is applied.
        /// </summary>
        public Type MapNativeType(string nativeType)
        {
            string upper = (nativeType ?? "").ToUpperInvariant();

            if (upper.Contains("INT"))                                     return typeof(long);
            if (upper.Contains("REAL") || upper.Contains("FLOA")
                                       || upper.Contains("DOUB"))         return typeof(double);
            if (upper.Contains("BLOB") || upper == "")                    return typeof(byte[]);
            if (upper.Contains("BOOL"))                                    return typeof(bool);
            if (upper.Contains("DATE") || upper.Contains("TIME"))         return typeof(DateTime);
            if (upper.Contains("GUID") || upper.Contains("UUID"))         return typeof(Guid);
            if (upper.Contains("NUM")  || upper.Contains("DEC")
                                       || upper.Contains("MONEY"))        return typeof(decimal);

            // TEXT and everything else → string
            return typeof(string);
        }

        // ── Inner type: SQLite field cache entry ──────────────────────────────────────

        /// <summary>Cached native-type metadata for a single database column.</summary>
        public sealed class SQLiteFieldCacheEntry
        {
            public string ColumnName { get; }
            public string NativeType { get; }
            public int    MaxLength  { get; }

            public SQLiteFieldCacheEntry(string columnName, string nativeType, int maxLength)
            {
                ColumnName = columnName;
                NativeType = nativeType;
                MaxLength  = maxLength;
            }
        }
    }
}
