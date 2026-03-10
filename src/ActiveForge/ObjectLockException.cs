using System;

namespace ActiveForge
{
    /// <summary>Thrown when an attempt to acquire or validate an object lock fails.</summary>
    [Serializable]
    public class ObjectLockException : PersistenceException
    {
        public ObjectLockException(string message) : base(message) { }
        public ObjectLockException(string message, Exception inner) : base(message, inner) { }
    }
}
