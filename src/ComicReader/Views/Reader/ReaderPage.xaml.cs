// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#if DEBUG
//#define DEBUG_LOG_POINTER
#endif

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Common.SimpleImageView;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Router;
using ComicReader.Utils;
using ComicReader.Utils.KVDatabase;
using ComicReader.Utils.Lifecycle;
using ComicReader.Views.Base;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.Storage;
using Windows.System;

using static ComicReader.Views.Reader.ReaderPageViewModel;

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

internal class ReaderPageBase : BasePage<ReaderPageViewModel>;

internal sealed partial class ReaderPage : ReaderPageBase
{
    private const string KEY_TIP_SHOWN = "ReaderTipShown";

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
        VerticalReader.Controller = new ReaderViewController(ViewModel, "Vertical", true);
        HorizontalReader.SetIsVertical(false);
        HorizontalReader.Controller = new ReaderViewController(ViewModel, "Horizontal", false);

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
                ViewModel.UpdateProgress(sender.Controller, save: !isIntermediate);
                BottomTileSetHold(false);
            };
        }
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        Utils.C0.Run(async delegate
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
            await ViewModel.LoadComic(comic, this, HorizontalReader, VerticalReader);

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
                        Dispatcher = new TaskQueueDispatcher(TaskQueue.DefaultQueue, "ReaderLoadPreview"),
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
        GetNavigationPageAbility().SetReaderSettings(ViewModel.ReaderSettingsLiveData.GetValue());
        ViewModel.UpdateReaderUI();

        ComicData comic = ViewModel.GetComic();
        if (comic != null && !comic.IsExternal)
        {
            AppStatusPreserver.SetReadingComic(comic.Id);
        }

        Utils.C0.Run(async delegate
        {
            await ViewModel.LoadComicInfo(this);
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
            ViewModel.GridViewModeEnabled = enabled;
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
            ReaderSettingDataModel lastSetting = ViewModel.ReaderSettingsLiveData.GetValue();
            ViewModel.SetReaderSettings(setting);
            HorizontalReader.SetFlowDirection(setting.IsLeftToRight);
            ViewModel.UpdateReaderUI();

            if (lastSetting.IsVertical != setting.IsVertical)
            {
                OnReaderSwitched();
            }

            if (lastSetting.IsContinuous != setting.IsContinuous)
            {
                ReaderView reader = GetReader();
                reader.SetIsContinuous(setting.IsContinuous);
                reader.Controller?.OnPageRearrangeEventSealed();
            }

            if (lastSetting.PageArrangement != setting.PageArrangement)
            {
                ReaderView reader = GetReader();
                reader.Controller?.OnPageRearrangeEventSealed();
            }
        });

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            FavoriteBt.IsEnabled = !isExternal;
            GetNavigationPageAbility().SetExternalComic(isExternal);
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            FavoriteBt.IsChecked = isFavorite;
            ViewModel.SetIsFavorite(isFavorite);
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, ViewModel.SetReaderSettings);

        ViewModel.ReaderStatusLiveData.Observe(this, delegate (ReaderStatusEnum status)
        {
            string readerStatusText = "";
            readerStatusText = status switch
            {
                ReaderStatusEnum.Loading => Utils.StringResourceProvider.GetResourceString("ReaderStatusLoading"),
                ReaderStatusEnum.Error => Utils.StringResourceProvider.GetResourceString("ReaderStatusError"),
                _ => "",
            };
            TbReaderStatus.Text = readerStatusText;
            TbReaderStatus.Visibility = readerStatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            ViewModel.UpdateReaderUI();
        });

        ViewModel.GridViewVisibleLiveData.Observe(this, delegate (bool visible)
        {
            GGridView.IsHitTestVisible = visible;
            GGridView.Opacity = visible ? 1 : 0;
            GMainSection.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        });

        ViewModel.VerticalReaderVisibleLiveData.Observe(this, delegate (bool visible)
        {
            VerticalReader.SetVisibility(visible);
        });

        ViewModel.HorizontalReaderVisibleLiveData.Observe(this, delegate (bool visible)
        {
            HorizontalReader.SetVisibility(visible);
        });
    }

    // Utilities
    public ReaderView GetReader()
    {
        if (ViewModel.ReaderSettingsLiveData.GetValue().IsVertical)
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

        int currentPage = reader.CurrentPage;
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
    }

    // Reader
    public void OnReaderSwitched()
    {
        Utils.C0.Run(async delegate
        {
            ReaderView reader = GetReader();
            if (reader.Controller == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            ReaderViewController last_reader = reader.Controller.IsVertical ? HorizontalReader.Controller : VerticalReader.Controller;
            System.Diagnostics.Debug.Assert(last_reader != null);

            if (last_reader == null)
            {
                return;
            }

            System.Diagnostics.Debug.Assert(reader.Controller.IsCurrentReader);
            System.Diagnostics.Debug.Assert(!last_reader.IsCurrentReader);

            double page = last_reader.PageSource;
            float zoom = Math.Min(100f, last_reader.Zoom);

            await Utils.C0.WaitFor(() => reader.Controller.Loaded, 1000);
            ReaderViewController.ScrollManager.BeginTransaction(reader.Controller, "RestoreStateAfterReaderSwitched")
                .Zoom(zoom)
                .Page(page)
                .Commit();
            await reader.Controller.UpdateImages(true);
            ViewModel.UpdateReaderUI();
            await last_reader.UpdateImages(false);
        });
    }

    // Preview
    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        ViewModel.GridViewModeEnabled = false;

        ReaderView reader = GetReader();
        ReaderViewController.ScrollManager.BeginTransaction(reader.Controller, "JumpToGridItem")
            .Page(ctx.Page)
            .Commit();
    }

    // Pointer events
    private void OnFavoritesChecked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetIsFavorite(true);
    }

    private void OnFavoritesUnchecked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetIsFavorite(false);
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        ViewModel.GetComic().SaveRating((int)sender.Value);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            ComicData comic = ViewModel.GetComic();

            StorageFolder folder = await Utils.Storage.TryGetFolder(comic.Location);

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
        Utils.C0.Run(async delegate
        {
            ComicData comic = ViewModel.GetComic();
            if (comic == null)
            {
                return;
            }

            var dialog = new EditComicInfoDialog(comic);
            ContentDialogResult result = await C0.ShowDialogAsync(dialog, XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.LoadComicInfo(this);
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

                _ = Threading.RunInMainThread(delegate
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

        if (ViewModel.GridViewModeEnabled)
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
}
