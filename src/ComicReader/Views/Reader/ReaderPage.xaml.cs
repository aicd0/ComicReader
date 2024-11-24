// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#if DEBUG
//#define DEBUG_LOG_POINTER
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Debug;
using ComicReader.Common.KVStorage;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.SimpleImageView;
using ComicReader.Common.Threading;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Router;
using ComicReader.Views.Base;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage;
using Windows.System;

namespace ComicReader.Views.Reader;

internal class ReaderPageShared : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    // Comic info
    private string m_ComicTitle1;
    public string ComicTitle1
    {
        get => m_ComicTitle1;
        set
        {
            m_ComicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicTitle1"));
        }
    }

    private string m_ComicTitle2;
    public string ComicTitle2
    {
        get => m_ComicTitle2;
        set
        {
            m_ComicTitle2 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicTitle2"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsComicTitle2Visible"));
        }
    }

    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;

    private string m_ComicDir;
    public string ComicDir
    {
        get => m_ComicDir;
        set
        {
            m_ComicDir = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicDir"));
        }
    }

    private bool m_CanDirOpenInFileExplorer = false;
    public bool CanDirOpenInFileExplorer
    {
        get => m_CanDirOpenInFileExplorer;
        set
        {
            m_CanDirOpenInFileExplorer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanDirOpenInFileExplorer"));
        }
    }

    private ObservableCollection<TagCollectionViewModel> m_ComicTags;
    public ObservableCollection<TagCollectionViewModel> ComicTags
    {
        get => m_ComicTags;
        set
        {
            m_ComicTags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicTags"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsComicTagsVisible"));
        }
    }

    public bool IsComicTagsVisible => ComicTags != null && ComicTags.Count > 0;

    private bool m_IsEditable;
    public bool IsEditable
    {
        get => m_IsEditable;
        set
        {
            m_IsEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditable"));
        }
    }

    // Reading record
    private double m_Rating;
    public double Rating
    {
        get => m_Rating;
        set
        {
            m_Rating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Rating"));
        }
    }

    // Fullscreen
    private bool m_IsFullscreen = false;
    public bool IsFullscreen
    {
        get => m_IsFullscreen;
        set
        {
            m_IsFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsFullscreen)}"));
        }
    }
}

internal sealed partial class ReaderPage : BasePage
{
    private const string KEY_TIP_SHOWN = "ReaderTipShown";

    private bool? _isFavorite = null;

    private readonly TaskQueueDispatcher _loadPreviewDispatcher = new(new TaskQueue("ReaderLoadPreview"), "");

    public ReaderPageShared Shared { get; set; } = new ReaderPageShared();
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

    public readonly TagItemHandler _tagItemHandler;

    // Bottom Tile
    private bool mBottomTileShowed = false;
    private bool mBottomTileHold = false;
    private bool mBottomTilePointerIn = false;
    private DateTimeOffset mBottomTileHideRequestTime = DateTimeOffset.Now;

    public ReaderPage()
    {
        InitializeComponent();

        _tagItemHandler = new TagItemHandler(this);

        Shared.ComicTitle1 = "";
        Shared.ComicTitle2 = "";
        Shared.ComicDir = "";
        Shared.ComicTags = new ObservableCollection<TagCollectionViewModel>();
        Shared.IsEditable = false;

        PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

        VerticalReader.SetIsVertical(true);
        HorizontalReader.SetIsVertical(false);

        ReaderView[] readers = [VerticalReader, HorizontalReader];
        foreach (ReaderView reader in readers)
        {
            reader.ReaderEventTapped += delegate
            {
                BottomTileSetHold(!mBottomTileShowed);
            };
            reader.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
            {
                UpdatePage(sender);
                UpdateProgress(sender, save: !isIntermediate);
                BottomTileSetHold(false);
            };
        }
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        C0.Run(async delegate
        {
            bool tipShown = KVDatabase.GetInstance().GetDefaultMethod().GetBoolean(KVLib.TIPS, KEY_TIP_SHOWN, false);
            if (!tipShown)
            {
                ReaderTip.IsOpen = !tipShown;
            }

            long comic_id = bundle.GetLong(RouterConstants.ARG_COMIC_ID, -1);
            ComicData comic = await ComicData.FromId(comic_id, "ReaderGetComic");
            if (comic == null)
            {
                string token = bundle.GetString(RouterConstants.ARG_COMIC_TOKEN, "");
                comic = AppDataRepository.GetComicData(token);
            }

            if (comic != null)
            {
                GetMainPageAbility().SetTitle(comic.Title);
            }
            GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });

            ReaderSettingDataModel readerSetting = _readerSettingsLiveData.GetValue();
            VerticalReader.SetVisibility(readerSetting.IsVertical);
            HorizontalReader.SetVisibility(!readerSetting.IsVertical);
            ReaderView reader = GetReader();
            reader.SetIsContinuous(readerSetting.IsContinuous);
            reader.SetPageArrangement(readerSetting.PageArrangement);
            reader.SetFlowDirection(readerSetting.IsLeftToRight);
            await LoadComic(comic, reader);

            // Update previews.
            double preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
            double preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
            PreviewDataSource.Clear();
            for (int i = 0; i < comic.ImageCount; ++i)
            {
                PreviewDataSource.Add(new ReaderImagePreviewViewModel
                {
                    Image = new SimpleImageView.Model
                    {
                        Source = new ComicImageSource(comic, i),
                        Width = preview_width,
                        Height = preview_height,
                        Dispatcher = _loadPreviewDispatcher,
                        Callback = new LoadPreviewCallback(i),
                        DebugDescription = i.ToString()
                    },
                    Page = i + 1,
                });
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
        GetNavigationPageAbility().SetGridViewMode(false);
        GetNavigationPageAbility().SetReaderSettings(ReaderSettingsLiveData.GetValue());
        UpdateReaderUI();

        ComicData comic = GetComic();
        if (comic != null && !comic.IsExternal)
        {
            AppStatusPreserver.SetReadingComic(comic.Id);
        }

        C0.Run(async delegate
        {
            await LoadComicInfo();
        });
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    private void ObserveData()
    {
        GetMainPageAbility().RegisterTabUnselectedHandler(this, AppStatusPreserver.UnsetReadingComic);
        GetNavigationPageAbility().RegisterLeavingHandler(this, AppStatusPreserver.UnsetReadingComic);

        GetNavigationPageAbility().RegisterGridViewModeChangedHandler(this, delegate (bool enabled)
        {
            GridViewModeEnabled = enabled;
        });

        GetNavigationPageAbility().RegisterExpandInfoPaneHandler(this, delegate
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        });

        GetMainPageAbility().RegisterFullscreenChangedHandler(this, delegate (bool isFullscreen)
        {
            Shared.IsFullscreen = isFullscreen;
        });

        EventBus.Default.With<double>(EventId.TitleBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            TitleBarArea.Height = h;
            PreviewTitleBarPlaceHolder.Height = h;
        });

        EventBus.Default.With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            BottomGrid.Opacity = opacity;
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, delegate (ReaderSettingDataModel setting)
        {
            ReaderSettingDataModel lastSetting = ReaderSettingsLiveData.GetValue();
            SetReaderSettings(setting);
            HorizontalReader.SetFlowDirection(setting.IsLeftToRight);
            UpdateReaderUI();

            if (lastSetting.IsVertical != setting.IsVertical)
            {
                OnReaderSwitched();
            }

            if (lastSetting.IsContinuous != setting.IsContinuous)
            {
                ReaderView reader = GetReader();
                reader.SetIsContinuous(setting.IsContinuous);
            }

            if (lastSetting.PageArrangement != setting.PageArrangement)
            {
                ReaderView reader = GetReader();
                reader.SetPageArrangement(setting.PageArrangement);
            }
        });

        IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            FavoriteBt.IsEnabled = !isExternal;
            GetNavigationPageAbility().SetExternalComic(isExternal);
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            FavoriteBt.IsChecked = isFavorite;
            SetIsFavorite(isFavorite);
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, SetReaderSettings);

        ReaderStatusLiveData.Observe(this, delegate (ReaderStatusEnum status)
        {
            string readerStatusText = "";
            readerStatusText = status switch
            {
                ReaderStatusEnum.Loading => StringResourceProvider.GetResourceString("ReaderStatusLoading"),
                ReaderStatusEnum.Error => StringResourceProvider.GetResourceString("ReaderStatusError"),
                _ => "",
            };
            TbReaderStatus.Text = readerStatusText;
            TbReaderStatus.Visibility = readerStatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateReaderUI();
        });

        GridViewVisibleLiveData.Observe(this, delegate (bool visible)
        {
            GGridView.IsHitTestVisible = visible;
            GGridView.Opacity = visible ? 1 : 0;
            GMainSection.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        });

        VerticalReaderVisibleLiveData.Observe(this, delegate (bool visible)
        {
            VerticalReader.SetVisibility(visible);
        });

        HorizontalReaderVisibleLiveData.Observe(this, delegate (bool visible)
        {
            HorizontalReader.SetVisibility(visible);
        });
    }

    // Utilities
    public ReaderView GetReader()
    {
        if (ReaderSettingsLiveData.GetValue().IsVertical)
        {
            return VerticalReader;
        }
        else
        {
            return HorizontalReader;
        }
    }

    public void UpdatePage(ReaderView reader)
    {
        if (PageIndicator == null)
        {
            return;
        }

        int currentPage = reader.CurrentPageDisplay;
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
    }

    // Reader
    public void OnReaderSwitched()
    {
        ReaderView reader = GetReader();

        ReaderView last_reader = reader.IsVertical ? HorizontalReader : VerticalReader;
        System.Diagnostics.Debug.Assert(last_reader != null);

        if (last_reader == null)
        {
            return;
        }

        ReaderView.ScrollManager.BeginTransaction(reader, "RestoreStateAfterReaderSwitched")
            .CopyFrom(last_reader)
            .Commit();
        UpdateReaderUI();
    }

    // Preview
    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        GridViewModeEnabled = false;

        ReaderView reader = GetReader();
        ReaderView.ScrollManager.BeginTransaction(reader, "JumpToGridItem")
            .Page(ctx.Page)
            .Commit();
    }

    // Pointer events
    private void OnFavoritesChecked(object sender, RoutedEventArgs e)
    {
        SetIsFavorite(true);
    }

    private void OnFavoritesUnchecked(object sender, RoutedEventArgs e)
    {
        SetIsFavorite(false);
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        GetComic().SaveRating((int)sender.Value);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            ComicData comic = GetComic();

            StorageFolder folder = await Storage.TryGetFolder(comic.Location);

            if (folder != null)
            {
                _ = await Launcher.LaunchFolderAsync(folder);
            }
        });
    }

    private void OnInfoPaneTagClicked(object sender, RoutedEventArgs e)
    {
        var ctx = (TagViewModel)((Button)sender).DataContext;
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, "<tag: " + ctx.Tag + ">");
        MainPage.Current.OpenInNewTab(route);
    }

    private void OnEditInfoClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            ComicData comic = GetComic();
            if (comic == null)
            {
                return;
            }

            var dialog = new EditComicInfoDialog(comic);
            ContentDialogResult result = await C0.ShowDialogAsync(dialog, XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                await LoadComicInfo();
            }
        });
    }

    // Bottom Tile
    public void BottomTileShow()
    {
        if (mBottomTileShowed)
        {
            return;
        }

        MainPage.Current.ShowOrHideTitleBar(true);
        mBottomTileShowed = true;
    }

    public void BottomTileHide(int timeout)
    {
        mBottomTileHideRequestTime = DateTimeOffset.Now;

        if (timeout > 0)
        {
            _ = Task.Run(() =>
            {
                Task.Delay(timeout + 1).Wait();

                if ((DateTimeOffset.Now - mBottomTileHideRequestTime).TotalMilliseconds < timeout)
                {
                    return;
                }

                _ = MainThreadUtils.RunInMainThread(delegate
                {
                    BottomTileHide(0);
                });
            });
            return;
        }

        if (!mBottomTileShowed || mBottomTileHold || mBottomTilePointerIn || InfoPane.IsPaneOpen)
        {
            return;
        }

        if (GridViewModeEnabled)
        {
            return;
        }

        if (GetNavigationPageAbility().GetIsSidePaneOpen())
        {
            return;
        }

        BottomGridForceHide();
    }

    private void BottomGridForceHide()
    {
        MainPage.Current.ShowOrHideTitleBar(false);
        mBottomTileShowed = false;
        mBottomTileHold = false;
    }

    private void BottomTileSetHold(bool val)
    {
        mBottomTileHold = val;

        if (mBottomTileHold)
        {
            BottomTileShow();
        }
        else
        {
            BottomTileHide(0);
        }
    }

    private void OnBottomGridPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnInfoPanePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnTitleBarAreaPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnReaderPointerExited()
    {
        mBottomTilePointerIn = true;
        BottomTileShow();
    }

    private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        mBottomTilePointerIn = false;

        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        if (!mBottomTileShowed || mBottomTileHold)
        {
            return;
        }

        BottomTileHide(3000);
    }

    // Tips
    private void OnReaderTipCloseButtonClick(InfoBar sender, object args)
    {
        KVDatabase.GetInstance().GetDefaultMethod().SetBoolean(KVLib.TIPS, KEY_TIP_SHOWN, true);
    }

    // Fullscreen
    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        MainPage.Current.EnterFullscreen();
        Shared.IsFullscreen = true;
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        MainPage.Current.ExitFullscreen();
        Shared.IsFullscreen = false;
    }

    // Debug
    public void Log(string message)
    {
        Logger.I("ReaderPage", message);
    }

    public class TagItemHandler : TagViewModel.IItemHandler
    {
        private readonly ReaderPage _page;

        public TagItemHandler(ReaderPage page)
        {
            _page = page;
        }

        public void OnClicked(object sender, RoutedEventArgs e)
        {
            _page.OnInfoPaneTagClicked(sender, e);
        }
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder.SetModel(item, args.InRecycleQueue);
    }

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

    public async Task LoadComic(ComicData comic, ReaderView reader)
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

        reader.StopLoadingImages();
        reader.SetReaderStateChangeHandler(delegate (ReaderView.ReaderState state)
        {
            switch (state)
            {
                case ReaderView.ReaderState.Ready:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
                    UpdatePage(reader);
                    BottomTileShow();
                    BottomTileHide(5000);
                    break;
                case ReaderView.ReaderState.Loading:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);
                    break;
                case ReaderView.ReaderState.Error:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                    break;
            }
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
                Log("Failed to load images of '" + _comic.Location + "'. " + result.ToString());
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                return;
            }
        }

        // Load info.
        await LoadComicInfo();

        // Load image frames.
        if (!_comic.IsExternal)
        {
            // Set initial page.
            reader.SetInitialPage(_comic.LastPosition);
        }

        var images = new List<IImageSource>();
        for (int i = 0; i < _comic.ImageCount; ++i)
        {
            images.Add(new ComicImageSource(comic, i));
        }
        reader.StartLoadingImages(images);

        // Refresh reader.
        UpdatePage(reader);
    }

    public void UpdateProgress(ReaderView reader, bool save)
    {
        double page = reader.CurrentPage;

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
        if (_isFavorite == isFavorite)
        {
            return;
        }
        _isFavorite = isFavorite;

        GetNavigationPageAbility().SetFavorite(isFavorite);

        if (!_comic.IsExternal)
        {
            C0.Run(async delegate
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

    public async Task LoadComicInfo()
    {
        if (_comic == null)
        {
            return;
        }

        _isExternalComicLiveData.Emit(_comic.IsExternal);

        if (_comic.Title1.Length == 0)
        {
            Shared.ComicTitle1 = _comic.Title;
        }
        else
        {
            Shared.ComicTitle1 = _comic.Title1;
            Shared.ComicTitle2 = _comic.Title2;
        }

        Shared.ComicDir = _comic.Location;
        Shared.CanDirOpenInFileExplorer = _comic is ComicFolderData;
        Shared.IsEditable = _comic.IsEditable;

        LoadComicTag();

        bool isFavorite = !_comic.IsExternal && await FavoriteDataManager.FromId(_comic.Id) != null;
        SetIsFavorite(isFavorite);

        if (!_comic.IsExternal)
        {
            Shared.Rating = _comic.Rating;
        }
    }

    private void LoadComicTag()
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
                    ItemHandler = _tagItemHandler
                };
                tags_model.Tags.Add(tag_model);
            }

            new_collection.Add(tags_model);
        }

        Shared.ComicTags = new_collection;
    }

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }

    private class LoadPreviewCallback(int index) : SimpleImageView.IImageCallback
    {
        private readonly long _startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        public void OnSuccess(BitmapImage image)
        {
            long loadTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _startTime;
            Logger.I(LogTag.N("ReaderVertical", "LoadTime", "LoadPreview"), $"time={loadTime},index={index}");
        }
    }
}
