using System;
using System.Collections.Generic;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// This PriorityQueue class implements a binary heap data structure
    /// 
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
        }
        /// <summary>
        /// The Dequeue method removes and returns the item with the highest priority, and reorders the items to maintain the binary heap property. 
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
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
        }

        /// <summary>
        /// The Peek method returns the item with the highest priority without removing it
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            T frontItem = data[0];
            return frontItem;
        }

        public int Count
        {
            get { return data.Count; }
        }

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
    }
}