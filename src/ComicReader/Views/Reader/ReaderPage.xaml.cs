// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Imaging;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

using Windows.Storage;
using Windows.System;

namespace ComicReader.Views.Reader;

internal class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private string _comicTitle1;
    public string ComicTitle1
    {
        get => _comicTitle1;
        set
        {
            _comicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle1)));
        }
    }

    private string _comicTitle2;
    public string ComicTitle2
    {
        get => _comicTitle2;
        set
        {
            _comicTitle2 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTitle2Visible)));
        }
    }

    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;

    private string _comicDir;
    public string ComicDir
    {
        get => _comicDir;
        set
        {
            _comicDir = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicDir)));
        }
    }

    private bool _canDirOpenInFileExplorer = false;
    public bool CanDirOpenInFileExplorer
    {
        get => _canDirOpenInFileExplorer;
        set
        {
            _canDirOpenInFileExplorer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanDirOpenInFileExplorer)));
        }
    }

    private ObservableCollection<TagCollectionViewModel> _comicTags;
    public ObservableCollection<TagCollectionViewModel> ComicTags
    {
        get => _comicTags;
        set
        {
            _comicTags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTags)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTagsVisible)));
        }
    }

    public bool IsComicTagsVisible => ComicTags != null && ComicTags.Count > 0;

    private bool _isEditable;
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    private double _rating;
    public double Rating
    {
        get => _rating;
        set
        {
            _rating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rating)));
        }
    }

    private bool _isFullscreen = false;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFullscreen)));
        }
    }

    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }
}

internal sealed partial class ReaderPage : BasePage
{
    //
    // Constants
    //

    private const string KEY_TIP_SHOWN = "ReaderTipShown";
    private const string REGEX_URL = "(https?:\\/\\/)?(www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)";

    //
    // Variables
    //

    private bool? _isFavorite = null;
    private ComicModel _comic;
    private IComicConnection _comicConnection;
    private volatile bool _updatingProgress = false;

    private bool _buttomTileShowed = false;
    private bool _buttomTileHold = false;
    private bool _buttomTilePointerIn = false;
    private DateTimeOffset _buttomTileHideRequestTime = DateTimeOffset.Now;

    private readonly ITaskDispatcher _loadPreviewDispatcher = TaskDispatcher.Factory.NewQueue("ReaderLoadPreview");
    private readonly TagItemHandler _tagItemHandler;

    private MutableLiveData<ReaderStatusEnum> ReaderStatusLiveData { get; } = new(ReaderStatusEnum.Loading);

    private readonly MutableLiveData<bool> _isExternalComicLiveData = new(true);
    public LiveData<bool> IsExternalComicLiveData => _isExternalComicLiveData;

    private bool _gridViewModeEnabled = false;
    private bool GridViewModeEnabled
    {
        get => _gridViewModeEnabled;
        set
        {
            _gridViewModeEnabled = value;
            UpdateReaderUI();
            GetNavigationPageAbility().SetGridViewMode(value);
        }
    }

    private ReaderPageViewModel ViewModel { get; set; } = new();

    //
    // Constructor
    //

    public ReaderPage()
    {
        InitializeComponent();

        _tagItemHandler = new TagItemHandler(this);

        ViewModel.ComicTitle1 = "";
        ViewModel.ComicTitle2 = "";
        ViewModel.ComicDir = "";
        ViewModel.ComicTags = new ObservableCollection<TagCollectionViewModel>();
        ViewModel.IsEditable = false;
        ViewModel.PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

        ReaderView reader = MainReaderView;

        reader.ReaderEventTapped += delegate (ReaderView sender)
        {
            BottomTileSetHold(!_buttomTileShowed);
        };

        reader.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
        {
            UpdatePage();
            UpdateProgress(sender, save: !isIntermediate);
            BottomTileSetHold(false);
        };

        reader.ReaderEventReaderStateChanged += delegate (ReaderView sender, ReaderView.ReaderState state)
        {
            switch (state)
            {
                case ReaderView.ReaderState.Ready:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
                    UpdatePage();
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
        };
    }

    //
    // Page Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        C0.Run(async delegate
        {
            bool tipShown = KVDatabase.GetDefaultMethod().GetBoolean(GlobalConstants.KV_DB_TIPS, KEY_TIP_SHOWN, false);
            if (!tipShown)
            {
                ReaderTip.IsOpen = !tipShown;
            }

            long comic_id = bundle.GetLong(RouterConstants.ARG_COMIC_ID, -1);
            ComicModel comic = await ComicModel.FromId(comic_id, "ReaderGetComic");
            if (comic == null)
            {
                string token = bundle.GetString(RouterConstants.ARG_COMIC_TOKEN, "");
                comic = AppModel.GetComicData(token);
            }

            if (comic != null)
            {
                GetMainPageAbility().SetTitle(comic.Title);
            }
            GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });

            ReaderSettingDataModel readerSettingModel = GetReaderSettingModel();
            ReaderView reader = MainReaderView;
            reader.SetIsVertical(readerSettingModel.IsVertical);
            reader.SetIsContinuous(readerSettingModel.IsContinuous);
            reader.SetPageArrangement(readerSettingModel.PageArrangement);
            reader.SetFlowDirection(readerSettingModel.IsLeftToRight);

            await LoadComic(comic);
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
        GetNavigationPageAbility().SetGridViewMode(false);
        GetNavigationPageAbility().SetReaderSettings(GetReaderSettingModel());
        UpdateReaderUI();

        ComicModel comic = _comic;
        if (comic != null && !comic.IsExternal)
        {
            AppModel.SetReadingComic(comic.Id);
        }

        C0.Run(async delegate
        {
            await LoadComicInfo();
        });
    }

    protected override void OnStop()
    {
        base.OnStop();
        ReleaseComicConnection();
    }

    private void ObserveData()
    {
        GetMainPageAbility().RegisterTabUnselectedHandler(this, AppModel.UnsetReadingComic);
        GetNavigationPageAbility().RegisterLeavingHandler(this, AppModel.UnsetReadingComic);

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
            ViewModel.IsFullscreen = isFullscreen;
        });

        GetEventBus().With<double>(EventId.TitleBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            TitleBarArea.Height = h;
            PreviewTitleBarPlaceHolder.Height = h;
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            BottomGrid.Opacity = opacity;
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, delegate (ReaderSettingDataModel setting)
        {
            ReaderView reader = MainReaderView;
            reader.SetIsVertical(setting.IsVertical);
            reader.SetFlowDirection(setting.IsLeftToRight);
            reader.SetIsContinuous(setting.IsContinuous);
            reader.SetPageArrangement(setting.PageArrangement);
            UpdateReaderUI();
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
    }

    //
    // Loader
    //

    public async Task LoadComic(ComicModel comic)
    {
        if (comic == _comic)
        {
            return;
        }

        ReleaseComicConnection();
        _comic = comic;

        if (comic == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        IComicConnection connection = await comic.OpenComicAsync();
        _comicConnection = connection;

        if (connection == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

        await LoadComicInfo();

        if (!_comic.IsExternal)
        {
            _comic.SetAsStarted();
            await HistoryDataManager.Add(_comic.Id, _comic.Title1, true);

            TaskException result = await _comic.ReloadImageFiles();
            if (!result.Successful())
            {
                Log("Failed to load images of '" + _comic.Location + "'. " + result.ToString());
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                return;
            }

            MainReaderView.SetInitialPage(_comic.LastPosition);
        }

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }
        MainReaderView.StartLoadingImages(images);

        // Update previews
        double preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        ViewModel.PreviewDataSource.Clear();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            ViewModel.PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(comic, connection, i),
                    Width = preview_width,
                    Height = preview_height,
                    Dispatcher = _loadPreviewDispatcher,
                    DebugDescription = i.ToString()
                },
                Page = i + 1,
            });
        }
    }

    private void ReleaseComicConnection()
    {
        _comicConnection?.Dispose();
        _comicConnection = null;
    }

    private async Task LoadComicInfo()
    {
        if (_comic == null)
        {
            return;
        }

        _isExternalComicLiveData.Emit(_comic.IsExternal);

        if (_comic.Title1.Length == 0)
        {
            ViewModel.ComicTitle1 = _comic.Title;
        }
        else
        {
            ViewModel.ComicTitle1 = _comic.Title1;
            ViewModel.ComicTitle2 = _comic.Title2;
        }

        LoadDescription(_comic.Description);
        ViewModel.ComicDir = _comic.Location;
        ViewModel.CanDirOpenInFileExplorer = _comic.IsDirectory;
        ViewModel.IsEditable = _comic.IsEditable;

        LoadComicTag();

        bool isFavorite = !_comic.IsExternal && await FavoriteModel.Instance.FromId(_comic.Id) != null;
        SetIsFavorite(isFavorite);

        if (!_comic.IsExternal)
        {
            ViewModel.Rating = _comic.Rating;
        }
    }

    private void LoadDescription(string description)
    {
        InlineCollection inlines = TbComicDescription.Inlines;
        inlines.Clear();

        Regex urlRegex = new(REGEX_URL, RegexOptions.None);
        MatchCollection matches = urlRegex.Matches(description);
        int currentIndex = 0;

        foreach (Match match in matches)
        {
            Uri uri;
            try
            {
                uri = new Uri(match.Value);
            }
            catch (Exception)
            {
                continue;
            }

            int startIndex = match.Index;
            int endIndex = match.Index + match.Length;

            if (endIndex <= currentIndex)
            {
                continue;
            }

            if (startIndex > currentIndex)
            {
                var run = new Run
                {
                    Text = description[currentIndex..startIndex]
                };
                inlines.Add(run);
            }

            {
                var run = new Run
                {
                    Text = match.Value
                };
                var hyperlink = new Hyperlink
                {
                    NavigateUri = uri
                };
                hyperlink.Inlines.Add(run);
                inlines.Add(hyperlink);
            }

            currentIndex = endIndex;
        }

        if (currentIndex < description.Length)
        {
            var run = new Run
            {
                Text = description[currentIndex..]
            };
            inlines.Add(run);
        }

        if (inlines.Count == 0)
        {
            TbComicDescription.Visibility = Visibility.Collapsed;
        }
        else
        {
            TbComicDescription.Visibility = Visibility.Visible;
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
            ComicData.TagData tags = _comic.Tags[i];
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

        ViewModel.ComicTags = new_collection;
    }

    private ReaderSettingDataModel GetReaderSettingModel()
    {
        return new ReaderSettingDataModel
        {
            IsVertical = XmlDatabase.Settings.VerticalReading,
            IsLeftToRight = XmlDatabase.Settings.LeftToRight,
            IsVerticalContinuous = XmlDatabase.Settings.VerticalContinuous,
            IsHorizontalContinuous = XmlDatabase.Settings.HorizontalContinuous,
            VerticalPageArrangement = XmlDatabase.Settings.VerticalPageArrangement,
            HorizontalPageArrangement = XmlDatabase.Settings.HorizontalPageArrangement,
        };
    }

    //
    // UI
    //

    public void UpdateReaderUI()
    {
        bool isWorking = ReaderStatusLiveData.GetValue() == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && _gridViewModeEnabled;
        bool readerVisible = isWorking && !previewVisible;

        GGridView.IsHitTestVisible = previewVisible;
        GGridView.Opacity = previewVisible ? 1 : 0;
        GMainSection.Visibility = previewVisible ? Visibility.Collapsed : Visibility.Visible;
        MainReaderView.SetVisibility(readerVisible);
    }

    private void UpdatePage()
    {
        if (PageIndicator == null)
        {
            return;
        }

        ReaderView reader = MainReaderView;
        int currentPage = reader.CurrentPageDisplay;
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
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

    //
    // Events
    //

    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        GridViewModeEnabled = false;

        ReaderView.ScrollManager.BeginTransaction(MainReaderView, "JumpToGridItem")
            .Page(ctx.Page)
            .Commit();
    }

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
        _comic.SaveRating((int)sender.Value);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            ComicModel comic = _comic;

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
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, "<tag: " + ctx.Tag + ">");
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void OnEditInfoClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            ComicModel comic = _comic;
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

    private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _buttomTilePointerIn = false;

        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        if (!_buttomTileShowed || _buttomTileHold)
        {
            return;
        }

        BottomTileHide(3000);
    }

    private void OnReaderTipCloseButtonClick(InfoBar sender, object args)
    {
        KVDatabase.GetDefaultMethod().SetBoolean(GlobalConstants.KV_DB_TIPS, KEY_TIP_SHOWN, true);
    }

    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().EnterFullscreen();
        ViewModel.IsFullscreen = true;
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().ExitFullscreen();
        ViewModel.IsFullscreen = false;
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder.SetModel(item, args.InRecycleQueue);
    }

    //
    // Bottom Tile
    //

    private void OnReaderPointerExited()
    {
        _buttomTilePointerIn = true;
        BottomTileShow();
    }

    public void BottomTileShow()
    {
        if (_buttomTileShowed)
        {
            return;
        }

        GetMainPageAbility().ShowOrHideTitleBar(true);
        _buttomTileShowed = true;
    }

    public void BottomTileHide(int timeout)
    {
        _buttomTileHideRequestTime = DateTimeOffset.Now;

        if (timeout > 0)
        {
            _ = Task.Run(() =>
            {
                Task.Delay(timeout + 1).Wait();

                if ((DateTimeOffset.Now - _buttomTileHideRequestTime).TotalMilliseconds < timeout)
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

        if (!_buttomTileShowed || _buttomTileHold || _buttomTilePointerIn || InfoPane.IsPaneOpen)
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
        GetMainPageAbility().ShowOrHideTitleBar(false);
        _buttomTileShowed = false;
        _buttomTileHold = false;
    }

    private void BottomTileSetHold(bool val)
    {
        _buttomTileHold = val;

        if (_buttomTileHold)
        {
            BottomTileShow();
        }
        else
        {
            BottomTileHide(0);
        }
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
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
                    await FavoriteModel.Instance.Add(_comic.Id, _comic.Title1, true);
                }
                else
                {
                    await FavoriteModel.Instance.RemoveWithId(_comic.Id, true);
                }
            });
        }
    }

    public void Log(string message)
    {
        Logger.I("ReaderPage", message);
    }

    //
    // Classes
    //

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

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }
}
