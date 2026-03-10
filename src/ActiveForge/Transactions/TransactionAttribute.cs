using System;
using System.Data;

namespace ActiveForge.Transactions
{
    /// <summary>
    /// Marks a method (or all methods on a class) so that <see cref="TransactionInterceptor"/>
    /// automatically wraps each invocation in a database transaction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class TransactionAttribute : Attribute
    {
        /// <summary>Isolation level to use when starting the transaction. Default: ReadCommitted.</summary>
        public IsolationLevel IsolationLevel { get; }

        public TransactionAttribute(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            IsolationLevel = isolationLevel;
        }
    }
}
