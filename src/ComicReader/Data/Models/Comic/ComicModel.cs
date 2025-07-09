﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.SqlHelpers;

using Windows.Storage;

using static ComicReader.Data.Models.Comic.ComicData;

namespace ComicReader.Data.Models.Comic;

internal sealed class ComicModel
{
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
    public double LastPosition => _internalModel.LastPosition;
    public string Location => _internalModel.Location;
    public int Progress => _internalModel.Progress;
    public DateTimeOffset LastVisit => _internalModel.LastVisit;
    public int Rating => _internalModel.Rating;
    public IReadOnlyList<TagData> Tags => _internalModel.Tags;
    public string Title => _internalModel.Title;
    public string Title1 => _internalModel.Title1;
    public string Title2 => _internalModel.Title2;
    public CompletionStateEnum CompletionState => _internalModel.CompletionState;

    public string? GetExt(string key)
    {
        return _internalModel.GetExt(key);
    }

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

    public void SetExt(string key, string? value)
    {
        _internalModel.SetExt(key, value);
    }

    public void FlushExt()
    {
        _internalModel.FlushExt();
    }

    public void SetTitle1(string title)
    {
        _internalModel.SetTitle1(title);
    }

    public void SetTitle2(string title)
    {
        _internalModel.SetTitle2(title);
    }

    public void SetDescription(string description)
    {
        _internalModel.SetDescription(description);
    }

    public void SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
    {
        _internalModel.SetTags(tags);
    }

    public async Task SetCompletionStateToNotStarted()
    {
        await SaveProgressAsync(-1, 0);
        await _internalModel.SaveCompletionState(CompletionStateEnum.NotStarted);
    }

    public async Task SetCompletionStateToStarted()
    {
        _internalModel.SetAsStarted();
        await _internalModel.SaveCompletionState(CompletionStateEnum.Started);
    }

    public async Task SetCompletionStateToAtLeastStarted()
    {
        _internalModel.SetAsStarted();
        if (CompletionState == CompletionStateEnum.NotStarted)
        {
            await _internalModel.SaveCompletionState(CompletionStateEnum.Started);
        }
    }

    public async Task SetCompletionStateToCompleted()
    {
        await _internalModel.SaveCompletionState(CompletionStateEnum.Completed);
    }

    public Task SaveProgressAsync(int progress, double lastPosition)
    {
        return _internalModel.SaveProgressAsync(progress, lastPosition);
    }

    public void SaveRating(int rating)
    {
        _internalModel.SaveRating(rating);
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

    public Task<TaskException> ReloadImageFiles()
    {
        return _internalModel.ReloadImageFiles();
    }

    //
    // Pool
    //

    private static readonly ConcurrentWeakPool<long, ComicModel> _idPool = new();
    private static readonly ConcurrentWeakPool<string, ComicModel> _locationPool = new();

    private static bool TryGetExisting(long id, [MaybeNullWhen(false)] out ComicModel model)
    {
        return _idPool.TryGetValue(id, out model);
    }

    private static bool TryGetExisting(string location, [MaybeNullWhen(false)] out ComicModel model)
    {
        return _locationPool.TryGetValue(location, out model);
    }

    private static ComicModel ReplaceWithExisting(ComicData comicData)
    {
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
        if (TryGetExisting(id, out ComicModel? model))
        {
            return model;
        }
        ComicData? comicData = await ComicData.FromId(id, taskName);
        if (comicData == null)
        {
            return null;
        }
        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromLocation(string location, string taskName)
    {
        if (TryGetExisting(location, out ComicModel? model))
        {
            return model;
        }
        ComicData? comicData = await ComicData.FromLocation(location, taskName);
        if (comicData == null)
        {
            return null;
        }
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
        if (comic == null)
        {
            return null;
        }
        return ReplaceWithExisting(comic);
    }

    public static ComicModel? FromImageFiles(string directory, List<StorageFile> imageFiles)
    {
        ComicData? comic = ComicFolderData.FromExternal(directory, imageFiles);
        return ReplaceWithExisting(comic);
    }

    public static async Task<List<ComicModel>> BatchFromId(IEnumerable<long> ids, string taskName)
    {
        HashSet<long> idsUnique = [.. ids];
        List<ComicModel> results = [];
        List<long> requestingIds = [];
        foreach (long id in idsUnique)
        {
            if (TryGetExisting(id, out ComicModel? model))
            {
                results.Add(model);
            }
            else
            {
                requestingIds.Add(id);
            }
        }
        if (requestingIds.Count > 0)
        {
            List<ComicData> requestResults = await ComicData.BatchFromId(requestingIds, taskName);
            foreach (ComicData result in requestResults)
            {
                results.Add(ReplaceWithExisting(result));
            }
        }
        return results;
    }

    //
    // Utilities
    //

    public static void UpdateAllComics(string reason, bool lazy)
    {
        ComicData.UpdateAllComics(reason, lazy);
    }

    public static async Task<List<string>> GetAllTagCategories()
    {
        HashSet<string> tags = [];
        var command = new SelectCommand(TagCategoryTable.Instance);
        IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
        command.Distinct();
        using SelectCommand.IReader reader = await command.ExecuteAsync(SqlDatabaseManager.MainDatabase);
        while (reader.Read())
        {
            string name = nameToken.GetValue();
            tags.Add(name);
        }
        return [.. tags];
    }
}
