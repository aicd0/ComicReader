﻿using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;
using ComicReader.Utils.Image;
using ComicReader.Views.Base;
using ComicReader.Views.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ComicReader.Views.Reader;
internal class ReaderPageViewModel : BaseViewModel
{
    private ComicData _comic;
    private readonly CancellationLock _loadComicLock = new();
    private readonly CancellationSession _loadImageSession = new CancellationSession();
    private volatile bool _updatingProgress = false;

    public LiveData<ReaderStatusEnum> ReaderStatusLiveData { get; } = new(ReaderStatusEnum.Loading);
    public LiveData<bool> GridViewVisibleLiveData { get; } = new();
    public LiveData<bool> VerticalReaderVisibleLiveData { get; } = new();
    public LiveData<bool> HorizontalReaderVisibleLiveData { get; } = new();
    public LiveData<ReaderSettingDataModel> ReaderSettingsLiveData
    {
        get => GetNavigationPageAbility().GetReaderSettingLiveData();
    }

    public override void OnPause()
    {
        base.OnPause();
        _loadImageSession.Next();
        if (_comic != null && !_comic.IsExternal)
        {
            Common.AppStatusPreserver.Instance.UnsetReadingComic(_comic.Id);
        }
    }

    public void UpdateReaderUI()
    {
        bool isWorking = ReaderStatusLiveData.GetValue() == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && GetNavigationPageAbility().GetPreviewButtonToggledLiveData().GetValue();
        bool readerVisible = isWorking && !previewVisible;
        bool verticalReaderVisible = readerVisible && ReaderSettingsLiveData.GetValue().IsVertical;
        bool horizontalReaderVisible = readerVisible && !verticalReaderVisible;

        GridViewVisibleLiveData.Emit(previewVisible);
        VerticalReaderVisibleLiveData.Emit(verticalReaderVisible);
        HorizontalReaderVisibleLiveData.Emit(horizontalReaderVisible);
    }

    public async Task LoadComic(ComicData comic, ReaderPage page)
    {
        if (comic == null || comic == _comic)
        {
            return;
        }

        await _loadComicLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

            page.VerticalReader.Reset();
            page.HorizontalReader.Reset();

            ReaderViewController reader = page.GetCurrentReader();
            System.Diagnostics.Debug.Assert(reader != null);

            reader.OnLoaded = () =>
            {
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
                page.UpdatePage(reader);
                page.BottomTileShow();
                page.BottomTileHide(5000);
            };

            _comic = comic;
            page.VerticalReader.Comic = _comic;
            page.HorizontalReader.Comic = _comic;

            if (!_comic.IsExternal)
            {
                // Mark as read.
                _comic.SetAsRead();

                // Add to history
                await HistoryDataManager.Add(_comic.Id, _comic.Title1, true);

                // Update image files.
                TaskException result = await _comic.UpdateImages(cover_only: false, reload: true);
                if (!result.Successful())
                {
                    page.Log("Failed to load images of '" + _comic.Location + "'. " + result.ToString());
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                    return;
                }
            }

            // Load info.
            await LoadComicInfo(page);

            // Load image frames.
            if (!_comic.IsExternal)
            {
                // Set initial page.
                reader.InitialPage = _comic.LastPosition;

                // Load frames.
                for (int i = 0; i < _comic.ImageAspectRatios.Count; ++i)
                {
                    await page.VerticalReader.LoadFrame(i);
                    await page.HorizontalReader.LoadFrame(i);
                }

                // Refresh reader.
                await reader.UpdateImages(true);
            }

            LoadImages(page);
            await reader.Finalize();

            // Refresh reader.
            await reader.UpdateImages(true);
            page.UpdatePage(reader);
        });
    }

    public void UpdateProgress(ReaderViewController reader, bool save)
    {
        double page = reader.PageSource;

        if (page <= 0.0)
        {
            return;
        }

        int progress;

        if (reader.PageCount <= 0)
        {
            progress = 0;
        }
        else if (reader.IsLastPage)
        {
            progress = 100;
        }
        else
        {
            progress = (int)((float)page / reader.PageCount * 100);
        }

        progress = Math.Min(progress, 100);

        if (save)
        {
            if (_updatingProgress)
            {
                return;
            }

            _updatingProgress = true;
            Task.Run(delegate
            {
                _comic.SaveProgressAsync(progress, page).Wait();
                _updatingProgress = false;
            });
        }
    }

    public async Task SetIsFavorite(bool isFavorite)
    {
        LiveData<bool> liveData = GetNavigationPageAbility().GetIsFavoriteLiveData();
        if (liveData.GetValue() == isFavorite)
        {
            return;
        }

        liveData.Emit(isFavorite);
        if (isFavorite)
        {
            await FavoriteDataManager.Add(_comic.Id, _comic.Title1, final: true);
        }
        else
        {
            await FavoriteDataManager.RemoveWithId(_comic.Id, final: true);
        }
    }

    public ComicData GetComic()
    {
        return _comic;
    }

    public async Task LoadComicInfo(ReaderPage page)
    {
        if (_comic == null)
        {
            return;
        }

        GetNavigationPageAbility().GetIsExternalLiveData().Emit(_comic.IsExternal);

        if (_comic.Title1.Length == 0)
        {
            page.Shared.ComicTitle1 = _comic.Title;
        }
        else
        {
            page.Shared.ComicTitle1 = _comic.Title1;
            page.Shared.ComicTitle2 = _comic.Title2;
        }

        page.Shared.ComicDir = _comic.Location;
        page.Shared.CanDirOpenInFileExplorer = _comic is ComicFolderData;
        page.Shared.IsEditable = _comic.IsEditable;

        LoadComicTag(page);

        if (!_comic.IsExternal)
        {
            bool isFavorite = await FavoriteDataManager.FromId(_comic.Id) != null;
            GetNavigationPageAbility().GetIsFavoriteLiveData().Emit(isFavorite);
            page.Shared.Rating = _comic.Rating;
        }
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    private void LoadImages(ReaderPage page)
    {
        CancellationSession.Token token = _loadImageSession.Next();

        double preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        page.PreviewDataSource.Clear();

        var preview_img_loader_tokens = new List<ImageLoader.Token>();
        ComicData comic = _comic; // Stores locally.
        var save_timer = new Utils.Stopwatch();

        for (int i = 0; i < comic.ImageCount; ++i)
        {
            int index = i; // Stores locally.
            preview_img_loader_tokens.Add(new ImageLoader.Token
            {
                SessionToken = token,
                Comic = comic,
                Index = index,
                Callback = new LoadPreviewImageCallback(_comic, page, index, save_timer)
            });
        }

        save_timer.Start();
        new ImageLoader.Transaction(preview_img_loader_tokens)
            .SetWidthConstraint(preview_width).SetHeightConstraint(preview_height).Commit();
    }

    private void LoadComicTag(ReaderPage page)
    {
        if (_comic == null)
        {
            return;
        }

        var new_collection = new ObservableCollection<TagCollectionViewModel>();

        for (int i = 0; i < _comic.Tags.Count; ++i)
        {
            TagData tags = _comic.Tags[i];
            var tags_model = new TagCollectionViewModel(tags.Name);

            foreach (string tag in tags.Tags)
            {
                var tag_model = new TagViewModel
                {
                    Tag = tag,
                    ItemHandler = page._tagItemHandler
                };
                tags_model.Tags.Add(tag_model);
            }

            new_collection.Add(tags_model);
        }

        page.Shared.ComicTags = new_collection;
    }

    private class LoadPreviewImageCallback : ImageLoader.ICallback
    {
        private readonly ReaderPage _page;
        private readonly int _index;
        private readonly ComicData _comic;
        private readonly Stopwatch _saveTimer;

        public LoadPreviewImageCallback(ComicData comic, ReaderPage page, int index, Stopwatch saveTimer)
        {
            _page = page;
            _index = index;
            _comic = comic;
            _saveTimer = saveTimer;
        }

        public void OnSuccess(BitmapImage image)
        {
            // Save image aspect ratio info.
            double image_aspect_ratio;
            if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
            {
                image_aspect_ratio = -1;
            }
            else
            {
                image_aspect_ratio = (double)image.PixelWidth / image.PixelHeight;
            }

            // Update previews.
            _page.PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                ImageSource = image,
                Page = _index + 1,
            });

            Utils.C0.Run(async delegate
            {
                if (_index < _comic.ImageAspectRatios.Count)
                {
                    _comic.ImageAspectRatios[_index] = image_aspect_ratio;
                }
                else
                {
                    // Normally image aspect ratio items will be added one by one.
                    // In some cases (like corrupted images), few indices will be skipped.
                    while (_index > _comic.ImageAspectRatios.Count)
                    {
                        _comic.ImageAspectRatios.Add(-1);
                        await _page.VerticalReader.LoadFrame(_comic.ImageAspectRatios.Count - 1);
                        await _page.HorizontalReader.LoadFrame(_comic.ImageAspectRatios.Count - 1);
                    }

                    _comic.ImageAspectRatios.Add(image_aspect_ratio);
                }

                await _page.VerticalReader.LoadFrame(_index);
                await _page.HorizontalReader.LoadFrame(_index);

                // Save for each 5 sec.
                if (_saveTimer.LapSpan().TotalSeconds > 5.0 || _index == _comic.ImageCount - 1)
                {
                    _comic.SaveImageAspectRatios();
                    _saveTimer.Lap();
                }
            });
        }
    }

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }
}
