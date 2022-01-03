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

            string token_form = StringUtils.TokenFromPath(path);
            List<string> useless_tokens = new List<string>();
            StorageFolder result = null;

            foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                string token = entry.Token;

                if (token.Length > token_form.Length)
                {
                    continue;
                }

                if (!token_form.Substring(0, token.Length).Equals(token))
                {
                    continue;
                }

                StorageFolder permit_folder = await
                    StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);

                if (!StringUtils.TokenFromPath(permit_folder.Path).Equals(token))
                {
                    // Remove the entry if folder path has changed.
                    useless_tokens.Add(token);
                    continue;
                }

                string permit_folder_path = permit_folder.Path.ToLower();
                string rest_path = path.Substring(permit_folder_path.Length);

                if (rest_path.Length <= 1)
                {
                    result = permit_folder;
                    break;
                }

                result = await permit_folder.GetFolderAsync(rest_path.Substring(1));
                break;
            }

            foreach (string token in useless_tokens)
            {
                StorageApplicationPermissions.FutureAccessList.Remove(token);
            }

            return result;
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
        public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
        {
            int idx = collection.IndexOf(item);
            collection.Insert(idx + 1, item);
            collection.RemoveAt(idx);
        }

        public static void UpdateCollection(ObservableCollection<T> old_collection,
            Collection<T> new_collection, Func<T, T, bool> equal_func)
        {
            for (int i = 0; i < Math.Min(old_collection.Count, new_collection.Count); ++i)
            {
                if (!equal_func(new_collection[i], old_collection[i]))
                {
                    old_collection.RemoveAt(i);
                    --i;
                }
            }

            for (int i = old_collection.Count; i < new_collection.Count; ++i)
            {
                old_collection.Add(new_collection[i]);
            }

            for (int i = new_collection.Count; i < old_collection.Count;)
            {
                old_collection.RemoveAt(i);
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