using System;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class that wraps a provider-specific database transaction.
    /// Decouples the ORM engine from any concrete ADO.NET provider by exposing a
    /// uniform transaction lifecycle API. Concrete subclasses
    /// (e.g. <c>SqlAdapterTransaction</c>, <c>NpgsqlAdapterTransaction</c>,
    /// <c>SQLiteAdapterTransaction</c>) delegate each operation to the underlying
    /// native transaction object.
    /// <para>
    /// Instances are created by <see cref="BaseConnection.BeginTransaction"/> and
    /// associated with commands via <see cref="BaseCommand.SetTransaction"/>.
    /// </para>
    /// </summary>
    public abstract class BaseTransaction : IDisposable
    {
        /// <summary>
        /// Commits all changes made within the transaction to the database.
        /// After a successful commit the transaction is no longer usable.
        /// </summary>
        /// <exception cref="Exception">
        /// The provider may raise an exception if the transaction is in a non-committable
        /// state (e.g. a prior statement failed on PostgreSQL or SQL Server).
        /// </exception>
        public abstract void Commit();

        /// <summary>
        /// Rolls back all changes made within the transaction, restoring the database to
        /// its state at the start of the transaction.
        /// After a rollback the transaction is no longer usable.
        /// </summary>
        public abstract void Rollback();

        /// <summary>
        /// Releases all resources held by the transaction, including the underlying
        /// native transaction object. Calling <see cref="Dispose"/> without a prior
        /// <see cref="Commit"/> implicitly rolls back the transaction on most providers.
        /// </summary>
        public abstract void Dispose();
    }
}
