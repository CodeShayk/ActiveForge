using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using ActiveForge.MongoDB.Internal;
using ActiveForge.Query;

namespace ActiveForge
{
    /// <summary>
    /// ActiveForge DataConnection implementation for MongoDB.
    /// Provides the standard ORM CRUD and query API against a MongoDB database.
    /// Extends <see cref="DataConnection"/> directly (not <c>DBDataConnection</c>,
    /// which is SQL-specific).
    /// </summary>
    /// <remarks>
    /// Mapping conventions:
    /// <list type="bullet">
    ///   <item><description><c>[Table("name")]</c> → MongoDB collection name.</description></item>
    ///   <item><description><c>[Column("name")]</c> → BSON field name.</description></item>
    ///   <item><description><c>[Identity]</c> field → BSON <c>_id</c> (stored as int32).</description></item>
    /// </list>
    /// MongoDB does not support SQL-specific operations.  <see cref="ExecSQL(Record,string)"/>
    /// and <see cref="ExecStoredProcedure"/> throw <see cref="NotSupportedException"/>.
    /// </remarks>
    public class MongoDataConnection : DataConnection, IDisposable
    {
        // ── State ─────────────────────────────────────────────────────────────────────

        // MongoClient owns the connection pool and is designed to be used as a singleton.
        // Sharing one client per connection string avoids socket exhaustion when many
        // MongoDataConnection instances are created with the same URI (e.g. in tests or
        // per-request scenarios without DI).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MongoClient>
            _clientPool = new System.Collections.Concurrent.ConcurrentDictionary<string, MongoClient>();

        private readonly string?        _connectionString;
        private readonly string         _databaseName;
        private readonly FactoryBase    _factory;
        private readonly ILogger        _logger;

        // When injected via DI, an external singleton MongoClient is provided; we must not dispose it.
        private readonly MongoClient?   _externalClient;

        private MongoClient?      _client;
        private IMongoDatabase?   _database;
        private IClientSessionHandle? _session;
        private bool              _inTransaction;

        // Action queue
        private readonly List<(Record obj, char op, QueryTerm? term)> _actionQueue
            = new List<(Record, char, QueryTerm?)>();

        // ── Constructors ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a connection using a MongoDB connection string.
        /// Each call to <see cref="Connect"/> creates a new <see cref="MongoClient"/>.
        /// Prefer <see cref="MongoDataConnection(MongoClient,string,FactoryBase,ILogger)"/> in DI scenarios
        /// so the singleton <see cref="MongoClient"/> (which owns the connection pool) is shared.
        /// </summary>
        /// <param name="connectionString">MongoDB connection string, e.g. <c>mongodb://localhost:27017</c>.</param>
        /// <param name="databaseName">Name of the MongoDB database to target.</param>
        /// <param name="factory">Optional polymorphic type factory. Pass <c>new FactoryBase()</c> for no mapping.</param>
        /// <param name="logger">Optional logger; <c>NullLogger</c> used when omitted.</param>
        public MongoDataConnection(
            string connectionString,
            string databaseName,
            FactoryBase? factory = null,
            ILogger? logger      = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _databaseName     = databaseName     ?? throw new ArgumentNullException(nameof(databaseName));
            _factory          = factory          ?? new FactoryBase();
            _logger           = logger           ?? NullLogger.Instance;
        }

        /// <summary>
        /// Creates a connection using a pre-built <see cref="MongoClient"/> singleton.
        /// Use this overload in DI registrations so that the connection-pool-owning client
        /// is shared across all scoped <see cref="MongoDataConnection"/> instances.
        /// The provided <paramref name="mongoClient"/> is <b>not</b> disposed when
        /// <see cref="Disconnect"/> is called.
        /// </summary>
        /// <param name="mongoClient">Singleton <see cref="MongoClient"/> managed externally.</param>
        /// <param name="databaseName">Name of the MongoDB database to target.</param>
        /// <param name="factory">Optional polymorphic type factory.</param>
        /// <param name="logger">Optional logger; <c>NullLogger</c> used when omitted.</param>
        public MongoDataConnection(
            MongoClient mongoClient,
            string databaseName,
            FactoryBase? factory = null,
            ILogger? logger      = null)
        {
            _externalClient   = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _databaseName     = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            _factory          = factory ?? new FactoryBase();
            _logger           = logger  ?? NullLogger.Instance;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        public override bool Connect()
        {
            if (_externalClient != null)
            {
                // DI path: use the injected singleton client; do not store it in _client so
                // Disconnect() does not null it out (it belongs to the container).
                _database = _externalClient.GetDatabase(_databaseName);
            }
            else
            {
                _client   = _clientPool.GetOrAdd(_connectionString!, cs => new MongoClient(cs));
                _database = _client.GetDatabase(_databaseName);
            }
            _logger.LogDebug("Connected to MongoDB database '{Database}'", _databaseName);
            return true;
        }

        public override bool Disconnect()
        {
            _session?.Dispose();
            _session = null;
            // Only null out the self-created client; the external/injected client is managed externally.
            _client   = null;
            _database = null;
            _logger.LogDebug("Disconnected from MongoDB database '{Database}'", _databaseName);
            return true;
        }

        public void Dispose() => Disconnect();

        public override bool IsOpen => _database != null;

        private IMongoDatabase Database
            => _database ?? throw new InvalidOperationException("Not connected. Call Connect() first.");

        private IMongoCollection<BsonDocument> GetCollection(string name)
            => Database.GetCollection<BsonDocument>(name);

        private IMongoCollection<BsonDocument> GetCollection(Record obj)
            => GetCollection(MongoTypeCache.GetEntry(obj.GetType()).CollectionName);

        // ── CRUD — Insert ─────────────────────────────────────────────────────────────

        public override bool Insert(Record obj) => RunWrite(() => InsertCore(obj));

        private bool InsertCore(Record obj)
        {
            var entry = MongoTypeCache.GetEntry(obj.GetType());
            var coll  = GetCollection(entry.CollectionName);

            // Determine if identity is auto-generated (Identity field is null or zero)
            bool autoId = entry.Identity != null &&
                          (((TField)entry.Identity.FieldInfo.GetValue(obj)!).IsNull());

            if (autoId)
            {
                // Use a counter document to simulate auto-increment
                int newId = GetNextId(entry.CollectionName);
                TField idField = (TField)entry.Identity!.FieldInfo.GetValue(obj)!;
                idField.SetValue(newId);
            }

            BsonDocument doc = MongoMapper.ToBsonDocument(obj);

            if (_inTransaction && _session != null)
                coll.InsertOne(_session, doc);
            else
                coll.InsertOne(doc);

            _logger.LogDebug("Inserted into '{Collection}'", entry.CollectionName);
            return true;
        }

        // ── CRUD — Delete ─────────────────────────────────────────────────────────────

        public override bool Delete(Record obj) => RunWrite(() => DeleteCore(obj));

        private bool DeleteCore(Record obj)
        {
            var coll   = GetCollection(obj);
            var filter = BuildPkFilter(obj);

            DeleteResult result = _inTransaction && _session != null
                ? coll.DeleteOne(_session, filter)
                : coll.DeleteOne(filter);

            return result.DeletedCount > 0;
        }

        public override bool Delete(Record obj, QueryTerm term) => RunWrite(() => DeleteTermCore(obj, term));

        private bool DeleteTermCore(Record obj, QueryTerm term)
        {
            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);

            if (_inTransaction && _session != null)
                coll.DeleteMany(_session, filter);
            else
                coll.DeleteMany(filter);

            return true;
        }

        internal override void Delete(Record obj, QueryTerm term, Type[] concreteTypes)
            => Delete(obj, term);

        // ── CRUD — Update ─────────────────────────────────────────────────────────────

        internal override FieldSubset Update(Record obj, RecordLock.UpdateOption option)
            => UpdateInternal(obj);

        internal override FieldSubset UpdateAll(Record obj)
            => UpdateInternal(obj);

        internal override FieldSubset UpdateChanged(Record obj)
            => UpdateInternal(obj);

        private FieldSubset UpdateInternal(Record obj) => RunWrite(() => UpdateCore(obj));

        private FieldSubset UpdateCore(Record obj)
        {
            var coll    = GetCollection(obj);
            var filter  = BuildPkFilter(obj);
            var entry   = MongoTypeCache.GetEntry(obj.GetType());

            var setDoc = new BsonDocument();
            foreach (var fd in entry.Fields)
            {
                if (fd.IsIdentity) continue;
                TField? f = fd.FieldInfo.GetValue(obj) as TField;
                if (f == null || f.IsNull()) continue;
                object? v = f.GetValue();
                setDoc[fd.BsonName] = MongoMapper.ClrToBson(v);
            }

            var update = new BsonDocument("$set", setDoc);

            if (_inTransaction && _session != null)
                coll.UpdateOne(_session, filter, update);
            else
                coll.UpdateOne(filter, update);

            return new global::ActiveForge.FieldSubset(obj, global::ActiveForge.FieldSubset.InitialState.IncludeAll, null);
        }

        // ── CRUD — Read ───────────────────────────────────────────────────────────────

        public override bool Read(Record obj)
            => Read(obj, null);

        public override bool Read(Record obj, FieldSubset? fieldSubset)
        {
            var coll   = GetCollection(obj);
            var filter = BuildPkFilter(obj);

            BsonDocument? doc = _inTransaction && _session != null
                ? coll.Find(_session, filter).FirstOrDefault()
                : coll.Find(filter).FirstOrDefault();

            if (doc == null)
                throw new PersistenceException(
                    $"No document found with the specified primary key in collection '{coll.CollectionNamespace.CollectionName}'.");
            MongoMapper.FromBsonDocument(doc, obj);
            obj.SetLoaded(true);
            return true;
        }

        public override bool ReadForUpdate(Record obj, FieldSubset? fieldSubset)
            => Read(obj, fieldSubset);    // MongoDB: no advisory lock

        // ── QueryFirst ────────────────────────────────────────────────────────────────

        public override bool QueryFirst(Record obj, QueryTerm? term, SortOrder? sortOrder, FieldSubset? fieldSubset)
            => QueryFirst(obj, term, sortOrder, fieldSubset, null);

        public override bool QueryFirst(Record obj, QueryTerm? term, SortOrder? sortOrder, FieldSubset? fieldSubset, RecordParameterCollectionBase? objectParameters)
        {
            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj);

            var query = _inTransaction && _session != null
                ? coll.Find(_session, filter)
                : coll.Find(filter);

            if (sort != null) query = query.Sort(sort);

            BsonDocument? doc = query.Limit(1).FirstOrDefault();
            if (doc == null) return false;
            MongoMapper.FromBsonDocument(doc, obj);
            obj.SetLoaded(true);
            return true;
        }

        // ── QueryCount ────────────────────────────────────────────────────────────────

        public override int QueryCount(Record obj)
            => QueryCount(obj, null);

        public override int QueryCount(Record obj, QueryTerm? term)
            => QueryCount(obj, term, null, null);

        public override int QueryCount(Record obj, QueryTerm? term, Type[]? expectedTypes)
            => QueryCount(obj, term, expectedTypes, null);

        public override int QueryCount(Record obj, QueryTerm? term, Type[]? expectedTypes, FieldSubset? subsetIn)
        {
            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);

            long count = _inTransaction && _session != null
                ? coll.CountDocuments(_session, filter)
                : coll.CountDocuments(filter);

            return (int)count;
        }

        // ── QueryAll ──────────────────────────────────────────────────────────────────

        public override RecordCollection QueryAll(Record obj, QueryTerm? term, SortOrder? sortOrder, int pageSize, FieldSubset? fieldSubset)
            => QueryAll(obj, term, sortOrder, pageSize, null, fieldSubset);

        public override RecordCollection QueryAll(Record obj, QueryTerm? term, SortOrder? sortOrder, int pageSize, Type[]? expectedTypes, FieldSubset? fieldSubset)
            => QueryAll(obj, term, sortOrder, pageSize, expectedTypes, fieldSubset, null);

        public override RecordCollection QueryAll(Record obj, QueryTerm? term, SortOrder? sortOrder, int pageSize, Type[]? expectedTypes, FieldSubset? fieldSubset, Dictionary<Type, FieldSubset>? expectedTypeFieldSubsets)
        {
            // Auto-detect embedded Record fields and use $lookup aggregation when present
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType());
            if (joinStages.Count > 0)
                return QueryAll(obj, term, sortOrder, pageSize, fieldSubset, (IReadOnlyList<JoinOverride>)System.Array.Empty<JoinOverride>());

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj);

            var query = _inTransaction && _session != null
                ? coll.Find(_session, filter)
                : coll.Find(filter);

            if (sort != null) query = query.Sort(sort);
            if (pageSize > 0) query = query.Limit(pageSize);

            var result = new RecordCollection();
            foreach (BsonDocument doc in query.ToEnumerable())
            {
                Record instance = CreateFresh(obj.GetType());
                MongoMapper.FromBsonDocument(doc, instance);
                instance.SetLoaded(true);
                result.Add(instance);
            }
            return result;
        }

        // ── LazyQueryAll ──────────────────────────────────────────────────────────────

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm? term, SortOrder? sortOrder, int pageSize, FieldSubset? fieldSubset)
            => LazyQueryAll<T>(obj, term, sortOrder, pageSize, null, fieldSubset);

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm? term, SortOrder? sortOrder, int pageSize, Type[]? expectedTypes, FieldSubset? fieldSubset)
        {
            // Auto-detect embedded Record fields and use $lookup aggregation when present
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType());
            if (joinStages.Count > 0)
            {
                foreach (var item in LazyQueryAll<T>(obj, term, sortOrder, pageSize, fieldSubset, (IReadOnlyList<JoinOverride>)System.Array.Empty<JoinOverride>()))
                    yield return item;
                yield break;
            }

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj);

            var query = _inTransaction && _session != null
                ? coll.Find(_session, filter)
                : coll.Find(filter);

            if (sort != null) query = query.Sort(sort);
            if (pageSize > 0) query = query.Limit(pageSize);

            foreach (BsonDocument doc in query.ToEnumerable())
            {
                T instance = (T)CreateFresh(typeof(T));
                MongoMapper.FromBsonDocument(doc, instance);
                instance.SetLoaded(true);
                yield return instance;
            }
        }

        // ── QueryPage ─────────────────────────────────────────────────────────────────

        public override RecordCollection QueryPage(Record obj, QueryTerm? term, SortOrder? sortOrder, int start, int count, FieldSubset? fieldSubset)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, null, null, false);

        public override RecordCollection QueryPage(Record obj, QueryTerm? term, SortOrder? sortOrder, int start, int count, FieldSubset? fieldSubset, Type[]? expectedTypes)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, expectedTypes, null, false);

        public override RecordCollection QueryPage(Record obj, QueryTerm? term, SortOrder? sortOrder, int start, int count, FieldSubset? fieldSubset, Type[]? expectedTypes, Dictionary<Type, FieldSubset>? expectedTypeFieldSubsets)
            => QueryPage(obj, term, sortOrder, start, count, fieldSubset, expectedTypes, expectedTypeFieldSubsets, false);

        public override RecordCollection QueryPage(Record obj, QueryTerm? term, SortOrder? sortOrder, int start, int count, FieldSubset? fieldSubset, Type[]? expectedTypes, Dictionary<Type, FieldSubset>? expectedTypeFieldSubsets, bool returnCountInfo)
        {
            // Auto-detect embedded Record fields and use $lookup aggregation when present
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType());
            if (joinStages.Count > 0)
                return QueryPage(obj, term, sortOrder, start, count, fieldSubset, (IReadOnlyList<JoinOverride>)System.Array.Empty<JoinOverride>());

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj);

            var query = _inTransaction && _session != null
                ? coll.Find(_session, filter)
                : coll.Find(filter);

            if (sort   != null) query = query.Sort(sort);
            if (start  > 0)     query = query.Skip(start);
            if (count  > 0)     query = query.Limit(count);

            var result = new RecordCollection { StartRecord = start, PageSize = count };

            foreach (BsonDocument doc in query.ToEnumerable())
            {
                Record instance = CreateFresh(obj.GetType());
                MongoMapper.FromBsonDocument(doc, instance);
                instance.SetLoaded(true);
                result.Add(instance);
            }

            if (returnCountInfo)
            {
                long total = _inTransaction && _session != null
                    ? coll.CountDocuments(_session, filter)
                    : coll.CountDocuments(filter);
                result.TotalRowCount      = (int)total;
                result.TotalRowCountValid = true;
                result.IsMoreData         = (start + result.Count) < total;
            }

            return result;
        }

        // ── Join-aware query overrides (uses $lookup aggregation) ────────────────────

        public override RecordCollection QueryAll(Record obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType(), joinOverrides);
            if (joinStages.Count == 0)
                return QueryAll(obj, term, sortOrder, pageSize, fieldSubset);

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj, joinStages);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj, joinStages);

            var agg = (_inTransaction && _session != null)
                ? coll.Aggregate(_session)
                : coll.Aggregate();

            // Apply lookups first so joined fields are available for filter/sort
            foreach (var stage in joinStages)
            {
                agg = agg.Lookup(stage.CollectionName, stage.LocalField, stage.ForeignField, stage.Alias);
                agg = agg.Unwind(stage.Alias,
                    new AggregateUnwindOptions<BsonDocument> { PreserveNullAndEmptyArrays = stage.IsLeftJoin });
            }
            agg = agg.Match(filter);
            if (sort   != null) agg = agg.Sort(sort);
            if (pageSize > 0)   agg = agg.Limit(pageSize);

            var result = new RecordCollection();
            foreach (var doc in agg.ToEnumerable())
            {
                Record instance = CreateFresh(obj.GetType());
                MongoMapper.FromBsonDocumentWithJoins(doc, instance, joinStages);
                instance.SetLoaded(true);
                result.Add(instance);
            }
            return result;
        }

        public override IEnumerable<T> LazyQueryAll<T>(T obj, QueryTerm term, SortOrder sortOrder, int pageSize, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType(), joinOverrides);
            if (joinStages.Count == 0)
            {
                foreach (var item in LazyQueryAll<T>(obj, term, sortOrder, pageSize, fieldSubset))
                    yield return item;
                yield break;
            }

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj, joinStages);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj, joinStages);

            var agg = (_inTransaction && _session != null)
                ? coll.Aggregate(_session)
                : coll.Aggregate();

            foreach (var stage in joinStages)
            {
                agg = agg.Lookup(stage.CollectionName, stage.LocalField, stage.ForeignField, stage.Alias);
                agg = agg.Unwind(stage.Alias,
                    new AggregateUnwindOptions<BsonDocument> { PreserveNullAndEmptyArrays = stage.IsLeftJoin });
            }
            agg = agg.Match(filter);
            if (sort   != null) agg = agg.Sort(sort);
            if (pageSize > 0)   agg = agg.Limit(pageSize);

            foreach (var doc in agg.ToEnumerable())
            {
                T instance = (T)CreateFresh(typeof(T));
                MongoMapper.FromBsonDocumentWithJoins(doc, instance, joinStages);
                instance.SetLoaded(true);
                yield return instance;
            }
        }

        public override RecordCollection QueryPage(Record obj, QueryTerm term, SortOrder sortOrder, int start, int count, FieldSubset fieldSubset, IReadOnlyList<JoinOverride> joinOverrides)
        {
            var joinStages = MongoJoinBuilder.BuildStages(obj.GetType(), joinOverrides);
            if (joinStages.Count == 0)
                return QueryPage(obj, term, sortOrder, start, count, fieldSubset);

            var coll   = GetCollection(obj);
            var filter = MongoQueryTranslator.Translate(term, obj, joinStages);
            var sort   = MongoQueryTranslator.TranslateSort(sortOrder, obj, joinStages);

            var agg = (_inTransaction && _session != null)
                ? coll.Aggregate(_session)
                : coll.Aggregate();

            foreach (var stage in joinStages)
            {
                agg = agg.Lookup(stage.CollectionName, stage.LocalField, stage.ForeignField, stage.Alias);
                agg = agg.Unwind(stage.Alias,
                    new AggregateUnwindOptions<BsonDocument> { PreserveNullAndEmptyArrays = stage.IsLeftJoin });
            }
            agg = agg.Match(filter);
            if (sort  != null) agg = agg.Sort(sort);
            if (start > 0)     agg = agg.Skip(start);
            if (count > 0)     agg = agg.Limit(count);

            var result = new RecordCollection { StartRecord = start, PageSize = count };
            foreach (var doc in agg.ToEnumerable())
            {
                Record instance = CreateFresh(obj.GetType());
                MongoMapper.FromBsonDocumentWithJoins(doc, instance, joinStages);
                instance.SetLoaded(true);
                result.Add(instance);
            }
            return result;
        }

        // ── ExecSQL / ExecStoredProcedure — not supported ─────────────────────────────

        public override RecordCollection ExecSQL(Record obj, string sql)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection. Use the MongoDB.Driver API directly.");

        public override RecordCollection ExecSQL(Record obj, string sqlFormat, params object[] values)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override RecordCollection ExecSQL(Record obj, string sql, Dictionary<string, object> parameters)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override RecordCollection ExecSQL(Record obj, string sql, int start, int count)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override RecordCollection ExecSQL(Record obj, string sql, int start, int count, Dictionary<string, object> parameters)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override ReaderBase ExecSQL(string sql)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override ReaderBase ExecSQL(string sql, Dictionary<string, CommandBase.Parameter> parameters)
            => throw new NotSupportedException("ExecSQL is not supported by MongoDataConnection.");

        public override RecordCollection ExecStoredProcedure(Record obj, string spName, int start, int count, params Record.SPParameter[] spParameters)
            => throw new NotSupportedException("Stored procedures are not supported by MongoDataConnection.");

        internal override QueryFragment GenerateExistsSQLQuery(Record obj, string outerAlias, string outerFieldName, TField linkField, ref int termNumber, QueryTerm? term, SortOrder? sortOrder, int start, int count, FieldSubset? fieldSubset, Type[]? expectedTypes, Dictionary<Type, FieldSubset>? expectedTypeFieldSubsets)
            => throw new NotSupportedException("ExistsTerm sub-queries are not supported by MongoDataConnection.");

        // ── Action queue ──────────────────────────────────────────────────────────────

        public override void QueueForInsert(Record obj) => _actionQueue.Add((obj, 'I', null));
        public override void QueueForUpdate(Record obj) => _actionQueue.Add((obj, 'U', null));
        public override void QueueForDelete(Record obj) => _actionQueue.Add((obj, 'D', null));
        public override void QueueForDelete(Record obj, QueryTerm term) => _actionQueue.Add((obj, 'Q', term));

        public override void ProcessActionQueue()
        {
            foreach (var (obj, op, term) in _actionQueue)
            {
                switch (op)
                {
                    case 'I': Insert(obj); break;
                    case 'U': UpdateInternal(obj); break;
                    case 'D': Delete(obj); break;
                    case 'Q': Delete(obj, term!); break;
                }
            }
            _actionQueue.Clear();
        }

        public override void ClearActionQueue() => _actionQueue.Clear();

        // ── Binding ───────────────────────────────────────────────────────────────────

        public override RecordBinding GetObjectBinding(RecordBase obj, bool targetExists, bool useCache)
            => MongoMapper.BuildMinimalObjectBinding((Record)obj);

        public override RecordBinding GetObjectBinding(RecordBase obj, bool targetExists, bool useCache, Type[]? expectedTypes)
            => MongoMapper.BuildMinimalObjectBinding((Record)obj);

        public override RecordBinding GetObjectBinding(RecordBase obj, bool targetExists, bool useCache, Type[]? expectedTypes, bool includeLookupDataObjects)
            => MongoMapper.BuildMinimalObjectBinding((Record)obj);

        public override RecordBinding GetChangedObjectBinding(RecordBase obj, RecordBase changedObj)
            => MongoMapper.BuildMinimalObjectBinding((Record)obj);

        public override RecordBinding GetDynamicObjectBinding(RecordBase obj, ReaderBase reader)
            => throw new NotSupportedException("GetDynamicObjectBinding is not supported by MongoDataConnection.");

        // ── Schema / field info ───────────────────────────────────────────────────────

        public override TargetFieldInfo GetTargetFieldInfo(string fullClassName, string sourceName, string fieldName)
            => throw new NotSupportedException("GetTargetFieldInfo by class/source is not supported by MongoDataConnection.");

        public override List<TargetFieldInfo> GetTargetFieldInfo(string sourceName)
            => throw new NotSupportedException("GetTargetFieldInfo by source is not supported by MongoDataConnection.");

        public override TargetFieldInfo? GetTargetFieldInfoFromCache(string sourceName, string targetFieldName)
            => null;

        public override void AddTargetFieldInfoToCache(string sourceName, string targetFieldName, TargetFieldInfo info)
        { /* no-op */ }

        public override bool TableExists(RecordBase obj)
        {
            var entry = MongoTypeCache.GetEntry(obj.GetType());
            var names = Database.ListCollectionNames().ToList();
            return names.Contains(entry.CollectionName);
        }

        // ── Dialect helpers ───────────────────────────────────────────────────────────

        public override string GetStringConnectionOperator()  => "";
        public override string GetParameterMark()             => "";
        public override string GetLeftNameQuote()             => "";
        public override string GetRightNameQuote()            => "";
        public override string GetSourceNameSeparator()       => ".";
        public override string GetUpdateLock()                => "";
        public override bool   IsAutoIdentity()               => true;
        public override string GetGeneratorOperator(TargetFieldInfo info) => "";

        public override string CreateConcatenateOperator(params string[] parts)
            => string.Join("", parts);

        // ── FieldSubset factories ─────────────────────────────────────────────────────

        public override Type MapType(Type generalization) => _factory.MapType(generalization);

        public override FieldSubset DefaultFieldSubset(Record rootObject)
            => new global::ActiveForge.FieldSubset(rootObject, global::ActiveForge.FieldSubset.InitialState.IncludeAll, null);

        public override FieldSubset FieldSubset(Record rootObject, global::ActiveForge.FieldSubset.InitialState state)
            => new global::ActiveForge.FieldSubset(rootObject, state, null);

        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, TField enclosed)
            => new global::ActiveForge.FieldSubset(rootObject, global::ActiveForge.FieldSubset.InitialState.ExcludeAll, null);

        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed)
            => new global::ActiveForge.FieldSubset(rootObject, global::ActiveForge.FieldSubset.InitialState.IncludeAll, null);

        public override FieldSubset FieldSubset(Record rootObject, Record enclosing, Record enclosed, global::ActiveForge.FieldSubset.InitialState state)
            => new global::ActiveForge.FieldSubset(rootObject, state, null);

        // ── Pre/post identity ─────────────────────────────────────────────────────────

        public override string PreInsertIdentityCommand(string sourceName)  => "";
        public override string PostInsertIdentityCommand(string sourceName) => "";

        // ── Object creation ───────────────────────────────────────────────────────────

        public override Record Create(Type type)
        {
            Type mapped = _factory.MapType(type);
            return (Record)Activator.CreateInstance(mapped, this)!;
        }

        public override Record Create(Type type, Record? owner, bool isTemplate = false)
        {
            Type mapped = _factory.MapType(type);
            try
            {
                return (Record)Activator.CreateInstance(mapped, this)!;
            }
            catch
            {
                return (Record)Activator.CreateInstance(mapped)!;
            }
        }

        // ── Transactions ──────────────────────────────────────────────────────────────

        public override TransactionBase BeginTransaction()
            => BeginTransaction(IsolationLevel.ReadCommitted);

        public override TransactionBase BeginTransaction(IsolationLevel level)
        {
            if (_database == null)
                throw new InvalidOperationException("Not connected. Call Connect() first.");

            var client = _externalClient ?? _client
                ?? throw new InvalidOperationException("No MongoClient available.");
            _session = client.StartSession();
            _session.StartTransaction();
            _inTransaction = true;

            _logger.LogDebug("MongoDB transaction started");
            return new MongoTransactionBase(_session, this);
        }

        public override void CommitTransaction(TransactionBase transaction)
        {
            if (_session == null || !_inTransaction)
                throw new InvalidOperationException("No active MongoDB transaction.");

            _session.CommitTransaction();
            _inTransaction = false;
            _session.Dispose();
            _session = null;
            _logger.LogDebug("MongoDB transaction committed");
        }

        public override void RollbackTransaction(TransactionBase transaction)
        {
            if (_session == null) return;

            _session.AbortTransaction();
            _inTransaction = false;
            _session.Dispose();
            _session = null;
            _logger.LogDebug("MongoDB transaction rolled back");
        }

        public override TransactionStates TransactionState(TransactionBase transaction)
        {
            if (transaction is MongoTransactionBase mt)
                return mt.State;
            return _inTransaction ? TransactionStates.Active : TransactionStates.None;
        }

        // ── Descriptions (no-op stubs) ────────────────────────────────────────────────

        public override string GetValidationMessage(string key, string defaultValue) => defaultValue;
        public override string GetFieldDescription(FieldInfo fi, RecordBase obj)     => fi.Name;
        public override string GetDataObjectDescription(RecordBase obj)               => obj.GetType().Name;

        // ── Private helpers ───────────────────────────────────────────────────────────

        private FilterDefinition<BsonDocument> BuildPkFilter(Record obj)
        {
            var entry = MongoTypeCache.GetEntry(obj.GetType());
            if (entry.Identity == null)
                throw new PersistenceException($"No [Identity] field on {obj.GetType().Name}. Cannot build primary key filter.");

            TField? idField = entry.Identity.FieldInfo.GetValue(obj) as TField;
            if (idField == null || idField.IsNull())
                throw new PersistenceException($"Identity field is null on {obj.GetType().Name}. Set the ID before calling this operation.");

            object? idValue = idField.GetValue();
            return Builders<BsonDocument>.Filter.Eq("_id", MongoMapper.ClrToBson(idValue));
        }

        private Record CreateFresh(Type type)
        {
            Type mapped = _factory.MapType(type);
            try
            {
                return (Record)Activator.CreateInstance(mapped, this)!;
            }
            catch
            {
                var obj = (Record)Activator.CreateInstance(mapped)!;
                obj.SetTarget(this);
                return obj;
            }
        }

        /// <summary>
        /// Simulates auto-increment by maintaining a counters collection.
        /// Thread-safe via findOneAndUpdate with $inc.
        /// </summary>
        private int GetNextId(string collectionName)
        {
            var counters = Database.GetCollection<BsonDocument>("__activeforge_counters");
            var filter   = Builders<BsonDocument>.Filter.Eq("_id", collectionName);
            var update   = Builders<BsonDocument>.Update.Inc("seq", 1);
            var options  = new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert       = true,
                ReturnDocument = ReturnDocument.After,
            };

            BsonDocument counter = counters.FindOneAndUpdate(filter, update, options);
            return counter["seq"].AsInt32;
        }
    }

    // ── MongoTransactionBase ──────────────────────────────────────────────────────────

    /// <summary>Wraps a MongoDB client session to implement the ORM TransactionBase contract.</summary>
    public sealed class MongoTransactionBase : TransactionBase
    {
        private readonly IClientSessionHandle _session;
        private readonly MongoDataConnection  _connection;
        internal TransactionStates State { get; private set; } = TransactionStates.Active;

        internal MongoTransactionBase(IClientSessionHandle session, MongoDataConnection connection)
        {
            _session    = session;
            _connection = connection;
        }

        public override void Commit()
        {
            _connection.CommitTransaction(this);
            State = TransactionStates.Committed;
        }

        public override void Rollback()
        {
            _connection.RollbackTransaction(this);
            State = TransactionStates.RolledBack;
        }

        public override void Dispose()
        {
            if (State == TransactionStates.Active)
                Rollback();
        }
    }
}
