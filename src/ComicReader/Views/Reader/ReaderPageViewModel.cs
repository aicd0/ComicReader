// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common.SimpleImageView;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;
using ComicReader.Utils.Lifecycle;
using ComicReader.Views.Base;
using ComicReader.Views.Navigation;

namespace ComicReader.Views.Reader;

internal class ReaderPageViewModel : BaseViewModel
{
    private ComicData _comic;
    private volatile bool _updatingProgress = false;

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

        if (comic == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

        verticalReader.Reset();
        horizontalReader.Reset();

        ReaderView reader = page.GetReader();
        System.Diagnostics.Debug.Assert(reader != null);

        reader.SetInitialPageLoadedHandler(delegate
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
            page.UpdatePage(reader);
            page.BottomTileShow();
            page.BottomTileHide(5000);
        });

        _comic = comic;

        if (!_comic.IsExternal)
        {
            // Mark as read.
            _comic.SetAsStarted();

            // Add to history
            await HistoryDataManager.Add(_comic.Id, _comic.Title1, true);

            // Update image files.
            TaskException result = await _comic.UpdateImages(reload: true);
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
            reader.SetInitialPosition(_comic.LastPosition);
        }

        var images = new List<IImageSource>();
        for (int i = 0; i < _comic.ImageCount; ++i)
        {
            images.Add(new ComicImageSource(comic, i));
        }
        reader.StartLoadingImages(images);

        reader.DoFinalize();

        // Refresh reader.
        reader.UpdateImages(true);
        page.UpdatePage(reader);
    }

    public void UpdateProgress(ReaderView reader, bool save)
    {
        double page = reader.CurrentPosition;

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

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }
}
