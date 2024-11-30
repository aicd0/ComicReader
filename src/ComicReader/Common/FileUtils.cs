// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace ComicReader.Common;

internal static class FileUtils
{
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
}
