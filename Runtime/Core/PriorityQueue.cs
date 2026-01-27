using System;
using System.Collections.Generic;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// This PriorityQueue class implements a binary heap data structure
    /// <typeparam name="T"></typeparam>
    public class PriorityQueue<T> where T : IComparable<T>
    {
        private List<T> data;

        public PriorityQueue()
        {
            this.data = new List<T>();
        }
        /// <summary>
        /// The Enqueue method adds a new item to the priority queue and reorders the items to maintain the binary heap property. 
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            data.Add(item);
            HeapifyUp(data.Count - 1);
            //OLD
            /*
            int ci = data.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (data[ci].CompareTo(data[pi]) >= 0)
                {
                    break;
                }
                T tmp = data[ci];
                data[ci] = data[pi];
                data[pi] = tmp;
                ci = pi;
            }
            */
        }
        /// <summary>
        /// The Dequeue method removes and returns the item with the highest priority, and reorders the items to maintain the binary heap property. 
        /// </summary>
        /// <returns></returns>

        /// <summary>
        /// Safe version of Dequeue.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            if (data.Count== 0)
            {
                item = default;
                return false;
            }
            item = Dequeue();
            return true;
        }
        public T Dequeue()
        {
            if (data.Count == 0)
            {
                throw new InvalidOperationException("Cannot Dequeue from an empty Priority Queue.");
            }
            T frontItem = data[0];
            int lastIndex = data.Count - 1;
            if(lastIndex == 0)
            {
                data.RemoveAt(0);
                return frontItem;
            }
            data[0] = data[lastIndex];
            data.RemoveAt(lastIndex);
            HeapifyDown(0);
            return frontItem;
            /*
            //OLD
            int li = data.Count - 1;
            T frontItem = data[0];
            data[0] = data[li];
            data.RemoveAt(li);

            --li;
            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li)
                {
                    break;
                }
                int rc = ci + 1;
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0)
                {
                    ci = rc;
                }
                if (data[pi].CompareTo(data[ci]) <= 0)
                {
                    break;
                }
                T tmp = data[pi];
                data[pi] = data[ci];
                data[ci] = tmp;
                pi = ci;
            }
            return frontItem;
            */
        }

        /// <summary>
        /// Safe version of Peek
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryPeek(out T item)
        {
            if (data.Count == 0)
            {
                item = default;
                return false;
            }
            item = data[0];
            return true;
        }
        /// <summary>
        /// The Peek method returns the item with the highest priority without removing it
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            if (data.Count == 0)
            {
                throw new InvalidOperationException("Cannot peek an empty Priority Queue.");
            }
            return data[0];
        }
        

        /// <summary>
        /// Removes the first occurence of the given item from the queue.
        /// Returns true if an item was removed
        /// Complexity: O(n) to find + O(log n) to restore heap
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            int index = data.IndexOf(item);
            if (index < 0) return false;
            return RemoveAt(index);
        }
        /// <summary>
        /// Removes the item at the given index (heap index)
        /// Returns true if successful, false if index out of range
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= data.Count) return false;

            int lastIndex = data.Count - 1;

            // If removing last element, just pop.
            if (index == lastIndex)
            {
                data.RemoveAt(lastIndex);
                return true;
            }

            // Swap with last and remove.
            data[index] = data[lastIndex];
            data.RemoveAt(lastIndex);

            // Fix heap: try down first; if no movement, then up.
            int movedTo = HeapifyDown(index);
            HeapifyUp(movedTo);

            return true;
        }
        public int Count => data.Count;
        
        /// <summary>
        ///  method iterates through all the nodes in the priority queue and compares the value 
        ///  of each node with the values of its children. If it finds a node that violates the 
        ///  binary heap property, it returns false. If all nodes are consistent with the binary 
        ///  heap property, it returns true.
        /// </summary>
        /// <returns></returns>
        public bool IsConsistent()
        {
            if (data.Count == 0)
            {
                return true;
            }
            int li = data.Count - 1;
            for (int pi = 0; pi < data.Count; ++pi)
            {
                int lci = 2 * pi + 1;
                int rci = 2 * pi + 2;
                if (lci <= li && data[pi].CompareTo(data[lci]) > 0)
                {
                    return false;
                }
                if (rci <= li && data[pi].CompareTo(data[rci]) > 0)
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Returns true if the given item is currently in the queue.
        /// </summary>
        public bool Contains(T item)
        {
            // This uses a linear search under the hood. If you need faster
            // membership checks, consider maintaining an additional HashSet<T>.
            return data.Contains(item);
        }
        /// <summary>
        /// Returns true if the given item is the 'active' item at the top of the priority queue.
        /// (i.e., the next item to be dequeued)
        /// </summary>
        public bool IsActive(T item)
        {
            // Make sure queue is not empty first
            if (data.Count == 0) return false;

            // Check if the front (smallest) item is 'item'
            return data[0].Equals(item);
        }
        /// <summary>
        /// Only should be called when we need to reset something from say a data file coming in
        /// </summary>
        public void ResetAndClear()
        {
            data.Clear();
        }
        #region Heap Helpers
        /// <summary>
        /// Works up the heap from a start index
        /// </summary>
        /// <param name="childIndex"></param>
        private void HeapifyUp(int childIndex)
        {
            int ci = childIndex;
            while(ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (data[ci].CompareTo(data[pi]) >= 0) break;

                Swap(ci, pi);
                ci = pi;
            }
        }
        /// <summary>
        /// Works down the heap from a start index. Returns final index the element moved to
        /// </summary>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        private int HeapifyDown(int startIndex)
        {
            int li = data.Count - 1;
            int pi = startIndex;

            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;

                int rc = ci + 1;
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0)
                    ci = rc;

                if (data[pi].CompareTo(data[ci]) <= 0) break;

                Swap(pi, ci);
                pi = ci;
            }

            return pi;
        }
        private void Swap(int a, int b)
        {
            T tmp = data[a];
            data[a] = data[b];
            data[b] = tmp;
        }
        #endregion
    }
}