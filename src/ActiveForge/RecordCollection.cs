using System.Collections.Generic;

namespace ActiveForge
{
    /// <summary>
    /// Result set returned by query operations.
    /// Contains the fetched records plus pagination metadata.
    /// </summary>
    public class RecordCollection : List<Record>
    {
        public int  StartRecord        { get; set; }
        public int  PageSize           { get; set; }
        public bool IsMoreData         { get; set; }
        public int  TotalRowCount      { get; set; }
        public bool TotalRowCountValid { get; set; }

        /// <summary>Appends an object at the end of the collection.</summary>
        public void AddTail(Record obj) => Add(obj);

        /// <summary>Inserts an object at the beginning of the collection.</summary>
        public void AddHead(Record obj) => Insert(0, obj);

        /// <summary>
        /// Appends all objects from another collection to this one.
        /// </summary>
        public void Add(RecordCollection other)
        {
            if (other != null) AddRange(other);
        }
    }

    /// <summary>
    /// Typed result set for query operations returning objects of type <typeparamref name="T"/>.
    /// </summary>
    public class ObjectCollection<T> : RecordCollection where T : Record
    {
        public new IEnumerator<T> GetEnumerator()
        {
            foreach (Record obj in (List<Record>)this)
                yield return (T)obj;
        }
    }
}
