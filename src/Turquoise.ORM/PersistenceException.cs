using System;

namespace Turquoise.ORM
{
    /// <summary>
    /// Thrown when any persistence (CRUD / query) operation fails.
    /// </summary>
    [Serializable]
    public class PersistenceException : ApplicationException
    {
        public PersistenceException(string message) : base(message) { }
        public PersistenceException(string message, Exception inner) : base(message, inner) { }
    }
}
