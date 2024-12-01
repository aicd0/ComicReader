// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using ComicReader.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.Common;

internal static class FileUtils
{
    private const string TAG = nameof(FileUtils);

    public static long GetDirectorySize(DirectoryInfo directory)
    {
        long size = 0;
        FileInfo[] files = directory.GetFiles();
        foreach (FileInfo file in files)
        {
            size += file.Length;
        }
        DirectoryInfo[] subDirectories = directory.GetDirectories();
        foreach (DirectoryInfo subDirectory in subDirectories)
        {
            size += GetDirectorySize(subDirectory);
        }
        return size;
    }

    public static int GetFileHashCode(StorageFile file)
    {
        try
        {
            var fileInfo = new FileInfo(file.Path);
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + fileInfo.LastWriteTime.GetHashCode();
                hash = hash * 23 + fileInfo.Length.GetHashCode();
                return hash;
            }
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "GetFileHashCode", ex);
            return 0;
        }
    }
}
