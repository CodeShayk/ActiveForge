using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using ActiveForge;
using ActiveForge.Attributes;
using Xunit;

namespace ActiveForge.PostgreSQL.Tests
{
    [Table("lock_products")]
    public sealed class PgLockProduct : IdentityRecord
    {
        [Column("name")]  public TString  Name  = new TString();
        [Column("price")] public TDecimal Price = new TDecimal();

        public PgLockProduct() { }
        public PgLockProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class PostgreSQLPessimisticLockingTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_lock_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string AdminConnStr =>
            Environment.GetEnvironmentVariable("PG_ADMIN_CONNSTR")
            ?? "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=Pa55w0rd";

        private string TestConnStr =>
            $"Host=localhost;Port=5455;Database={_dbName};Username=postgres;Password=Pa55w0rd;CommandTimeout=1;Pooling=false;";

        private readonly PostgreSQLConnection _connA;
        private readonly PostgreSQLConnection _connB;

        public PostgreSQLPessimisticLockingTests()
        {
            CreateDatabase();
            _connA = new PostgreSQLConnection(TestConnStr);
            _connA.Connect();
            CreateSchema();
            SeedData();

            _connB = new PostgreSQLConnection(TestConnStr);
            _connB.Connect();
            _connB.SetTimeout(1); // Force fail fast for blocked locks
        }

        public void Dispose()
        {
            _connA.Disconnect();
            _connB.Disconnect();
            DropDatabase();
        }

        [Fact]
        public void ReadForUpdate_BlocksConcurrentAccess_UntilCommit()
        {
            int targetId = 1;

            // Thread A starts a transaction and claims a lock using ReadForUpdate
            var txA = _connA.BeginTransaction();
            
            var pA = new PgLockProduct(_connA);
            pA.ID.SetValue(targetId);
            bool readRes = _connA.ReadForUpdate(pA, null);
            readRes.Should().BeTrue();

            // Thread B attempts to read the same record for update
            // Since Thread A holds the row lock, this should throw a Postgres timeout exception
            // Note: ReadForUpdate requires an active transaction.
            var pB = new PgLockProduct(_connB);
            pB.ID.SetValue(targetId);

            Action blockedRead = () =>
            {
                var txB = _connB.BeginTransaction();
                try
                {
                    _connB.ReadForUpdate(pB, null);
                }
                finally
                {
                    _connB.RollbackTransaction(txB);
                }
            };
            
            blockedRead.Should().Throw<PersistenceException>()
                       .Where(ex => ex.ToString().ToLower().Contains("timeout"));

            // Thread A commits the transaction, releasing the lock
            _connA.CommitTransaction(txA);

            // Now thread B should easily acquire the lock and read the record
            var txB = _connB.BeginTransaction();
            bool secondaryRes = _connB.ReadForUpdate(pB, null);
            secondaryRes.Should().BeTrue();
            _connB.CommitTransaction(txB);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CreateSchema()
        {
            using (_connA.ExecSQL(
                "CREATE TABLE lock_products (" +
                "  \"ID\"     SERIAL PRIMARY KEY," +
                "  name     VARCHAR(200) NOT NULL," +
                "  price    DECIMAL(18,4) NOT NULL DEFAULT 0" +
                ")")) { }
        }

        private void SeedData()
        {
            InsertProduct("Shared Item", 100m);
        }

        private int InsertProduct(string name, decimal price)
        {
            var p = new PgLockProduct(_connA);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.Insert();
            return (int)p.ID.GetValue();
        }

        private void CreateDatabase()
        {
            using var conn = new NpgsqlConnection(AdminConnStr);
            conn.Open();
            // Terminate existing connections and drop leftover DB from previous runs
            using (var killCmd = conn.CreateCommand())
            {
                killCmd.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{_dbName}'";
                killCmd.ExecuteNonQuery();
            }
            using (var dropCmd = conn.CreateCommand())
            {
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_dbName}";
                dropCmd.ExecuteNonQuery();
            }
            using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE {_dbName}";
            createCmd.ExecuteNonQuery();
        }

        private void DropDatabase()
        {
            using var conn = new NpgsqlConnection(AdminConnStr);
            conn.Open();
            using (var killCmd = conn.CreateCommand())
            {
                killCmd.CommandText =
                    $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{_dbName}'";
                try { killCmd.ExecuteNonQuery(); } catch { }
            }
            using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_dbName}";
            try { dropCmd.ExecuteNonQuery(); } catch { }
        }
    }
}
