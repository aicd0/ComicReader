// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;

using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal class ComicFolderData : ComicData
{
    private const string TAG = "ComicFolderData";

    private StorageFolder Folder { get; set; }
    private List<StorageFile> ImageFiles { get; set; } = [];

    public override bool IsEditable => !IsExternal;

    private ComicFolderData(bool is_external) :
        base(ComicType.Folder, is_external)
    { }

    public static ComicData FromDatabase(string location)
    {
        return new ComicFolderData(false)
        {
            Location = location,
        };
    }

    public static ComicData FromExternal(string directory, List<StorageFile> image_files)
    {
        if (image_files.Count == 0)
        {
            return null;
        }

        image_files = [.. image_files.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)];

        var comic = new ComicFolderData(true)
        {
            Location = directory,
            ImageFiles = image_files,
        };

        return comic;
    }

    private async Task<TaskException> SetFolder()
    {
        if (Folder != null)
        {
            return TaskException.Success;
        }

        if (Location == null)
        {
            return TaskException.InvalidParameters;
        }

        StorageFolder folder = await Storage.TryGetFolder(Location);

        if (folder == null)
        {
            return TaskException.NoPermission;
        }

        Folder = folder;
        return TaskException.Success;
    }

    protected override async Task<TaskException> ReloadImages()
    {
        if (IsExternal)
        {
            return TaskException.Success;
        }

        TaskException result = await SetFolder();
        if (!result.Successful())
        {
            return result;
        }

        // Load all images.
        Log("Retrieving images in '" + Location + "'");
        var query_options = new QueryOptions
        {
            FolderDepth = FolderDepth.Shallow,
            IndexerOption = IndexerOption.DoNotUseIndexer, // The results from UseIndexerWhenAvailable are incomplete.
        };

        foreach (string type in AppInfoProvider.SupportedImageExtensions)
        {
            query_options.FileTypeFilter.Add(type);
        }

        StorageFileQueryResult query = Folder.CreateFileQueryWithOptions(query_options);
        IReadOnlyList<StorageFile> img_files = await query.GetFilesAsync();

        // Sort by display name.
        ImageFiles = img_files
            .OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)
            .ToList();
        Log(img_files.Count.ToString() + " images added.");
        return TaskException.Success;
    }

    public override string GetImageCacheKey(int index)
    {
        if (index < 0 || index >= ImageFiles.Count)
        {
            Logger.F(TAG, "GetImageCacheKey");
            return null;
        }

        return ImageFiles[index].Path;
    }

    public override int GetImageSignature(int index)
    {
        if (index < 0 || index >= ImageFiles.Count)
        {
            Logger.F(TAG, "GetImageSignature");
            return 0;
        }

        return FileUtils.GetFileHashCode(ImageFiles[index]);
    }

    public override async Task<IComicConnection> OpenComicAsync()
    {
        await LoadImageFiles();
        return new FolderComicConnection(ImageFiles);
    }

    private class FolderComicConnection : IComicConnection
    {
        private readonly List<StorageFile> _imageFiles;

        public FolderComicConnection(List<StorageFile> imageFiles)
        {
            _imageFiles = imageFiles;
        }

        public void Dispose()
        {
        }

        public int GetImageCount()
        {
            return _imageFiles.Count;
        }

        public async Task<IRandomAccessStream> GetImageStream(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "InternalGetImageStream");
                return null;
            }

            try
            {
                return await _imageFiles[index].OpenAsync(FileAccessMode.Read);
            }
            catch (Exception e)
            {
                Logger.F(TAG, "Failed to access '" + _imageFiles[index].Path + "'. ", e);
                return null;
            }
        }
    }
}
