using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace ComicReader.Utils
{
    public class C0
    {
        public static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static async Task Sync(DispatchedHandler callback)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, callback);
        }

        public static string TryGetResourceString(string resource)
        {
            ResourceLoader resource_loader = ResourceLoader.GetForCurrentView();

            if (resource_loader == null)
            {
                return "?";
            }

            return resource_loader.GetString(resource);
        }

        public static MemoryStream SerializeToMemoryStream(object o)
        {
            MemoryStream stream = new MemoryStream();
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, o);
            return stream;
        }

        public static object DeserializeFromMemoryStream(MemoryStream stream)
        {
            IFormatter formatter = new BinaryFormatter();
            stream.Seek(0, SeekOrigin.Begin);
            object o = formatter.Deserialize(stream);
            return o;
        }

        public static async Task<object> DeserializeFromStream(Stream stream)
        {
            MemoryStream mstream = new MemoryStream();
            mstream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(mstream);
            return DeserializeFromMemoryStream(mstream);
        }

        public static async Task<StorageFolder> TryGetFolder(string path)
        {
            System.Diagnostics.Debug.Assert(path.Length != 0);

            string token = StringUtils.TokenFromPath(path);
            List<string> out_of_date_tokens = new List<string>();
            StorageFolder result = null;

            foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                string base_token = entry.Token;

                if (!Utils.StringUtils.PathContain(base_token, token))
                {
                    continue;
                }

                StorageFolder permitted_folder = await
                    StorageApplicationPermissions.FutureAccessList.GetFolderAsync(base_token);

                if (!StringUtils.TokenFromPath(permitted_folder.Path).Equals(base_token))
                {
                    // Remove entry if the folder path has changed.
                    out_of_date_tokens.Add(base_token);
                    continue;
                }

                result = await TryGetFolder(permitted_folder, path);
                break;
            }

            foreach (string out_of_date_token in out_of_date_tokens)
            {
                Utils.C0.RemoveFromFutureAccessList(out_of_date_token);
            }

            return result;
        }

        public static async Task<StorageFile> TryGetFile(string path)
        {
            string folder_path = Utils.StringUtils.ParentPathFromPath(path);
            StorageFolder folder = await Utils.C0.TryGetFolder(folder_path);

            if (folder == null)
            {
                return null;
            }

            string filename = Utils.StringUtils.ItemNameFromPath(path);
            IStorageItem item = await folder.TryGetItemAsync(filename);

            if (item == null || !item.IsOfType(StorageItemTypes.File))
            {
                return null;
            }

            return (StorageFile)item;
        }

        public static void AddToFutureAccessList(IStorageItem item)
        {
            string token = Utils.StringUtils.TokenFromPath(item.Path);
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, item);
            Utils.Debug.Log("Added '" + token + "' to future access list.");
        }

        public static void RemoveFromFutureAccessList(string token)
        {
            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                return;
            }

            StorageApplicationPermissions.FutureAccessList.Remove(token);
            Utils.Debug.Log("Removed '" + token + "' from future access list.");
        }

        public static async Task<StorageFolder> TryGetFolder(StorageFolder base_folder, string path)
        {
            string base_path = Utils.StringUtils.ToPathNoTail(base_folder.Path);

            if (base_path.Length > path.Length)
            {
                return null;
            }

            string rest_path = path.Substring(base_path.Length);

            if (rest_path.Length <= 1)
            {
                return base_folder;
            }

            IStorageItem item = await base_folder.TryGetItemAsync(rest_path.Substring(1));

            if (item == null || !item.IsOfType(StorageItemTypes.Folder))
            {
                return null;
            }

            return item as StorageFolder;
        }

        public static async Task<object> TryGetFile(StorageFolder folder, string name)
        {
            IStorageItem item = await folder.TryGetItemAsync(name);

            if (item == null)
            {
                return null;
            }

            if (!item.IsOfType(StorageItemTypes.File))
            {
                return null;
            }

            return (StorageFile)item;
        }

        public static async Task WaitFor(Func<bool> signal, int timeout_milliseconds = -1)
        {
            DateTimeOffset start_time = DateTimeOffset.Now;
            
            await Task.Run(delegate
            {
                SpinWait sw = new SpinWait();

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

        public static IBuffer GetBufferFromString(string _string)
        {
            if (_string.Length == 0)
            {
                return new Windows.Storage.Streams.Buffer(0);
            }
            else
            {
                return CryptographicBuffer.ConvertStringToBinary(
                    _string, BinaryStringEncoding.Utf8);
            }
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

        private enum _ModificationType
        {
            Unset,
            Skip,
            Delete,
            Add
        }

        class _Modification
        {
            public _ModificationType Type = _ModificationType.Unset;
            public int MinSteps;
        }

        public static void UpdateCollectionWithMinimumEditing(ObservableCollection<T> dst_collection,
            IEnumerable<T> src_collection, Func<T, T, bool> equal_func)
        {
            // DP Problem: Edit Distance
            List<List<_Modification>> modifications = new List<List<_Modification>>(dst_collection.Count + 1);

            for (int i = 0; i < dst_collection.Count + 1; i++)
            {
                var row = new List<_Modification>(src_collection.Count() + 1);

                for (int j = 0; j < src_collection.Count() + 1; j++)
                {
                    row.Add(new _Modification());
                }

                modifications.Add(row);
            }

            // Initialize cache.
            for (int i = 1; i < modifications[0].Count; ++i)
            {
                var mod = modifications[0][i];
                mod.Type = _ModificationType.Add;
                mod.MinSteps = i;
            }

            for (int i = 1; i < modifications.Count; ++i)
            {
                var mod = modifications[i][0];
                mod.Type = _ModificationType.Delete;
                mod.MinSteps = i;
            }

            // Update the whole cache.
            for (int i = 1; i < modifications.Count; ++i)
            {
                var row = modifications[i];
                var last_row = modifications[i - 1];

                for (int j = 1; j < row.Count; ++j)
                {
                    if (equal_func(dst_collection[i - 1], src_collection.ElementAt(j - 1)))
                    {
                        row[j].Type = _ModificationType.Skip;
                        row[j].MinSteps = last_row[j - 1].MinSteps;
                    }
                    else
                    {
                        if (row[j - 1].MinSteps < last_row[j].MinSteps)
                        {
                            row[j].Type = _ModificationType.Add;
                            row[j].MinSteps = row[j - 1].MinSteps + 1;
                        }
                        else
                        {
                            row[j].Type = _ModificationType.Delete;
                            row[j].MinSteps = last_row[j].MinSteps + 1;
                        }
                    }
                }
            }

            // Backtracking to find the best solution.
            List<_ModificationType> solution = new List<_ModificationType>();

            {
                int i = dst_collection.Count;
                int j = src_collection.Count();

                while (i + j > 0)
                {
                    var type = modifications[i][j].Type;
                    solution.Add(type);

                    if (type == _ModificationType.Skip)
                    {
                        i--;
                        j--;
                    }
                    else if (type == _ModificationType.Add)
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
                    _ModificationType type = solution[k];

                    if (type == _ModificationType.Skip)
                    {
                        ++i;
                        ++j;
                    }
                    else if (type == _ModificationType.Add)
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
            if (dst_collection.Count * src_collection.Count() <= 512)
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
            IEqualityComparer<V> m_comparer;

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
            List<KeyValuePair<V, object>> pairs_1 = new List<KeyValuePair<V, object>>();
            List<KeyValuePair<V, object>> pairs_2 = new List<KeyValuePair<V, object>>();

            foreach (T val in first)
            {
                pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
            }

            foreach (U val in second)
            {
                pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
            }

            IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Except(pairs_2, new KeyEqualityComparer(comparer));
            List<T> output = new List<T>();

            foreach (KeyValuePair<V, object> val in processed)
            {
                output.Add((T)val.Value);
            }

            return output;
        }

        public static IEnumerable<T> Intersect(IEnumerable<T> first, IEnumerable<U> second,
            Func<T, V> key_first, Func<U, V> key_second, IEqualityComparer<V> comparer)
        {
            List<KeyValuePair<V, object>> pairs_1 = new List<KeyValuePair<V, object>>();
            List<KeyValuePair<V, object>> pairs_2 = new List<KeyValuePair<V, object>>();

            foreach (T val in first)
            {
                pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
            }

            foreach (U val in second)
            {
                pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
            }

            IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Intersect(pairs_2, new KeyEqualityComparer(comparer));
            List<T> output = new List<T>();

            foreach (KeyValuePair<V, object> val in processed)
            {
                output.Add((T)val.Value);
            }

            return output;
        }
    }
}