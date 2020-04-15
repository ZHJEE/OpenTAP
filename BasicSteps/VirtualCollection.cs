using System.Collections;
using System.Collections.Generic;

namespace OpenTap.Plugins.BasicSteps
{
    ///<summary> A list with virtual accessors. </summary>
    public class VirtualCollection<T> : IList<T>
    {
        List<T> list = new List<T>();
        public virtual IEnumerator<T> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
        public virtual void Add(T item) => list.Add(item);
        public virtual void Clear() => list.Clear();
        public virtual bool Contains(T item) => list.Contains(item);
        public virtual void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
        public virtual bool Remove(T item) => list.Remove(item);
        public virtual int Count => list.Count;
        public virtual bool IsReadOnly => ((IList)list).IsReadOnly;
        public virtual int IndexOf(T item) => list.IndexOf(item);
        public virtual void Insert(int index, T item) => list.Insert(index, item);
        public virtual void RemoveAt(int index) => list.RemoveAt(index);
        public virtual T this[int index]
        {
            get => list[index];
            set => list[index] = value;
        }
    }
}