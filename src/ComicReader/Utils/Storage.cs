// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.AccessCache;

namespace ComicReader.Utils;

internal static class Storage
{
    private static readonly Dictionary<string, StorageFolder> s_FolderResources = new();
    private static readonly Dictionary<string, StorageFile> s_FileResources = new();

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

        var pendingRemoveTokens = new List<string>();
        StorageFolder result = null;

        var futureAccessList = StorageApplicationPermissions.FutureAccessList.Entries.ToList();
        foreach (AccessListEntry entry in futureAccessList)
        {
            string base_token = entry.Token;

            if (!StringUtils.FolderContain(StringUtils.PathFromToken(base_token), StringUtils.PathFromToken(token)))
            {
                continue;
            }

            StorageFolder permittedFolder = null;
            try
            {
                permittedFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(base_token);
            }
            catch (FileNotFoundException)
            {
                pendingRemoveTokens.Add(base_token);
                continue;
            }
            catch (Exception e)
            {
                Logger.F("GetFolderFromToken", "", e);
                pendingRemoveTokens.Add(base_token);
                continue;
            }

            if (!StringUtils.TokenFromPath(permittedFolder.Path).Equals(base_token))
            {
                // remove the entry if the folder path has changed
                pendingRemoveTokens.Add(base_token);
                continue;
            }

            result = await TryGetFolder(permittedFolder, path);
            break;
        }

        foreach (string removingToken in pendingRemoveTokens)
        {
            RemoveFromFutureAccessList(removingToken);
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

    private static void Log(string message)
    {
        Logger.I("Storag", message);
    }
}
