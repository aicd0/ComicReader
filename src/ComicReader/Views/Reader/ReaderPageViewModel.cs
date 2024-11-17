// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;
using ComicReader.Utils.Image;
using ComicReader.Utils.Lifecycle;
using ComicReader.Views.Base;
using ComicReader.Views.Navigation;

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Reader;

internal class ReaderPageViewModel : BaseViewModel
{
    private ComicData _comic;
    private readonly CancellationLock _loadComicLock = new();
    private volatile bool _updatingProgress = false;
    private readonly CancellationSession _frameInfoLoaderSession = new();
    private readonly TaskQueue _frameInfoLoaderQueue = new("LoadFrameInfo");

    public MutableLiveData<ReaderStatusEnum> ReaderStatusLiveData { get; } = new(ReaderStatusEnum.Loading);

    private readonly MutableLiveData<bool> _gridViewVisibleLiveData = new();
    public LiveData<bool> GridViewVisibleLiveData => _gridViewVisibleLiveData;

    private readonly MutableLiveData<bool> _verticalReaderVisibleLiveData = new(false);
    public LiveData<bool> VerticalReaderVisibleLiveData => _verticalReaderVisibleLiveData;

    private readonly MutableLiveData<bool> _horizontalReaderVisibleLiveData = new();
    public LiveData<bool> HorizontalReaderVisibleLiveData => _horizontalReaderVisibleLiveData;

    private readonly MutableLiveData<ReaderSettingDataModel> _readerSettingsLiveData = new(AppDataRepository.GetReaderSetting());
    public LiveData<ReaderSettingDataModel> ReaderSettingsLiveData => _readerSettingsLiveData;

    private readonly MutableLiveData<bool> _isExternalComicLiveData = new(true);
    public LiveData<bool> IsExternalComicLiveData => _isExternalComicLiveData;

    private bool _gridViewModeEnabled = false;
    public bool GridViewModeEnabled
    {
        get => _gridViewModeEnabled;
        set
        {
            _gridViewModeEnabled = value;
            UpdateReaderUI();
            GetNavigationPageAbility().SetGridViewMode(value);
        }
    }

    public override void OnPause()
    {
        base.OnPause();
        _frameInfoLoaderSession.Next();
    }

    public void SetReaderSettings(ReaderSettingDataModel settings)
    {
        _readerSettingsLiveData.Emit(settings);
    }

    public void UpdateReaderUI()
    {
        bool isWorking = ReaderStatusLiveData.GetValue() == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && _gridViewModeEnabled;
        bool readerVisible = isWorking && !previewVisible;
        bool verticalReaderVisible = readerVisible && ReaderSettingsLiveData.GetValue().IsVertical;
        bool horizontalReaderVisible = readerVisible && !verticalReaderVisible;

        _gridViewVisibleLiveData.Emit(previewVisible);
        _verticalReaderVisibleLiveData.Emit(verticalReaderVisible);
        _horizontalReaderVisibleLiveData.Emit(horizontalReaderVisible);
    }

    public async Task LoadComic(ComicData comic, ReaderPage page, ReaderView horizontalReader, ReaderView verticalReader)
    {
        if (comic == _comic)
        {
            return;
        }

        await _loadComicLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            if (comic == null)
            {
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                return;
            }

            ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

            verticalReader.Controller.Reset();
            horizontalReader.Controller.Reset();

            ReaderView reader = page.GetReader();
            System.Diagnostics.Debug.Assert(reader != null);

            reader.Controller.SetOnLoadedListener(() =>
            {
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
                page.UpdatePage(reader);
                page.BottomTileShow();
                page.BottomTileHide(5000);
            });

            _comic = comic;
            verticalReader.Controller.Comic = _comic;
            horizontalReader.Controller.Comic = _comic;

            if (!_comic.IsExternal)
            {
                // Mark as read.
                _comic.SetAsStarted();

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
                reader.Controller.SetInitialPage(_comic.LastPosition);

                // Load frames.
                for (int i = 0; i < _comic.ImageAspectRatios.Count; ++i)
                {
                    await verticalReader.Controller.LoadFrame(i);
                    await horizontalReader.Controller.LoadFrame(i);
                }

                // Refresh reader.
                await reader.Controller.UpdateImages(true);
            }

            LoadImages(horizontalReader, verticalReader);
            await reader.Controller.Finalize();

            // Refresh reader.
            await reader.Controller.UpdateImages(true);
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

    public void SetIsFavorite(bool isFavorite)
    {
        GetNavigationPageAbility().SetFavorite(isFavorite);

        if (!_comic.IsExternal)
        {
            Utils.C0.Run(async delegate
            {
                if (isFavorite)
                {
                    await FavoriteDataManager.Add(_comic.Id, _comic.Title1, true);
                }
                else
                {
                    await FavoriteDataManager.RemoveWithId(_comic.Id, true);
                }
            });
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

        _isExternalComicLiveData.Emit(_comic.IsExternal);

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

        bool isFavorite = !_comic.IsExternal && await FavoriteDataManager.FromId(_comic.Id) != null;
        SetIsFavorite(isFavorite);

        if (!_comic.IsExternal)
        {
            page.Shared.Rating = _comic.Rating;
        }
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    private void LoadImages(ReaderView horizontalReader, ReaderView verticalReader)
    {
        CancellationSession.Token token = _frameInfoLoaderSession.Next();

        var tokens = new List<ImageLoader.Token>();
        ComicData comic = _comic; // Stores locally.
        var save_timer = new Utils.Stopwatch();

        for (int i = 0; i < comic.ImageCount; ++i)
        {
            int index = i; // Stores locally.
            tokens.Add(new ImageLoader.Token
            {
                SessionToken = token,
                Comic = comic,
                Index = index,
                Callback = new FrameInfoLoaderCallback(comic, horizontalReader, verticalReader, index, save_timer)
            });
        }

        save_timer.Start();
        new ImageLoader.Transaction(tokens)
            .SetQueue(_frameInfoLoaderQueue)
            .Commit();
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

    private class FrameInfoLoaderCallback : ImageLoader.ICallback
    {
        private readonly ReaderView _horizontalReader;
        private readonly ReaderView _verticalReader;
        private readonly int _index;
        private readonly ComicData _comic;
        private readonly Stopwatch _saveTimer;

        public FrameInfoLoaderCallback(ComicData comic, ReaderView horizontalReader, ReaderView verticalReader, int index, Stopwatch saveTimer)
        {
            _horizontalReader = horizontalReader;
            _verticalReader = verticalReader;
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
                        await _verticalReader.Controller.LoadFrame(_comic.ImageAspectRatios.Count - 1);
                        await _horizontalReader.Controller.LoadFrame(_comic.ImageAspectRatios.Count - 1);
                    }

                    _comic.ImageAspectRatios.Add(image_aspect_ratio);
                }

                await _verticalReader.Controller.LoadFrame(_index);
                await _horizontalReader.Controller.LoadFrame(_index);

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
