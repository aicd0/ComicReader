using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Utils
{
    internal static class Storage
    {
        private static readonly Dictionary<string, StorageFolder> s_FolderResources = new Dictionary<string, StorageFolder>();
        private static readonly Dictionary<string, StorageFile> s_FileResources = new Dictionary<string, StorageFile>();

        public static void AddTrustedFolder(StorageFolder folder)
        {
            string token = StringUtils.TokenFromPath(folder.Path);
            s_FolderResources[token] = folder;
        }

        public static void AddTrustedFile(StorageFile file)
        {
            string token = StringUtils.TokenFromPath(file.Path);
            s_FileResources[token] = file;
        }

        public static async Task<StorageFolder> TryGetFolder(string path)
        {
            System.Diagnostics.Debug.Assert(path.Length != 0);
            string token = StringUtils.TokenFromPath(path);

            if (s_FolderResources.ContainsKey(token))
            {
                return s_FolderResources[token];
            }

            var out_of_date_tokens = new List<string>();
            StorageFolder result = null;

            foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                string base_token = entry.Token;

                if (!StringUtils.FolderContain(StringUtils.PathFromToken(base_token), StringUtils.PathFromToken(token)))
                {
                    continue;
                }

                StorageFolder permitted_folder = null;
                try
                {
                    permitted_folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(base_token);
                }
                catch (Exception e)
                {
                    Debug.LogException("GetFolderFromToken", e);
                    out_of_date_tokens.Add(base_token);
                    continue;
                }

                if (!StringUtils.TokenFromPath(permitted_folder.Path).Equals(base_token))
                {
                    // remove the entry if the folder path has changed
                    out_of_date_tokens.Add(base_token);
                    continue;
                }

                result = await TryGetFolder(permitted_folder, path);
                break;
            }

            foreach (string out_of_date_token in out_of_date_tokens)
            {
                RemoveFromFutureAccessList(out_of_date_token);
            }

            if (result != null)
            {
                s_FolderResources[token] = result;
            }

            return result;
        }

        public static async Task<StorageFile> TryGetFile(string path)
        {
            string token = StringUtils.TokenFromPath(path);

            if (s_FileResources.ContainsKey(token))
            {
                return s_FileResources[token];
            }

            string folder_path = StringUtils.ParentLocationFromLocation(path);
            StorageFolder folder = await TryGetFolder(folder_path);

            if (folder == null)
            {
                return null;
            }

            string filename = StringUtils.ItemNameFromPath(path);
            IStorageItem item = await folder.TryGetItemAsync(filename);

            if (item == null || !item.IsOfType(StorageItemTypes.File))
            {
                return null;
            }

            var file = (StorageFile)item;
            s_FileResources[token] = file;
            return file;
        }

        public static void AddToFutureAccessList(IStorageItem item)
        {
            string token = StringUtils.TokenFromPath(item.Path);
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, item);
            Log("Added '" + token + "' to future access list.");
        }

        public static void RemoveFromFutureAccessList(string token)
        {
            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                return;
            }

            StorageApplicationPermissions.FutureAccessList.Remove(token);
            Log("Removed '" + token + "' from future access list.");
        }

        public static bool AllowAddToFutureAccessList()
        {
            return StorageApplicationPermissions.FutureAccessList.Entries.Count <
                StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed;
        }

        public static async Task<StorageFolder> TryGetFolder(StorageFolder base_folder, string path)
        {
            string base_path = StringUtils.ToPathNoTail(base_folder.Path);

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

        private static void Log(string text)
        {
            Debug.Log("Storage: " + text);
        }
    }
}
