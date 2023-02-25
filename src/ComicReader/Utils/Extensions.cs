using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;

namespace ComicReader.Utils
{
    internal static class Extensions
    {
        public static bool Successful(this TaskException r)
        {
            return r == TaskException.Success;
        }

        public static IEnumerable<DependencyObject> ChildrenBreadthFirst(this DependencyObject obj, bool includeSelf = false)
        {
            if (includeSelf)
            {
                yield return obj;
            }

            var queue = new Queue<DependencyObject>();
            queue.Enqueue(obj);

            while (queue.Count > 0)
            {
                obj = queue.Dequeue();
                var count = VisualTreeHelper.GetChildrenCount(obj);

                for (var i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    yield return child;
                    queue.Enqueue(child);
                }
            }
        }

        public static T Get<T>(this WeakReference<T> r) where T : class
        {
            if (r.TryGetTarget(out T obj))
            {
                return obj;
            }
            return null;
        }
    }
}
