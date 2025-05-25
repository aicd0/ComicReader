// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;

using static ComicReader.Data.Models.Comic.ComicData;

namespace ComicReader.Data.Models.Comic;

internal sealed class ComicModel
{
    public const string COMIC_INFO_FILE_NAME = ComicData.COMIC_INFO_FILE_NAME;

    private readonly ComicData _internalModel;

    private ComicModel(ComicData comicData)
    {
        _internalModel = comicData;
    }

    //
    // Getters
    //

    public string CoverImageCacheKey => _internalModel.GetCoverImageCacheKey();
    public string Description => _internalModel.Description;
    public bool Hidden => _internalModel.Hidden;
    public long Id => _internalModel.Id;
    public bool IsDirectory => _internalModel is ComicFolderData;
    public bool IsEditable => _internalModel.IsEditable;
    public bool IsExternal => _internalModel.IsExternal;
    public bool IsRead => _internalModel.IsRead;
    public bool IsUnread => _internalModel.IsUnread;
    public double LastPosition => _internalModel.LastPosition;
    public string Location => _internalModel.Location;
    public int Progress => _internalModel.Progress;
    public int Rating => _internalModel.Rating;
    public string ReadableTags => _internalModel.TagString();
    public IReadOnlyList<TagData> Tags => _internalModel.Tags;
    public string Title => _internalModel.Title;
    public string Title1 => _internalModel.Title1;
    public string Title2 => _internalModel.Title2;

    public string GetImageCacheKey(int index)
    {
        return _internalModel.GetImageCacheKey(index);
    }

    public int GetImageSignature(int index)
    {
        return _internalModel.GetImageSignature(index);
    }

    //
    // Setters
    //

    public void ParseInfo(string text)
    {
        _internalModel.ParseInfo(text);
    }

    public void SaveBasic()
    {
        _internalModel.SaveBasic();
    }

    public void SetAsStarted()
    {
        _internalModel.SetAsStarted();
    }

    public Task SaveProgressAsync(int progress, double lastPosition)
    {
        return _internalModel.SaveProgressAsync(progress, lastPosition);
    }

    public void SaveRating(int rating)
    {
        _internalModel.SaveRating(rating);
    }

    public void SetAsRead()
    {
        _internalModel.SetAsRead();
    }

    public void SetAsUnread()
    {
        _internalModel.SetAsUnread();
    }

    public Task SaveHiddenAsync(bool hidden)
    {
        return _internalModel.SaveHiddenAsync(hidden);
    }

    //
    // Utilities
    //

    public Task<IComicConnection> OpenComicAsync()
    {
        return _internalModel.OpenComicAsync();
    }

    public Task SaveToInfoFile()
    {
        return _internalModel.SaveToInfoFile();
    }

    public Task<TaskException> ReloadImageFiles()
    {
        return _internalModel.ReloadImageFiles();
    }

    //
    // Pool
    //

    private static readonly ConcurrentWeakPool<long, ComicModel> _idPool = new();
    private static readonly ConcurrentWeakPool<string, ComicModel> _locationPool = new();

    private static ComicModel? ReplaceWithExisting(ComicData? comicData)
    {
        if (comicData == null)
        {
            return null;
        }
        var model = new ComicModel(comicData);
        if (comicData.Id >= 0)
        {
            return _idPool.GetOrAdd(model.Id, model);
        }
        if (!model.IsExternal)
        {
            // This should never happen, as all comics in the database should have an ID.
            Logger.AssertNotReachHere("C1A98069CD40CC1A");
        }
        return _locationPool.GetOrAdd(model.Location, model);
    }

    //
    // Creators
    //

    public static async Task<ComicModel?> FromId(long id, string taskName)
    {
        ComicData? comicData = await ComicData.FromId(id, taskName);
        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromLocation(string location, string taskName)
    {
        ComicData? comicData = await ComicData.FromLocation(location, taskName);
        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromFile(StorageFile file)
    {
        ComicData? comic = null;
        if (AppInfoProvider.IsSupportedDocumentExtension(file.FileType))
        {
            comic = await ComicData.FromLocation(file.Path, "ComicModelFromFileDocument");
            if (comic == null)
            {
                switch (file.FileType.ToLower())
                {
                    case ".pdf":
                        comic = await ComicPdfData.FromExternal(file);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (AppInfoProvider.IsSupportedArchiveExtension(file.FileType))
        {
            comic = await ComicData.FromLocation(file.Path, "ComicModelFromFileArchive");
            comic ??= await ComicArchiveData.FromExternal(file);
        }
        return ReplaceWithExisting(comic);
    }

    public static async Task<ComicModel?> FromImageFiles(string directory, List<StorageFile> imageFiles, StorageFile infoFile)
    {
        ComicData? comic = await ComicFolderData.FromExternal(directory, imageFiles, infoFile);
        return ReplaceWithExisting(comic);
    }

    //
    // Scanner
    //

    public static void UpdateAllComics(string reason, bool lazy)
    {
        ComicData.UpdateAllComics(reason, lazy);
    }
}
