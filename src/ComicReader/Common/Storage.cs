// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Storage;

namespace ComicReader.Common;

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
        ArgumentException.ThrowIfNullOrEmpty(path);
        return await StorageFolder.GetFolderFromPathAsync(path);
    }

    public static async Task<StorageFile> TryGetFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return await StorageFile.GetFileFromPathAsync(path);
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
}
