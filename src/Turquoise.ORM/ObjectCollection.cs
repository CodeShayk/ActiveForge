using System.Collections.Generic;

namespace Turquoise.ORM
{
    /// <summary>
    /// Result set returned by query operations.
    /// Contains the fetched DataObjects plus pagination metadata.
    /// </summary>
    public class ObjectCollection : List<DataObject>
    {
        public int  StartRecord        { get; set; }
        public int  PageSize           { get; set; }
        public bool IsMoreData         { get; set; }
        public int  TotalRowCount      { get; set; }
        public bool TotalRowCountValid { get; set; }

        /// <summary>Appends an object at the end of the collection.</summary>
        public void AddTail(DataObject obj) => Add(obj);

        /// <summary>Inserts an object at the beginning of the collection.</summary>
        public void AddHead(DataObject obj) => Insert(0, obj);

        /// <summary>
        /// Appends all objects from another collection to this one.
        /// </summary>
        public void Add(ObjectCollection other)
        {
            if (other != null) AddRange(other);
        }
    }

    /// <summary>
    /// Typed result set for query operations returning objects of type <typeparamref name="T"/>.
    /// </summary>
    public class ObjectCollection<T> : ObjectCollection where T : DataObject
    {
        public new IEnumerator<T> GetEnumerator()
        {
            foreach (DataObject obj in (List<DataObject>)this)
                yield return (T)obj;
        }
    }
}
