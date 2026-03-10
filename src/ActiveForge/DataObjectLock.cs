namespace ActiveForge
{
    /// <summary>
    /// Represents a row-level lock held on a DataObject read for update.
    /// In this port the locking infrastructure is minimal; only the
    /// <see cref="UpdateOption"/> enum is retained as it is used by
    /// <see cref="DataObject.Update(DataObjectLock.UpdateOption)"/>.
    /// </summary>
    public class DataObjectLock
    {
        /// <summary>Controls how a row lock is treated during an update.</summary>
        public enum UpdateOption
        {
            ReleaseLock = 1,
            RetainLock  = 2,
            IgnoreLock  = 3,
        }
    }
}
