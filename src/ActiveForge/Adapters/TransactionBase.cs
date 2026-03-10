using System;

namespace ActiveForge
{
    /// <summary>Wraps a provider-specific database transaction.</summary>
    public abstract class TransactionBase : IDisposable
    {
        public abstract void Commit();
        public abstract void Rollback();
        public abstract void Dispose();
    }
}
