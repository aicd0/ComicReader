using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Core;

namespace ComicReader.Utils
{
    public interface IMethods_1<out T> { }

    public class Methods_1<T> : IMethods_1<T>
    {
        public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
        {
            collection[collection.IndexOf(item)] = item;
        }

        public static void UpdateCollection(ObservableCollection<T> source, Collection<T> new_collection, Func<T, T, bool> equal_func)
        {
            for (int i = 0; i < new_collection.Count; ++i)
            {
                if (i >= source.Count)
                {
                    break;
                }

                if (!equal_func(new_collection[i], source[i]))
                {
                    source.RemoveAt(0);
                    --i;
                }
            }

            for (int i = source.Count; i < new_collection.Count; ++i)
            {
                source.Add(new_collection[i]);
            }
        }
    }

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

            foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                string token = entry.Token;

                if (token.Length > token_form.Length)
                {
                    continue;
                }

                if (token_form.Substring(0, token.Length).Equals(token))
                {
                    var permit_folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                    string permit_folder_path = permit_folder.Path.ToLower();
                    string rest_path = path.Substring(permit_folder_path.Length);

                    if (rest_path.Length <= 1)
                    {
                        return permit_folder;
                    }

                    StorageFolder folder = await permit_folder.GetFolderAsync(rest_path.Substring(1));
                    return folder;
                }
            }

            return null; // no permission
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
    }
}
