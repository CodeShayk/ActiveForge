using System;
using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// Fluent static helpers that wrap an <see cref="Action"/> or <see cref="Func{T}"/>
    /// in a database transaction obtained from the ambient <see cref="IUnitOfWork"/>.
    ///
    /// <para>Basic usage:</para>
    /// <code>
    /// With.Transaction(uow, () =&gt;
    /// {
    ///     product.Name.Value = "New name";
    ///     product.Update();
    /// });
    /// </code>
    ///
    /// <para>Or let the locator resolve the UoW automatically:</para>
    /// <code>
    /// With.Transaction(() =&gt; { ... });
    /// </code>
    /// </summary>
    public static class With
    {
        private static ILogger _logger = NullLogger.Instance;

        /// <summary>
        /// Replaces the logger used by <see cref="Transaction"/> helpers.
        /// Called once at startup when Microsoft.Extensions.Logging is configured.
        /// </summary>
        public static void SetLogger(ILogger logger)
            => _logger = logger ?? NullLogger.Instance;

        // ── Action overloads ─────────────────────────────────────────────────────────

        /// <summary>
        /// Executes <paramref name="action"/> inside a new transaction on <paramref name="uow"/>.
        /// Commits on success, rolls back on any exception.
        /// </summary>
        public static void Transaction(IUnitOfWork uow, Action action,
            IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (uow    == null) throw new ArgumentNullException(nameof(uow));
            if (action == null) throw new ArgumentNullException(nameof(action));

            uow.CreateTransaction(level);
            try
            {
                action();
                uow.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "With.Transaction: exception — rolling back.");
                uow.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Resolves an <see cref="IUnitOfWork"/> from <see cref="TurquoiseServiceLocator"/>
        /// and executes <paramref name="action"/> inside a new transaction.
        /// </summary>
        public static void Transaction(Action action,
            IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            using IUnitOfWork uow = TurquoiseServiceLocator.GetUnitOfWork();
            Transaction(uow, action, level);
        }

        // ── Func<T> overloads ────────────────────────────────────────────────────────

        /// <summary>
        /// Executes <paramref name="func"/> inside a transaction and returns its result.
        /// Commits on success, rolls back on any exception.
        /// </summary>
        public static T Transaction<T>(IUnitOfWork uow, Func<T> func,
            IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            if (uow  == null) throw new ArgumentNullException(nameof(uow));
            if (func == null) throw new ArgumentNullException(nameof(func));

            uow.CreateTransaction(level);
            try
            {
                T result = func();
                uow.Commit();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "With.Transaction<T>: exception — rolling back.");
                uow.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Resolves an <see cref="IUnitOfWork"/> from <see cref="TurquoiseServiceLocator"/>
        /// and executes <paramref name="func"/> inside a new transaction.
        /// </summary>
        public static T Transaction<T>(Func<T> func,
            IsolationLevel level = IsolationLevel.ReadCommitted)
        {
            using IUnitOfWork uow = TurquoiseServiceLocator.GetUnitOfWork();
            return Transaction(uow, func, level);
        }

        // ── Serializable shorthand ───────────────────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="action"/> under Serializable isolation.
        /// Use when the operation must be fully isolated from concurrent readers.
        /// </summary>
        public static void SerializableTransaction(IUnitOfWork uow, Action action)
            => Transaction(uow, action, IsolationLevel.Serializable);

        /// <summary>
        /// Runs <paramref name="action"/> under RepeatableRead isolation.
        /// </summary>
        public static void RepeatableReadTransaction(IUnitOfWork uow, Action action)
            => Transaction(uow, action, IsolationLevel.RepeatableRead);

        /// <summary>
        /// Runs <paramref name="action"/> under Snapshot isolation (requires database to have ALLOW_SNAPSHOT_ISOLATION ON).
        /// </summary>
        public static void SnapshotTransaction(IUnitOfWork uow, Action action)
            => Transaction(uow, action, IsolationLevel.Snapshot);
    }
}
