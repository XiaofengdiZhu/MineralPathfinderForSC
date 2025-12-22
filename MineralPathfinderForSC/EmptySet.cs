using System.Collections;
using System.Collections.Generic;

namespace Game {
    public class EmptySet<T> : ISet<T> {
        public IEnumerator<T> GetEnumerator() {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        void ICollection<T>.Add(T item) { }
        public void ExceptWith(IEnumerable<T> other) { }
        public void IntersectWith(IEnumerable<T> other) { }
        public bool IsProperSubsetOf(IEnumerable<T> other) => false;
        public bool IsProperSupersetOf(IEnumerable<T> other) => false;
        public bool IsSubsetOf(IEnumerable<T> other) => false;
        public bool IsSupersetOf(IEnumerable<T> other) => false;
        public bool Overlaps(IEnumerable<T> other) => false;
        public bool SetEquals(IEnumerable<T> other) => false;
        public void SymmetricExceptWith(IEnumerable<T> other) { }
        public void UnionWith(IEnumerable<T> other) { }
        bool ISet<T>.Add(T item) => false;
        public void Clear() { }
        public bool Contains(T item) => false;
        public void CopyTo(T[] array, int arrayIndex) { }
        public bool Remove(T item) => false;
        public int Count => 0;
        public bool IsReadOnly => true;
    }
}