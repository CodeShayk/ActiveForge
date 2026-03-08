using System;
using System.Data;

namespace Turquoise.ORM.Transactions
{
    /// <summary>
    /// Coordinates database work across one or more <see cref="DataObject"/> operations.
    /// Obtain a concrete instance from <see cref="TurquoiseServiceLocator"/> or your DI container.
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
