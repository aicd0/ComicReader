using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Utils
{
    public class Storage
    {
        private static Dictionary<string, StorageFolder> FolderResources = new Dictionary<string, StorageFolder>();
        private static Dictionary<string, StorageFile> FileResources = new Dictionary<string, StorageFile>();

        public static void AddTrustedFolder(StorageFolder folder)
        {
            string token = Utils.StringUtils.TokenFromPath(folder.Path);
            FolderResources[token] = folder;
        }

        public static void AddTrustedFile(StorageFile file)
        {
            string token = Utils.StringUtils.TokenFromPath(file.Path);
            FileResources[token] = file;
        }

        public static async Task<StorageFolder> TryGetFolder(string path)
        {
            System.Diagnostics.Debug.Assert(path.Length != 0);
            string token = StringUtils.TokenFromPath(path);

            if (FolderResources.ContainsKey(token))
            {
                return FolderResources[token];
            }

            List<string> out_of_date_tokens = new List<string>();
            StorageFolder result = null;

            foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                string base_token = entry.Token;

                if (!Utils.StringUtils.PathContain(base_token, token))
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
                    Log("Failed to access folder through token '" + base_token + "'. " + e.ToString());
                    out_of_date_tokens.Add(base_token);
                    continue;
                }

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
                Utils.Storage.RemoveFromFutureAccessList(out_of_date_token);
            }

            if (result != null)
            {
                FolderResources[token] = result;
            }

            return result;
        }

        public static async Task<StorageFile> TryGetFile(string path)
        {
            string token = Utils.StringUtils.TokenFromPath(path);

            if (FileResources.ContainsKey(token))
            {
                return FileResources[token];
            }

            string folder_path = Utils.StringUtils.ParentLocationFromLocation(path);
            StorageFolder folder = await Utils.Storage.TryGetFolder(folder_path);

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

            StorageFile file = (StorageFile)item;
            FileResources[token] = file;
            return file;
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

        private static void Log(string text)
        {
            Utils.Debug.Log("Storage: " + text);
        }
    }
}
