using System;
using System.Collections.Generic;
using System.Threading;
using Engine;

namespace Game {
    //Almost by ChatGPT
    public sealed class ScanningProgressBuffer {
        readonly List<Vector3>[] m_history;
        readonly ReaderWriterLockSlim m_lock = new(LockRecursionPolicy.NoRecursion);
        int m_head;
        int m_count;
        List<Vector3> m_next;

        public int Capacity { get; }

        public int Count {
            get {
                m_lock.EnterReadLock();
                try {
                    return m_count;
                }
                finally {
                    m_lock.ExitReadLock();
                }
            }
        }

        public ScanningProgressBuffer(int capacity, int initialListCapacity = 100) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            Capacity = capacity;
            m_history = new List<Vector3>[capacity];
            for (int i = 0; i < capacity; i++) {
                m_history[i] = new List<Vector3>(initialListCapacity);
            }
            m_next = new List<Vector3>(initialListCapacity);
        }

        public void AddStep(CellFace cellFace, bool nextTurn) {
            if (m_count == 0 || nextTurn) {
                m_lock.EnterWriteLock();
                try {
                    int writeIndex;
                    if (m_count < Capacity) {
                        writeIndex = (m_head + m_count) % Capacity;
                        m_count++;
                    }
                    else {
                        writeIndex = m_head;
                        m_head = (m_head + 1) % Capacity;
                    }
                    (m_history[writeIndex], m_next) = (m_next, m_history[writeIndex]);
                    m_next.Clear();
                    m_next.AddRange(cellFace.GetSixVertices(1f, 0.02f));
                }
                finally {
                    m_lock.ExitWriteLock();
                }
            }
            else {
                m_next.AddRange(cellFace.GetSixVertices(1f, 0.02f));
            }
        }

        public IEnumerable<IReadOnlyList<Vector3>> EnumerateHistory() {
            int headSnapshot;
            int countSnapshot;
            m_lock.EnterReadLock();
            try {
                headSnapshot = m_head;
                countSnapshot = m_count;
            }
            finally {
                m_lock.ExitReadLock();
            }
            for (int i = 0; i < countSnapshot; i++) {
                yield return m_history[(headSnapshot + i) % Capacity];
            }
        }

        public void Clear() {
            m_lock.EnterWriteLock();
            try {
                foreach (List<Vector3> list in m_history) {
                    list.Clear();
                }
                m_next.Clear();
                m_count = 0;
                m_head = 0;
            }
            finally {
                m_lock.ExitWriteLock();
            }
        }
    }
}