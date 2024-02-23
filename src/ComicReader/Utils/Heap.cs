using System;
using System.Collections.Generic;

namespace ComicReader.Utils
{
    public class FixedHeap<T>
    {
        public int Count => m_container.Count;

        private readonly List<T> m_container;
        private readonly int m_capacity;
        private readonly Comparison<T> m_comparison;

        public FixedHeap(int capacity, Comparison<T> comparer)
        {
            m_container = new List<T>(capacity);
            m_capacity = capacity;
            m_comparison = comparer;
        }

        public void Add(T item)
        {
            if (Count >= m_capacity)
            {
                if (m_comparison(item, m_container[0]) <= 0)
                {
                    return;
                }

                m_container[0] = item;
                int i = 0;
                int il = 1;
                int ir = 2;

                while (true)
                {
                    int i_min = i;

                    if (il < Count && m_comparison(m_container[il], item) < 0)
                    {
                        i_min = il;
                    }

                    if (ir < Count && m_comparison(m_container[ir], m_container[i_min]) < 0)
                    {
                        i_min = ir;
                    }

                    if (i_min == i)
                    {
                        break;
                    }

                    m_container[i] = m_container[i_min];
                    m_container[i_min] = item;
                    i = i_min;
                    il = i * 2 + 1;
                    ir = il + 1;
                }
            }
            else
            {
                m_container.Add(item);
                int i = Count - 1;
                int ip = (i - 1) / 2;

                while (ip >= 0 && m_comparison(m_container[i], m_container[ip]) < 0)
                {
                    m_container[i] = m_container[ip];
                    m_container[ip] = item;
                    i = ip;
                    ip = (i - 1) / 2;
                }
            }
        }

        public List<T> GetSorted()
        {
            var cpy = new List<T>(m_container);
            cpy.Sort(m_comparison);
            cpy.Reverse();
            return cpy;
        }
    }
}
