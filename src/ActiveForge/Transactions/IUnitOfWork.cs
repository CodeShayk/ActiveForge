using System;
using System.Data;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Coordinates database work across one or more <see cref="Record"/> operations.
    /// Obtain a concrete instance from <see cref="ActiveForgeServiceLocator"/> or your DI container.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>Whether a transaction is currently active.</summary>
        bool InTransaction { get; }

        /// <summary>Begins a new database transaction on the underlying connection.</summary>
        TransactionBase CreateTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);

        /// <summary>Commits the current transaction.</summary>
        void Commit();

        /// <summary>Rolls back the current transaction.</summary>
        void Rollback();
    }
}
