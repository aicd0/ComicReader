// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace ComicReader.Common;

internal class C0
{
    public static void Run(Action action)
    {
        action();
    }

    public static async Task WaitFor(Func<bool> signal, int timeout_milliseconds = -1)
    {
        DateTimeOffset start_time = DateTimeOffset.Now;

        await Task.Run(delegate
        {
            var sw = new SpinWait();

            while (!signal())
            {
                if (timeout_milliseconds >= 0)
                {
                    TimeSpan time_elapsed = DateTimeOffset.Now - start_time;

                    if ((int)time_elapsed.TotalMilliseconds > timeout_milliseconds)
                    {
                        break;
                    }
                }

                sw.SpinOnce();
            }
        });
    }

    public static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog, XamlRoot root)
    {
        // https://learn.microsoft.com/en-us/windows/apps/design/controls/dialogs-and-flyouts/dialogs
        // https://github.com/microsoft/microsoft-ui-xaml/issues/4167
        dialog.XamlRoot = root;
        return await dialog.ShowAsync();
    }

    public static IBuffer GetBufferFromString(string text)
    {
        if (text.Length == 0)
        {
            return new Windows.Storage.Streams.Buffer(0);
        }
        else
        {
            return CryptographicBuffer.ConvertStringToBinary(
                text, BinaryStringEncoding.Utf8);
        }
    }

    public static Stream GetStreamFromString(string text)
    {
        IBuffer buffer = GetBufferFromString(text);
        return buffer.AsStream();
    }
}

interface IC1<out T> { }
public class C1<T> : IC1<T>
{
    public class DefaultEqualityComparer : EqualityComparer<T>
    {
        public override bool Equals(T x, T y)
        {
            return x.Equals(y);
        }

        public override int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }

    public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
    {
        int idx = collection.IndexOf(item);
        collection.Insert(idx + 1, item);
        collection.RemoveAt(idx);
    }

    private enum ModificationType
    {
        Unset,
        Skip,
        Delete,
        Add
    }

    class Modification
    {
        public ModificationType Type = ModificationType.Unset;
        public int MinSteps;
    }

    public static void UpdateCollectionWithMinimumEditing(ObservableCollection<T> dst_collection,
        IEnumerable<T> src_collection, Func<T, T, bool> equal_func)
    {
        // DP Problem: Edit Distance
        var modifications = new List<List<Modification>>(dst_collection.Count + 1);

        for (int i = 0; i < dst_collection.Count + 1; i++)
        {
            var row = new List<Modification>(src_collection.Count() + 1);

            for (int j = 0; j < src_collection.Count() + 1; j++)
            {
                row.Add(new Modification());
            }

            modifications.Add(row);
        }

        // Initialize cache.
        for (int i = 1; i < modifications[0].Count; ++i)
        {
            Modification mod = modifications[0][i];
            mod.Type = ModificationType.Add;
            mod.MinSteps = i;
        }

        for (int i = 1; i < modifications.Count; ++i)
        {
            Modification mod = modifications[i][0];
            mod.Type = ModificationType.Delete;
            mod.MinSteps = i;
        }

        // Update the whole cache.
        for (int i = 1; i < modifications.Count; ++i)
        {
            List<Modification> row = modifications[i];
            List<Modification> last_row = modifications[i - 1];

            for (int j = 1; j < row.Count; ++j)
            {
                if (equal_func(dst_collection[i - 1], src_collection.ElementAt(j - 1)))
                {
                    row[j].Type = ModificationType.Skip;
                    row[j].MinSteps = last_row[j - 1].MinSteps;
                }
                else
                {
                    if (row[j - 1].MinSteps < last_row[j].MinSteps)
                    {
                        row[j].Type = ModificationType.Add;
                        row[j].MinSteps = row[j - 1].MinSteps + 1;
                    }
                    else
                    {
                        row[j].Type = ModificationType.Delete;
                        row[j].MinSteps = last_row[j].MinSteps + 1;
                    }
                }
            }
        }

        // Backtracking to find the best solution.
        var solution = new List<ModificationType>();

        {
            int i = dst_collection.Count;
            int j = src_collection.Count();

            while (i + j > 0)
            {
                ModificationType type = modifications[i][j].Type;
                solution.Add(type);

                if (type == ModificationType.Skip)
                {
                    i--;
                    j--;
                }
                else if (type == ModificationType.Add)
                {
                    j--;
                }
                else
                {
                    i--;
                }
            }
        }

        // Perform the solution.
        {
            int i = 0;
            int j = 0;

            for (int k = solution.Count - 1; k >= 0; --k)
            {
                ModificationType type = solution[k];

                if (type == ModificationType.Skip)
                {
                    ++i;
                    ++j;
                }
                else if (type == ModificationType.Add)
                {
                    dst_collection.Insert(i, src_collection.ElementAt(j));
                    ++i;
                    ++j;
                }
                else
                {
                    dst_collection.RemoveAt(i);
                }
            }
        }
    }

    public static void UpdateCollectionWithDeleteFirstMatch(ObservableCollection<T> dst_collection,
        IEnumerable<T> src_collection, Func<T, T, bool> equal_func)
    {
        for (int i = 0; i < src_collection.Count(); ++i)
        {
            if (i < dst_collection.Count)
            {
                if (!equal_func(dst_collection[i], src_collection.ElementAt(i)))
                {
                    dst_collection.RemoveAt(i);
                    --i;
                }
            }
            else
            {
                dst_collection.Add(src_collection.ElementAt(i));
            }
        }
    }

    public static void UpdateCollection(ObservableCollection<T> dst_collection,
        IEnumerable<T> src_collection, Func<T, T, bool> equal_func)
    {
        if (dst_collection.Count * src_collection.Count() <= 32768)
        {
            UpdateCollectionWithMinimumEditing(dst_collection, src_collection, equal_func);
        }
        else
        {
            UpdateCollectionWithDeleteFirstMatch(dst_collection, src_collection, equal_func);
        }
    }
}

public interface IC3<out T, out U, out V> { }
public class C3<T, U, V> : IC3<T, U, V>
{
    private class KeyEqualityComparer : EqualityComparer<KeyValuePair<V, object>>
    {
        readonly IEqualityComparer<V> m_comparer;

        public KeyEqualityComparer(IEqualityComparer<V> comparer)
        {
            m_comparer = comparer;
        }

        public override bool Equals(KeyValuePair<V, object> x, KeyValuePair<V, object> y)
        {
            return m_comparer.Equals(x.Key, y.Key);
        }

        public override int GetHashCode(KeyValuePair<V, object> obj)
        {
            return m_comparer.GetHashCode(obj.Key);
        }
    }

    public static IEnumerable<T> Except(IEnumerable<T> first, IEnumerable<U> second,
        Func<T, V> key_first, Func<U, V> key_second, IEqualityComparer<V> comparer)
    {
        var pairs_1 = new List<KeyValuePair<V, object>>();
        var pairs_2 = new List<KeyValuePair<V, object>>();

        foreach (T val in first)
        {
            pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
        }

        foreach (U val in second)
        {
            pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
        }

        IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Except(pairs_2, new KeyEqualityComparer(comparer));
        var output = new List<T>();

        foreach (KeyValuePair<V, object> val in processed)
        {
            output.Add((T)val.Value);
        }

        return output;
    }

    public static IEnumerable<T> Intersect(IEnumerable<T> first, IEnumerable<U> second,
        Func<T, V> key_first, Func<U, V> key_second, IEqualityComparer<V> comparer)
    {
        var pairs_1 = new List<KeyValuePair<V, object>>();
        var pairs_2 = new List<KeyValuePair<V, object>>();

        foreach (T val in first)
        {
            pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
        }

        foreach (U val in second)
        {
            pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
        }

        IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Intersect(pairs_2, new KeyEqualityComparer(comparer));
        var output = new List<T>();

        foreach (KeyValuePair<V, object> val in processed)
        {
            output.Add((T)val.Value);
        }

        return output;
    }
}
