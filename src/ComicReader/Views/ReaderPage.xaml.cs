#if DEBUG
//#define DEBUG_LOG_POINTER
#endif

using muxc = Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Common;
using ComicReader.Common.Router;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils.KVDatabase;
using ComicReader.Common.Constants;
using ComicReader.Utils;

namespace ComicReader.Views
{
    using TaskResult = Utils.TaskResult;

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }

    public class ReaderPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private NavigationPageShared m_NavigationPageShared;
        public NavigationPageShared NavigationPageShared
        {
            get => m_NavigationPageShared;
            set
            {
                m_NavigationPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NavigationPageShared"));
            }
        }

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

        private string m_Progress;
        public string Progress
        {
            get => m_Progress;
            set
            {
                m_Progress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Progress"));
            }
        }

        // Reader
        private ReaderStatusEnum m_ReaderStatus = ReaderStatusEnum.Loading;
        public ReaderStatusEnum ReaderStatus
        {
            get => m_ReaderStatus;
            set
            {
                m_ReaderStatus = value;
                UpdateReaderUI();
            }
        }

        private string m_ReaderStatusText = "";
        public string ReaderStatusText
        {
            get => m_ReaderStatusText;
            set
            {
                m_ReaderStatusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReaderStatusText"));
            }
        }

        private bool m_IsReaderStatusTextBlockVisible = false;
        public bool IsReaderStatusTextBlockVisible
        {
            get => m_IsReaderStatusTextBlockVisible;
            set
            {
                m_IsReaderStatusTextBlockVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsReaderStatusTextBlockVisible"));
            }
        }

        private bool m_IsGridViewVisible = false;
        public bool IsGridViewVisible
        {
            get => m_IsGridViewVisible;
            set
            {
                m_IsGridViewVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGridViewVisible"));
            }
        }

        private bool m_IsTitleBarPlaceHolderVisible = false;
        public bool IsTitleBarPlaceHolderVisible
        {
            get => m_IsTitleBarPlaceHolderVisible;
            set
            {
                m_IsTitleBarPlaceHolderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTitleBarPlaceHolderVisible"));
            }
        }

        public ReaderSettingViewModel ReaderSettings => NavigationPageShared.ReaderSettings;

        public void UpdateReaderUI()
        {
            bool is_working = ReaderStatus == ReaderStatusEnum.Working;
            bool grid_view_visible = is_working && NavigationPageShared?.IsPreviewButtonToggled == true;
            bool reader_visible = is_working && !grid_view_visible;
            bool vertical_reader_visible = reader_visible && ReaderSettings.IsVertical;
            bool horizontal_reader_visible = reader_visible && !vertical_reader_visible;

            IsReaderStatusTextBlockVisible = !is_working;
            if (IsReaderStatusTextBlockVisible)
            {
                switch (ReaderStatus)
                {
                    case ReaderStatusEnum.Loading:
                        ReaderStatusText = Utils.StringResourceProvider.GetResourceString("ReaderStatusLoading");
                        break;
                    case ReaderStatusEnum.Error:
                        ReaderStatusText = Utils.StringResourceProvider.GetResourceString("ReaderStatusError");
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                }
            }

            IsTitleBarPlaceHolderVisible = !(reader_visible && !BottomTilePinned);
            IsGridViewVisible = grid_view_visible;

            if (NavigationPageShared != null)
            {
                NavigationPageShared.IsVerticalReaderVisible = vertical_reader_visible;
                NavigationPageShared.IsHorizontalReaderVisible = horizontal_reader_visible;
            }
        }

        // Bottom tile
        private bool m_BottomTilePinned = false;
        public bool BottomTilePinned
        {
            get => m_BottomTilePinned;
            set
            {
                m_BottomTilePinned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BottomTilePinned"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PinButtonToolTip"));
                UpdateReaderUI();
                BottomTilePinnedChanged?.Invoke();
            }
        }

        public string PinButtonToolTip
        {
            get
            {
                if (BottomTilePinned)
                {
                    return Utils.StringResourceProvider.GetResourceString("Unpin");
                }
                else
                {
                    return Utils.StringResourceProvider.GetResourceString("Pin");
                }
            }
        }

        public Action BottomTilePinnedChanged;
    }

    sealed internal partial class ReaderPage : NavigatablePage
    {
        private const string KEY_TIP_SHOWN = "ReaderTipShown";

        public ReaderPageShared Shared { get; set; } = new ReaderPageShared();
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

        private ComicData _comic;

        // Pointer events
        private readonly GestureRecognizer mGestureRecognizer = new GestureRecognizer();
        private bool mPendingTap = false;
        private bool mTapCancelled = false;

        // Bottom Tile
        private bool mBottomTileShowed = false;
        private bool mBottomTileHold = false;
        private bool mBottomTilePointerIn = false;
        private DateTimeOffset mBottomTileHideRequestTime = DateTimeOffset.Now;

        // Locks
        private readonly Utils.CancellationLock mLoadComicLock = new Utils.CancellationLock();

        public ReaderPage()
        {
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicDir = "";
            Shared.ComicTags = new ObservableCollection<TagCollectionViewModel>();
            Shared.IsEditable = false;
            Shared.BottomTilePinned = false;
            Shared.BottomTilePinnedChanged = OnBottomTilePinnedChanged;

            VerticalReader = new ReaderModel(Shared, true);
            HorizontalReader = new ReaderModel(Shared, false);
            PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

            mGestureRecognizer.GestureSettings =
                GestureSettings.Tap |
                GestureSettings.DoubleTap |
                GestureSettings.ManipulationTranslateX |
                GestureSettings.ManipulationTranslateY |
                GestureSettings.ManipulationTranslateInertia |
                GestureSettings.ManipulationScale;
            mGestureRecognizer.InertiaTranslationDeceleration = 0.002F;
            mGestureRecognizer.Tapped += OnReaderTapped;
            mGestureRecognizer.ManipulationStarted += OnReaderManipulationStarted;
            mGestureRecognizer.ManipulationUpdated += OnReaderManipulationUpdated;
            mGestureRecognizer.ManipulationCompleted += OnReaderManipulationCompleted;

            InitializeComponent();
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            Shared.NavigationPageShared = (NavigationPageShared)p.Params;

            bool tipShown = KVDatabase.getInstance().getDefaultMethod().GetBoolean(KVLib.TIPS, KEY_TIP_SHOWN, false);
            if (!tipShown)
            {
                ReaderTip.IsOpen = !tipShown;
            }
        }

        public override void OnResume()
        {
            base.OnResume();
            Shared.NavigationPageShared.OnKeyDown += OnKeyDown;
            Shared.NavigationPageShared.OnSwitchFavorites += OnSwitchFavorites;
            Shared.NavigationPageShared.OnPreviewModeChanged += Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane += ExpandInfoPane;
            Shared.ReaderSettings.OnVerticalChanged += OnReaderSwitched;
            Shared.ReaderSettings.OnContinuousChanged += OnReaderContinuousChanged;
            Shared.ReaderSettings.OnHorizontalContinuousChanged += HorizontalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged += VerticalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged += HorizontalReader.OnPageRearrangeEventSealed;

            Shared.NavigationPageShared.IsPreviewButtonToggled = false;
            OnReaderContinuousChanged();
            Shared.UpdateReaderUI();

            ComicData comic = (ComicData)GetTabId().RequestArgs;
            GetTabId().Tab.Header = comic.Title;
            GetTabId().Tab.IconSource = new muxc.SymbolIconSource { Symbol = Symbol.Pictures };

            Utils.C0.Run(async delegate
            {
                await LoadComic(comic);
            });
        }

        public override void OnPause()
        {
            base.OnPause();
            Shared.NavigationPageShared.OnKeyDown -= OnKeyDown;
            Shared.NavigationPageShared.OnSwitchFavorites -= OnSwitchFavorites;
            Shared.NavigationPageShared.OnPreviewModeChanged -= Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane -= ExpandInfoPane;
            Shared.ReaderSettings.OnVerticalChanged -= OnReaderSwitched;
            Shared.ReaderSettings.OnContinuousChanged -= OnReaderContinuousChanged;
            Shared.ReaderSettings.OnHorizontalContinuousChanged -= HorizontalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged -= VerticalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged -= HorizontalReader.OnPageRearrangeEventSealed;
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            base.OnLoaded(sender, e);

            EventBus.Instance.With<double>(EventId.TitleBarHeightChange).Observe(this, delegate (double h)
            {
                TitleBarArea.Height = h;
                TitleBarPlaceHolder.Height = h;
            }, true);

            EventBus.Instance.With<double>(EventId.TitleBarOpacity).Observe(this, delegate (double opacity)
            {
                BottomGrid.Opacity = opacity;
            }, true);
        }

        public override void OnSelected()
        {
            Utils.C0.Run(async delegate
            {
                Shared.BottomTilePinned = false;
                await LoadComicInfo();
            });
        }

        public override string GetUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Location;
        }

        public override bool AllowJump()
        {
            return true;
        }

        // Load
        private async Task LoadComic(ComicData comic)
        {
            if (comic == null || comic == _comic)
            {
                return;
            }

            await mLoadComicLock.WaitAsync();
            try
            {
                Shared.ReaderStatus = ReaderStatusEnum.Loading;

                VerticalReader.Reset();
                HorizontalReader.Reset();

                ReaderModel reader = GetCurrentReader();
                System.Diagnostics.Debug.Assert(reader != null);

                reader.OnLoaded = () =>
                {
                    Shared.ReaderStatus = ReaderStatusEnum.Working;
                    UpdatePage(reader);
                    BottomTileShow();
                    BottomTileHide(5000);
                };

                _comic = comic;
                VerticalReader.Comic = _comic;
                HorizontalReader.Comic = _comic;

                if (!_comic.IsExternal)
                {
                    // Mark as read.
                    _comic.SetAsRead();

                    // Add to history
                    await HistoryDataManager.Add(_comic.Id, _comic.Title1, true);

                    // Update image files.
                    TaskResult result = await _comic.UpdateImages(cover_only: false, reload: true);

                    if (!result.Successful)
                    {
                        Log("Failed to load images of '" + _comic.Location + "'. " + result.ExceptionType.ToString());
                        Shared.ReaderStatus = ReaderStatusEnum.Error;
                        return;
                    }
                }

                // Load info.
                await LoadComicInfo();

                // Load image frames.
                if (!_comic.IsExternal)
                {
                    // Set initial page.
                    reader.InitialPage = _comic.LastPosition;

                    // Load frames.
                    for (int i = 0; i < _comic.ImageAspectRatios.Count; ++i)
                    {
                        await VerticalReader.LoadFrame(i);
                        await HorizontalReader.LoadFrame(i);
                    }

                    // Refresh reader.
                    await reader.UpdateImages(true);
                }

                await LoadImages();
                await reader.Finalize();

                // Refresh reader.
                await reader.UpdateImages(true);
                UpdatePage(reader);
            }
            finally
            {
                mLoadComicLock.Release();
            }
        }

        private async Task LoadImages()
        {
            double preview_width = 0.0;
            double preview_height = 0.0;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
                preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
                PreviewDataSource.Clear();
            });

            List<Utils.ImageLoader.Token> preview_img_loader_tokens = new List<Utils.ImageLoader.Token>();
            ComicData comic = _comic; // Stores locally.
            Utils.Stopwatch save_timer = new Utils.Stopwatch();

            for (int i = 0; i < comic.ImageCount; ++i)
            {
                int index = i; // Stores locally.
                int page = i + 1; // Stores locally.

                preview_img_loader_tokens.Add(new Utils.ImageLoader.Token
                {
                    Comic = comic,
                    Index = index,
                    CallbackAsync = async (BitmapImage img) => 
                    {
                        // Save image aspect ratio info.
                        double image_aspect_ratio;
                        if (img.PixelWidth <= 0 || img.PixelHeight <= 0)
                        {
                            image_aspect_ratio = -1;
                        }
                        else
                        {
                            image_aspect_ratio = (double)img.PixelWidth / img.PixelHeight;
                        }

                        if (index < comic.ImageAspectRatios.Count)
                        {
                            comic.ImageAspectRatios[index] = image_aspect_ratio;
                        }
                        else
                        {
                            // Normally image aspect ratio items will be added one by one.
                            // In some cases (like corrupted images), few indices will be skipped.
                            while (index > comic.ImageAspectRatios.Count)
                            {
                                comic.ImageAspectRatios.Add(-1);
                                await VerticalReader.LoadFrame(comic.ImageAspectRatios.Count - 1);
                                await HorizontalReader.LoadFrame(comic.ImageAspectRatios.Count - 1);
                            }
                            comic.ImageAspectRatios.Add(image_aspect_ratio);
                        }
                        await VerticalReader.LoadFrame(index);
                        await HorizontalReader.LoadFrame(index);

                        // Save for each 5 sec.
                        if (save_timer.LapSpan().TotalSeconds > 5.0 || index == comic.ImageCount - 1)
                        {
                            comic.SaveImageAspectRatios();
                            save_timer.Lap();
                        }

                        // Update previews.
                        PreviewDataSource.Add(new ReaderImagePreviewViewModel
                        {
                            ImageSource = img,
                            Page = page,
                        });
                    }
                });
            }

            save_timer.Start();
            await new Utils.ImageLoader.Builder(preview_img_loader_tokens, mLoadComicLock)
                .WidthConstrain(preview_width).HeightConstrain(preview_height).Commit();
        }

        private async Task LoadComicInfo()
        {
            if (_comic == null)
            {
                return;
            }

            Shared.NavigationPageShared.IsExternal = _comic.IsExternal;

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
            Shared.Progress = "";

            LoadComicTag();

            if (!_comic.IsExternal)
            {
                Shared.NavigationPageShared.IsFavorite = await FavoriteDataManager.FromId(_comic.Id) != null;
                Shared.Rating = _comic.Rating;
            }
        }

        private void LoadComicTag()
        {
            if (_comic == null)
            {
                return;
            }

            ObservableCollection<TagCollectionViewModel> new_collection = new ObservableCollection<TagCollectionViewModel>();

            for (int i = 0; i < _comic.Tags.Count; ++i)
            {
                TagData tags = _comic.Tags[i];
                TagCollectionViewModel tags_model = new TagCollectionViewModel(tags.Name);

                foreach (string tag in tags.Tags)
                {
                    TagViewModel tag_model = new TagViewModel
                    {
                        Tag = tag,
                        OnClicked = OnInfoPaneTagClicked
                    };

                    tags_model.Tags.Add(tag_model);
                }

                new_collection.Add(tags_model);
            }

            Shared.ComicTags = new_collection;
        }

        // Utilities
        private ReaderModel GetCurrentReader()
        {
            if (Shared.ReaderSettings.IsVertical)
            {
                return VerticalReader;
            }
            else
            {
                return HorizontalReader;
            }
        }

        private void UpdatePage(ReaderModel reader)
        {
            if (PageIndicator == null)
            {
                return;
            }

            int page = reader.Page;

            if (page <= 0)
            {
                page = (int)Math.Round(reader.InitialPage);
            }

            PageIndicator.Text = page.ToString() + " / " + reader.PageCount.ToString();
        }

        private void UpdateProgress(ReaderModel reader, bool save)
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
            Shared.Progress = progress.ToString() + "%";

            if (save)
            {
                _comic.SaveProgress(progress, reader.PageSource);
            }
        }

        // Reader
        public void OnReaderSwitched()
        {
            Utils.C0.Run(async delegate
            {
                ReaderModel reader = GetCurrentReader();
                System.Diagnostics.Debug.Assert(reader != null);

                if (reader == null)
                {
                    return;
                }

                ReaderModel last_reader = reader.IsVertical ? HorizontalReader : VerticalReader;
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
                ReaderModel.ScrollManager.BeginTransaction(reader).Zoom(zoom).Page(page).Commit();
                await reader.UpdateImages(true);
                Shared.UpdateReaderUI();
                await last_reader.UpdateImages(false);
            });
        }

        private void OnReaderContinuousChanged()
        {
            mGestureRecognizer.AutoProcessInertia = Shared.ReaderSettings.IsContinuous;
        }

        private void OnReaderScrollViewerViewChanged(ReaderModel control, ScrollViewerViewChangedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                if (await control.OnViewChanged(!e.IsIntermediate))
                {
                    UpdatePage(control);
                    UpdateProgress(control, save: !e.IsIntermediate);
                    BottomTileSetHold(false);
                }
            });
        }

        private void OnReaderScrollViewerSizeChanged(ReaderModel control)
        {
            control.OnSizeChanged();
        }

        private void OnReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                // Ctrl key down indicates the user is zooming the page. In that case we shouldn't handle the event.
                CoreVirtualKeyStates ctrl_state = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                
                if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
                {
                    return;
                }

                ReaderModel reader = GetCurrentReader();

                if (reader == null)
                {
                    return;
                }

                await reader.OnReaderScrollViewerPointerWheelChanged(e);
            });
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
            Utils.C0.Run(async delegate
            {
                ReaderImagePreviewViewModel ctx = (ReaderImagePreviewViewModel)e.ClickedItem;

                Shared.NavigationPageShared.IsPreviewButtonToggled = false;

                ReaderModel reader = GetCurrentReader();

                if (reader == null)
                {
                    return;
                }

                await Utils.C0.WaitFor(() => reader.Loaded);
                ReaderModel.ScrollManager.BeginTransaction(reader).Page(ctx.Page).Commit();
            });
        }

        // Pointer events
        private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            (sender as UIElement).CapturePointer(e.Pointer);
            PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
            mGestureRecognizer.ProcessDownEvent(pointer_point);

#if DEBUG_LOG_POINTER
            Log("Pointer pressed");
#endif
        }

        private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            mGestureRecognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));

#if DEBUG_LOG_POINTER
            //Log("Pointer moved");
#endif
        }

        private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
            mGestureRecognizer.ProcessUpEvent(pointer_point);
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!mGestureRecognizer.AutoProcessInertia)
            {
                mGestureRecognizer.CompleteGesture();
            }

#if DEBUG_LOG_POINTER
            Log("Pointer released");
#endif
        }

        private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
            mGestureRecognizer.ProcessUpEvent(pointer_point);
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!mGestureRecognizer.AutoProcessInertia)
            {
                mGestureRecognizer.CompleteGesture();
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
                ReaderModel reader = GetCurrentReader();
                if (reader == null)
                {
                    return;
                }
                if (Math.Abs(reader.Zoom - 100) <= 1)
                {
                    ReaderModel.ScrollManager.BeginTransaction(reader)
                        .Zoom(100, Common.Structs.ZoomType.CenterCrop)
                        .EnableAnimation()
                        .Commit();
                }
                else
                {
                    ReaderModel.ScrollManager.BeginTransaction(reader)
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
            ReaderModel reader = GetCurrentReader();

            if (reader  == null)
            {
                return;
            }

            Utils.C0.Run(async delegate
            {
                await reader.OnReaderManipulationCompleted(e);
            });
        }

        // Favorites
        public async Task SetIsFavorite(bool is_favorite)
        {
            if (Shared.NavigationPageShared.IsFavorite == is_favorite)
            {
                return;
            }

            Shared.NavigationPageShared.IsFavorite = is_favorite;

            if (is_favorite)
            {
                await FavoriteDataManager.Add(_comic.Id, _comic.Title1, final: true);
            }
            else
            {
                await FavoriteDataManager.RemoveWithId(_comic.Id, final: true);
            }
        }

        private void OnSwitchFavorites()
        {
            Utils.C0.Run(async delegate
            {
                await SetIsFavorite(!Shared.NavigationPageShared.IsFavorite);
            });
        }

        private void OnFavoritesChecked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await SetIsFavorite(true);
            });
        }

        private void OnFavoritesUnchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await SetIsFavorite(false);
            });
        }

        // Info Pane
        public void ExpandInfoPane()
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        }

        private void OnRatingControlValueChanged(muxc.RatingControl sender, object args)
        {
            _comic.SaveRating((int)sender.Value);
        }

        private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                StorageFolder folder = await Utils.Storage.TryGetFolder(_comic.Location);

                if (folder != null)
                {
                    _ = await Launcher.LaunchFolderAsync(folder);
                }
            });
        }

        private void OnInfoPaneTagClicked(object sender, RoutedEventArgs e)
        {
            TagViewModel ctx = (TagViewModel)((Button)sender).DataContext;
            MainPage.Current.LoadTab(null, SearchPageTrait.Instance, "<tag: " + ctx.Tag + ">");
        }

        private void OnEditInfoClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                if (_comic == null)
                {
                    return;
                }

                EditComicInfoDialog dialog = new EditComicInfoDialog(_comic);
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await LoadComicInfo();
                }
            });
        }

        // Bottom Tile
        private void BottomTileShow()
        {
            if (mBottomTileShowed)
            {
                return;
            }
            MainPage.Current.ShowOrHideTitleBar(true);
            mBottomTileShowed = true;
        }

        private void BottomTileHide(int timeout)
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

                    _ = Utils.C0.Sync(delegate
                    {
                        BottomTileHide(0);
                    });
                });
                return;
            }

            if (!mBottomTileShowed || Shared.BottomTilePinned
                || mBottomTileHold || mBottomTilePointerIn)
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

        private void OnBottomTilePinnedChanged()
        {
            if (Shared.BottomTilePinned)
            {
                BottomTileShow();
            }
        }

        private void OnPinClick(object sender, RoutedEventArgs e)
        {
            Shared.BottomTilePinned = !Shared.BottomTilePinned;
        }

        private void OnReaderPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                return;
            }
            if (!ScreenUtils.IsPointerInApp())
            {
                return;
            }

            mBottomTilePointerIn = true;
            BottomTileShow();
        }

        private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            mBottomTilePointerIn = false;

            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
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
        private void OnReaderTipCloseButtonClick(muxc.InfoBar sender, object args)
        {
            KVDatabase.getInstance().getDefaultMethod().SetBoolean(KVLib.TIPS, KEY_TIP_SHOWN, true);
        }

        // Keys
        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            ReaderModel reader = GetCurrentReader();

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
                        if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
                        {
                            await reader.MoveFrame(-1);
                        }
                        else
                        {
                            await reader.MoveFrame(1);
                        }
                        break;

                    case VirtualKey.Left:
                        if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
                        {
                            await reader.MoveFrame(1);
                        }
                        else
                        {
                            await reader.MoveFrame(-1);
                        }
                        break;

                    case VirtualKey.Up:
                        await reader.MoveFrame(-1);
                        break;

                    case VirtualKey.Down:
                        await reader.MoveFrame(1);
                        break;

                    case VirtualKey.PageUp:
                        await reader.MoveFrame(-1);
                        break;

                    case VirtualKey.PageDown:
                        await reader.MoveFrame(1);
                        break;

                    case VirtualKey.Home:
                        ReaderModel.ScrollManager.BeginTransaction(reader).Page(1).Commit();
                        break;

                    case VirtualKey.End:
                        ReaderModel.ScrollManager.BeginTransaction(reader).Page(reader.PageCount).Commit();
                        break;

                    case VirtualKey.Space:
                        await reader.MoveFrame(1);
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
        private void Log(string text)
        {
            Utils.Debug.Log("ReaderPage: " + text + ".");
        }
    }
}
