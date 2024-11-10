#if DEBUG
//#define DEBUG_LOG_POINTER
#endif

using ComicReader.Common;
using ComicReader.Common.Constants;
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
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
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

sealed internal partial class ReaderPage : ReaderPageBase
{
    private const string KEY_TIP_SHOWN = "ReaderTipShown";

    public ReaderPageShared Shared { get; set; } = new ReaderPageShared();
    public ReaderViewController VerticalReader { get; set; }
    public ReaderViewController HorizontalReader { get; set; }
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

    public readonly TagItemHandler _tagItemHandler;
    private readonly GestureHandler _gestureHandler;

    // Pointer events
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();
    private bool mPendingTap = false;
    private bool mTapCancelled = false;

    // Bottom Tile
    private bool mBottomTileShowed = false;
    private bool mBottomTileHold = false;
    private bool mBottomTilePointerIn = false;
    private DateTimeOffset mBottomTileHideRequestTime = DateTimeOffset.Now;

    public ReaderPage()
    {
        _tagItemHandler = new TagItemHandler(this);
        _gestureHandler = new GestureHandler(this);
        _gestureRecognizer.SetHandler(_gestureHandler);

        Shared.ComicTitle1 = "";
        Shared.ComicTitle2 = "";
        Shared.ComicDir = "";
        Shared.ComicTags = new ObservableCollection<TagCollectionViewModel>();
        Shared.IsEditable = false;

        VerticalReader = new ReaderViewController(ViewModel, "Vertical", true);
        HorizontalReader = new ReaderViewController(ViewModel, "Horizontal", false);
        PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

        InitializeComponent();
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
            _ = ViewModel.LoadComic(comic, this);
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
        GetNavigationPageAbility().SetGridViewMode(false);
        GetNavigationPageAbility().SetReaderSettings(ViewModel.ReaderSettingsLiveData.GetValue());
        OnReaderContinuousChanged();
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

    protected override void OnPause()
    {
        base.OnPause();
        HorizontalReader.StopLoadingImage();
        VerticalReader.StopLoadingImage();
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
            SvHorizontalReader.FlowDirection = setting.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            ViewModel.UpdateReaderUI();

            if (lastSetting.IsVertical != setting.IsVertical)
            {
                OnReaderSwitched();
            }

            if (lastSetting.IsContinuous != setting.IsContinuous)
            {
                OnReaderContinuousChanged();
                GetCurrentReader()?.OnPageRearrangeEventSealed();
            }

            if (lastSetting.PageArrangement != setting.PageArrangement)
            {
                GetCurrentReader()?.OnPageRearrangeEventSealed();
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
            SvVerticalReader.IsEnabled = visible;
            SvVerticalReader.IsHitTestVisible = visible;
            SvVerticalReader.Opacity = visible ? 1 : 0;
        });

        ViewModel.HorizontalReaderVisibleLiveData.Observe(this, delegate (bool visible)
        {
            SvHorizontalReader.IsEnabled = visible;
            SvHorizontalReader.IsHitTestVisible = visible;
            SvHorizontalReader.Opacity = visible ? 1 : 0;
        });
    }

    // Utilities
    public ReaderViewController GetCurrentReader()
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

    public void UpdatePage(ReaderViewController reader)
    {
        if (PageIndicator == null)
        {
            return;
        }

        int currentPage = reader.GetCurrentPage();
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
    }

    // Reader
    public void OnReaderSwitched()
    {
        Utils.C0.Run(async delegate
        {
            ReaderViewController reader = GetCurrentReader();
            if (reader == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            OnReaderContinuousChanged();
            ReaderViewController last_reader = reader.IsVertical ? HorizontalReader : VerticalReader;
            System.Diagnostics.Debug.Assert(last_reader != null);

            if (last_reader == null)
            {
                return;
            }

            System.Diagnostics.Debug.Assert(reader.IsCurrentReader);
            System.Diagnostics.Debug.Assert(!last_reader.IsCurrentReader);

            double page = last_reader.PageSource;
            float zoom = Math.Min(100f, last_reader.Zoom);

            await Utils.C0.WaitFor(() => reader.Loaded, 1000);
            ReaderViewController.ScrollManager.BeginTransaction(reader, "RestoreStateAfterReaderSwitched")
                .Zoom(zoom)
                .Page(page)
                .Commit();
            await reader.UpdateImages(true);
            ViewModel.UpdateReaderUI();
            await last_reader.UpdateImages(false);
        });
    }

    private void OnReaderContinuousChanged()
    {
        _gestureRecognizer.AutoProcessInertia = ViewModel.ReaderSettingsLiveData.GetValue().IsContinuous;
    }

    private void OnReaderScrollViewerViewChanged(ReaderViewController control, ScrollViewerViewChangedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            if (await control.OnViewChanged(!e.IsIntermediate))
            {
                UpdatePage(control);
                ViewModel.UpdateProgress(control, save: !e.IsIntermediate);
                BottomTileSetHold(false);
            }
        });
    }

    private void OnReaderScrollViewerSizeChanged(ReaderViewController control)
    {
        control.OnSizeChanged();
    }

    private void OnReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            // Ctrl key down indicates the user is zooming the page. In that case we shouldn't handle the event.
            CoreVirtualKeyStates ctrl_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

            if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
            {
                return;
            }

            ReaderViewController reader = GetCurrentReader();

            if (reader == null)
            {
                return;
            }

            await reader.OnReaderScrollViewerPointerWheelChanged(e);
        });
    }

    private void OnReaderContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderFrameViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderFrame;
        viewHolder.Bind(item);
    }

    private void OnVerticalReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        OnReaderScrollViewerViewChanged(VerticalReader, e);
    }

    private void OnHorizontalReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        OnReaderScrollViewerViewChanged(HorizontalReader, e);
    }

    private void OnVerticalReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnReaderScrollViewerSizeChanged(VerticalReader);
    }

    private void OnHorizontalReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnReaderScrollViewerSizeChanged(HorizontalReader);
    }

    private void OnVerticalReaderScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        VerticalReader.ThisScrollViewer = sender as ScrollViewer;
    }

    private void OnVerticalReaderListViewLoaded(object sender, RoutedEventArgs e)
    {
        VerticalReader.ThisListView = sender as ListView;
    }

    private void OnHorizontalReaderScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        HorizontalReader.ThisScrollViewer = sender as ScrollViewer;
    }

    private void OnHorizontalReaderListViewLoaded(object sender, RoutedEventArgs e)
    {
        HorizontalReader.ThisListView = sender as ListView;
    }

    // Preview
    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        ViewModel.GridViewModeEnabled = false;

        ReaderViewController reader = GetCurrentReader();
        if (reader == null)
        {
            return;
        }

        ReaderViewController.ScrollManager.BeginTransaction(reader, "JumpToGridItem")
            .Page(ctx.Page)
            .Commit();
    }

    // Pointer events
    private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        (sender as UIElement).CapturePointer(e.Pointer);
        PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
        _gestureRecognizer.ProcessDownEvent(pointer_point);
#if DEBUG_LOG_POINTER
        Log("Pointer pressed");
#endif
    }

    private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _gestureRecognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));
#if DEBUG_LOG_POINTER
        //Log("Pointer moved");
#endif
    }

    private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
        _gestureRecognizer.ProcessUpEvent(pointer_point);
        (sender as UIElement).ReleasePointerCapture(e.Pointer);

        if (!_gestureRecognizer.AutoProcessInertia)
        {
            _gestureRecognizer.CompleteGesture();
        }

#if DEBUG_LOG_POINTER
        Log("Pointer released");
#endif
    }

    private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
        _gestureRecognizer.ProcessUpEvent(pointer_point);
        (sender as UIElement).ReleasePointerCapture(e.Pointer);

        if (!_gestureRecognizer.AutoProcessInertia)
        {
            _gestureRecognizer.CompleteGesture();
        }

#if DEBUG_LOG_POINTER
        Log("Pointer canceled");
#endif
    }

    private void OnReaderTapped(object sender, TappedEventArgs e)
    {
        if (e.TapCount == 1)
        {
            if (mPendingTap)
            {
                return;
            }

            mPendingTap = true;
            mTapCancelled = false;
            Utils.C0.Run(async delegate
            {
                await Task.Delay(100);
                mPendingTap = false;
                if (mTapCancelled)
                {
                    return;
                }

                BottomTileSetHold(!mBottomTileShowed);
            });
        }
        else if (e.TapCount == 2)
        {
            mTapCancelled = true;
            ReaderViewController reader = GetCurrentReader();
            if (reader == null)
            {
                return;
            }

            if (Math.Abs(reader.Zoom - 100) <= 1)
            {
                ReaderViewController.ScrollManager.BeginTransaction(reader, "FitScreenUsingCenterCrop")
                    .Zoom(100, Common.Structs.ZoomType.CenterCrop)
                    .EnableAnimation()
                    .Commit();
            }
            else
            {
                ReaderViewController.ScrollManager.BeginTransaction(reader, "FitScreenUsingCenterInside")
                    .Zoom(100)
                    .EnableAnimation()
                    .Commit();
            }
        }
    }

    private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
    {
        GetCurrentReader()?.OnReaderManipulationStarted(e);
    }

    private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
    {
        GetCurrentReader()?.OnReaderManipulationUpdated(e);
    }

    private void OnReaderManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        ReaderViewController reader = GetCurrentReader();

        if (reader == null)
        {
            return;
        }

        Utils.C0.Run(async delegate
        {
            await reader.OnReaderManipulationCompleted(e);
        });
    }

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

    // Keys
    private void OnReaderKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ReaderViewController reader = GetCurrentReader();

        if (reader == null)
        {
            return;
        }

        Utils.C0.Run(async delegate
        {
            bool handled = true;

            switch (e.Key)
            {
                case VirtualKey.Right:
                    if (reader.IsHorizontal && !ViewModel.ReaderSettingsLiveData.GetValue().IsLeftToRight)
                    {
                        await reader.MoveFrame(-1, "JumpToPreviousPageUsingRightKey");
                    }
                    else
                    {
                        await reader.MoveFrame(1, "JumpToNextPageUsingRightKey");
                    }

                    break;

                case VirtualKey.Left:
                    if (reader.IsHorizontal && !ViewModel.ReaderSettingsLiveData.GetValue().IsLeftToRight)
                    {
                        await reader.MoveFrame(1, "JumpToNextPageUsingLeftKey");
                    }
                    else
                    {
                        await reader.MoveFrame(-1, "JumpToPreviousPageUsingLeftKey");
                    }

                    break;

                case VirtualKey.Up:
                    await reader.MoveFrame(-1, "JumpToPerviousPageUsingUpKey");
                    break;

                case VirtualKey.Down:
                    await reader.MoveFrame(1, "JumpToNextPageUsingDownKey");
                    break;

                case VirtualKey.PageUp:
                    await reader.MoveFrame(-1, "JumpToPerviousPageUsingPgUpKey");
                    break;

                case VirtualKey.PageDown:
                    await reader.MoveFrame(1, "JumpToNextPageUsingPgDownKey");
                    break;

                case VirtualKey.Home:
                    ReaderViewController.ScrollManager.BeginTransaction(reader, "JumpToFirstPageUsingHomeKey")
                        .Page(1)
                        .Commit();
                    break;

                case VirtualKey.End:
                    ReaderViewController.ScrollManager.BeginTransaction(reader, "JumpToLastPageUsingEndKey")
                        .Page(reader.PageCount)
                        .Commit();
                    break;

                case VirtualKey.Space:
                    await reader.MoveFrame(1, "JumpToNextPageUsingSpaceKey");
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                e.Handled = true;
            }
        });
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

    private class GestureHandler : ReaderGestureRecognizer.IHandler
    {
        private readonly ReaderPage _page;

        public GestureHandler(ReaderPage page)
        {
            _page = page;
        }

        public void ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            _page.OnReaderManipulationCompleted(sender, e);
        }

        public void ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            _page.OnReaderManipulationStarted(sender, e);
        }

        public void ManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            _page.OnReaderManipulationUpdated(sender, e);
        }

        public void Tapped(object sender, TappedEventArgs e)
        {
            _page.OnReaderTapped(sender, e);
        }
    }
}
