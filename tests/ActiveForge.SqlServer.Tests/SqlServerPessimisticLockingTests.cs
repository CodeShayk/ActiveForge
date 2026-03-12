using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ActiveForge;
using ActiveForge.Attributes;
using Xunit;

namespace ActiveForge.SqlServer.Tests
{
    [Table("lock_products")]
    public sealed class SsLockProduct : IdentityRecord
    {
        [Column("name")]  public TString  Name  = new TString();
        [Column("price")] public TDecimal Price = new TDecimal();

        public SsLockProduct() { }
        public SsLockProduct(DataConnection conn) : base(conn) { }
    }

    [Trait("Category", "Integration")]
    public sealed class SqlServerPessimisticLockingTests : IDisposable
    {
        private static int _counter;
        private readonly string _dbName =
            $"af_lock_{System.Threading.Interlocked.Increment(ref _counter)}";

        private static string MasterConnStr => // Using lower timeout to fail fast for tests
            (Environment.GetEnvironmentVariable("SS_ADMIN_CONNSTR")
            ?? @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True")
            + ";Pooling=false";

        private string TestConnStr =>
            new SqlConnectionStringBuilder(MasterConnStr)
            {
                InitialCatalog          = _dbName,
                MultipleActiveResultSets = true,
                ConnectTimeout          = 5,
            }.ConnectionString;

        private readonly SqlServerConnection _connA;
        private readonly SqlServerConnection _connB;

        public SqlServerPessimisticLockingTests()
        {
            CreateDatabase();
            _connA = new SqlServerConnection(TestConnStr);
            _connA.Connect();
            CreateSchema();
            SeedData();

            _connB = new SqlServerConnection(TestConnStr);
            _connB.Connect();
            _connB.SetTimeout(1); // 1 second timeout for thread B so it fails fast when blocked
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
            
            var pA = new SsLockProduct(_connA);
            pA.ID.SetValue(targetId);
            bool readRes = _connA.ReadForUpdate(pA, null);
            readRes.Should().BeTrue();

            // Thread B attempts to read the same record for update
            // Since Thread A holds the UPDLOCK, this should throw a SQL timeout exception
            // Note: ReadForUpdate requires an active transaction.
            var pB = new SsLockProduct(_connB);
            pB.ID.SetValue(targetId);
            
            Action blockedRead = () => 
            {
                using var txB_tmp = _connB.BeginTransaction();
                try 
                {
                    _connB.ReadForUpdate(pB, null);
                }
                finally
                {
                    _connB.RollbackTransaction(txB_tmp);
                }
            };

            blockedRead.Should().Throw<PersistenceException>()
                       .Where(ex => ex.ToString().Contains("Timeout") || ex.InnerException.ToString().Contains("Timeout"));

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
            _connA.ExecSQL(
                "CREATE TABLE lock_products (" +
                "  id    INT IDENTITY(1,1) PRIMARY KEY," +
                "  name  NVARCHAR(200) NOT NULL," +
                "  price DECIMAL(18,4) NOT NULL DEFAULT 0" +
                ")");
        }

        private void SeedData()
        {
            InsertProduct("Shared Item", 100m);
        }

        private int InsertProduct(string name, decimal price)
        {
            var p = new SsLockProduct(_connA);
            p.Name.SetValue(name);
            p.Price.SetValue(price);
            p.Insert();
            return (int)p.ID.GetValue();
        }

        private void CreateDatabase()
        {
            using var conn = new SqlConnection(MasterConnStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"IF DB_ID('{_dbName}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_dbName}] END; " +
                $"CREATE DATABASE [{_dbName}]";
            cmd.ExecuteNonQuery();
        }

        private void DropDatabase()
        {
            using var conn = new SqlConnection(MasterConnStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_dbName}]";
            try { cmd.ExecuteNonQuery(); } catch { }
        }
    }
}
