using System;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Base class for unit-of-work implementations.
    /// Manages a single <see cref="BaseTransaction"/> and a nesting depth counter so that
    /// re-entrant calls (an already-active transaction calling another transactional method)
    /// are safe: the outer transaction "owns" the connection-level transaction; inner calls
    /// increment the depth counter and only the outermost Commit/Rollback touches the DB.
    /// <para>
    /// Connection lifetime is tied to transaction lifetime: the underlying
    /// <see cref="DataConnection"/> is opened when the outermost transaction begins
    /// (<c>_depth</c> 0 → 1) and closed in a <c>finally</c> block when it commits or rolls
    /// back (<c>_depth</c> 1 → 0).  <c>[ConnectionScope]</c> is therefore not required.
    /// </para>
    /// </summary>
    public abstract class BaseUnitOfWork : IUnitOfWork
    {
        private readonly DataConnection _connection;
        private readonly ILogger        _logger;
        private BaseTransaction         _currentTransaction;
        private int                     _depth;
        private bool                    _rollbackOnly;
        private bool                    _disposed;
        private bool                    _ownedConnection;  // true when THIS UoW opened the connection

        protected BaseUnitOfWork(DataConnection connection, ILogger logger = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger     = logger     ?? NullLogger.Instance;
        }

        // ── IUnitOfWork ──────────────────────────────────────────────────────────────

        public bool InTransaction => _depth > 0;

        public BaseTransaction CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            ThrowIfDisposed();
            if (_depth == 0)
            {
                _ownedConnection = !_connection.IsOpen;
                if (_ownedConnection) _connection.Connect();
                _currentTransaction = BeginTransactionCore(level);
                _rollbackOnly = false;
                _logger.LogDebug("UnitOfWork: transaction started (isolation={Level})", level);
            }
            _depth++;
            _logger.LogDebug("UnitOfWork: depth now {Depth}", _depth);
            return _currentTransaction;
        }

        public void Commit()
        {
            ThrowIfDisposed();
            if (_depth == 0)
                throw new InvalidOperationException("No active transaction to commit.");

            _depth--;
            _logger.LogDebug("UnitOfWork: commit requested, depth now {Depth}", _depth);

            if (_depth == 0)
            {
                if (_rollbackOnly)
                {
                    _logger.LogWarning("UnitOfWork: transaction marked rollback-only — rolling back instead of committing.");
                    CommitOrRollback(commit: false);
                }
                else
                {
                    CommitOrRollback(commit: true);
                    _logger.LogDebug("UnitOfWork: transaction committed.");
                }
            }
        }

        public void Rollback()
        {
            ThrowIfDisposed();
            if (_depth == 0)
                throw new InvalidOperationException("No active transaction to roll back.");

            // Mark rollback-only so nested Commit() calls honour the outer Rollback().
            _rollbackOnly = true;
            _depth--;
            _logger.LogDebug("UnitOfWork: rollback requested, depth now {Depth}", _depth);

            if (_depth == 0)
            {
                CommitOrRollback(commit: false);
                _logger.LogDebug("UnitOfWork: transaction rolled back.");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_depth > 0)
            {
                _logger.LogWarning("UnitOfWork disposed with depth {Depth} — rolling back.", _depth);
                try { _currentTransaction?.Rollback(); } catch { /* swallow */ }
                _depth = 0;
            }

            _currentTransaction?.Dispose();
            _currentTransaction = null;

            if (_ownedConnection)
            {
                try { if (_connection.IsOpen) _connection.Disconnect(); } catch { /* swallow */ }
                _ownedConnection = false;
            }

            DisposeCore();
        }

        // ── Overridable hooks ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a provider-specific transaction at the given isolation level.
        /// Called only when depth transitions from 0 → 1 (after the connection is opened).
        /// </summary>
        protected abstract BaseTransaction BeginTransactionCore(IsolationLevel level);

        /// <summary>Called by <see cref="Dispose"/> after the transaction has been cleaned up.</summary>
        protected virtual void DisposeCore() { }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private void CommitOrRollback(bool commit)
        {
            try
            {
                if (commit)
                    _currentTransaction.Commit();
                else
                    _currentTransaction.Rollback();
            }
            finally
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
                _rollbackOnly = false;

                // Sync any provider-internal depth counters before closing the connection.
                if (commit)
                    _connection.NotifyTransactionCommitted();
                else
                    _connection.NotifyTransactionRolledBack();

                // Only close the connection if this UoW opened it.
                if (_ownedConnection)
                {
                    _connection.Disconnect();
                    _ownedConnection = false;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}
