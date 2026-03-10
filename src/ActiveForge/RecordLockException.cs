using System;

namespace ActiveForge
{
    /// <summary>Thrown when an attempt to acquire or validate an record lock fails.</summary>
    [Serializable]
    public class RecordLockException : PersistenceException
    {
        public RecordLockException(string message) : base(message) { }
        public RecordLockException(string message, Exception inner) : base(message, inner) { }
    }
}
