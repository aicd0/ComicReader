// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;

namespace ComicReader.ViewModels;

internal class ComicItemViewModel
{
    //
    // properties
    //

    public ComicModel Comic { get; }
    public string Title { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int Rating { get; set; } = -1;
    public string Progress { get; set; } = string.Empty;
    public bool IsFavorite { get; set; } = false;
    public bool IsHide { get; set; } = false;
    public ComicCompletionStatusEnum CompletionState { get; set; } = ComicCompletionStatusEnum.NotStarted;

    private readonly ReaderImageViewModel _image = new();
    public ReaderImageViewModel Image => _image;

    public bool IsRatingVisible => Rating != -1;
    public bool IsRead => CompletionState == ComicCompletionStatusEnum.Completed;
    public bool IsReading => CompletionState == ComicCompletionStatusEnum.Started;
    public bool IsUnread => CompletionState == ComicCompletionStatusEnum.NotStarted;

    //
    // Constructors
    //

    public ComicItemViewModel(ComicModel comic)
    {
        Comic = comic;
        Title = Comic.Title;
        Rating = comic.Rating;
        IsFavorite = FavoriteModel.Instance.FromId(comic.Id) != null;
        IsHide = comic.Hidden;
        CompletionState = comic.CompletionState;
    }

    public ComicItemViewModel(ComicItemViewModel source)
    {
        Comic = source.Comic;
        Title = source.Title;
        Detail = source.Detail;
        Rating = source.Rating;
        Progress = source.Progress;
        IsFavorite = source.IsFavorite;
        IsHide = source.IsHide;
        CompletionState = source.CompletionState;
        _image.Image = source._image.Image;
        _image.ImageRequested = source._image.ImageRequested;
    }

    //
    // Utilities
    //

    public void UpdateProgress(bool compat)
    {
        if (Comic.CompletionState == ComicCompletionStatusEnum.NotStarted)
        {
            Progress = StringResourceProvider.Instance.Unread;
        }
        else if (Comic.CompletionState == ComicCompletionStatusEnum.Completed)
        {
            Progress = StringResourceProvider.Instance.Finished;
        }
        else
        {
            if (compat)
            {
                Progress = Comic.Progress.ToString() + "%";
            }
            else
            {
                Progress = StringResourceProvider.Instance.FinishPercentage
                    .Replace("$percentage", Comic.Progress.ToString());
            }
        }
    }
};
