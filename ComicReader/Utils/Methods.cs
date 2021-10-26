using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    internal class Methods
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

        public static async Task<StorageFolder> TryGetFolder(string path)
        {
            if (path.Length == 0)
            {
                throw new Exception();
            }

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
                    StorageApplicationPermissions.FutureAccessList.GetFolderAsync(
                        token);

                if (!StringUtils.TokenFromPath(permit_folder.Path).Equals(token))
                {
                    // remove the entry if folder path has changed
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

        public static async Task WaitFor(Func<bool> signal)
        {
            await Task.Run(delegate
            {
                SpinWait sw = new SpinWait();

                while (!signal())
                {
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

    public interface IMethods1<out T> { }

    public class Methods1<T> : IMethods1<T>
    {
        public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
        {
            collection[collection.IndexOf(item)] = item;
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
}
