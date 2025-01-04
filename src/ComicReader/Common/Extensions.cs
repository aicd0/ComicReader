// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.Common;

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
            int count = VisualTreeHelper.GetChildrenCount(obj);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
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

    public static void SafeAppend(this StringBuilder sb, string category, Func<object> func)
    {
        string value;
        try
        {
            value = func()?.ToString() ?? "[null]";
        }
        catch (Exception)
        {
            return;
        }
        sb.Append(category);
        sb.Append(": ");
        sb.Append(value);
        sb.Append('\n');
    }
}
