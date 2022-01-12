using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class MinHeap<T> : IEnumerable<T>
    {
        private readonly T[] Container;
        private readonly int Capacity;
        private int Count;
        private readonly Func<T, T, int> CompareFunc;

        public MinHeap(int capacity, Func<T, T, int> cmp_func)
        {
            Container = new T[capacity + 1];
            Capacity = capacity;
            Count = 0;
            CompareFunc = cmp_func;
        }

        public void Add(T item)
        {
            if (Count >= Capacity)
            {
                if (CompareFunc(item, Container[1]) <= 0)
                {
                    return;
                }

                Container[1] = item;
                int i = 1;
                int il = 2;
                int ir = 3;

                while (true)
                {
                    int i_min = i;

                    if (il <= Count && CompareFunc(Container[il], item) < 0)
                    {
                        i_min = il;
                    }

                    if (ir <= Count && CompareFunc(Container[ir], Container[i_min]) < 0)
                    {
                        i_min = ir;
                    }

                    if (i_min == i)
                    {
                        break;
                    }

                    Container[i] = Container[i_min];
                    Container[i_min] = item;
                    i = i_min;
                    il = i * 2;
                    ir = i * 2 + 1;
                }
            }
            else
            {
                ++Count;
                Container[Count] = item;
                int i = Count;
                int ip = i / 2;

                while (ip >= 1 && CompareFunc(Container[i], Container[ip]) < 0)
                {
                    Container[i] = Container[ip];
                    Container[ip] = item;
                    i = ip;
                    ip = i / 2;
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            IEnumerable<T> e = Container.Skip(1);
            if (Count < Capacity)
            {
                e = e.SkipLast(Capacity - Count);
            }
            return e.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
