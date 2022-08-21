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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Views
{
    using TaskResult = Utils.TaskResult;
    using ReaderModel = Common.ReaderModel;

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
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Progress"));
                }
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

        public ReaderSettingViewModel ReaderSettings => NavigationPageShared.ReaderSettings;

        public void UpdateReaderUI()
        {
            bool is_working = ReaderStatus == ReaderStatusEnum.Working;
            bool grid_view_visible = is_working && NavigationPageShared.IsPreviewButtonToggled;
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

            IsGridViewVisible = grid_view_visible;
            NavigationPageShared.IsVerticalReaderVisible = vertical_reader_visible;
            NavigationPageShared.IsHorizontalReaderVisible = horizontal_reader_visible;
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

    public sealed partial class ReaderPage : Page
    {
        public ReaderPageShared Shared { get; set; } = new ReaderPageShared();
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

        private readonly Common.Tab.TabManager m_tab_manager;
        private ComicData m_comic = null;

        // Pointer events
        private readonly GestureRecognizer m_gesture_recognizer = new GestureRecognizer();

        // Bottom Tile
        private bool m_bottom_tile_showed = false;
        private bool m_bottom_tile_hold = false;
        private bool m_bottom_tile_pointer_in = false;
        private DateTimeOffset m_bottom_tile_hide_request_time = DateTimeOffset.Now;

        // Locks
        private readonly Utils.CancellationLock m_lock_load_comic = new Utils.CancellationLock();

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

            m_tab_manager = new Common.Tab.TabManager(this)
            {
                OnTabUpdate = OnTabUpdate,
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabStart = OnTabStart
            };

            m_gesture_recognizer.GestureSettings =
                GestureSettings.Tap |
                GestureSettings.ManipulationTranslateX |
                GestureSettings.ManipulationTranslateY |
                GestureSettings.ManipulationTranslateInertia |
                GestureSettings.ManipulationScale;
            m_gesture_recognizer.Tapped += OnReaderTapped;
            m_gesture_recognizer.ManipulationStarted += OnReaderManipulationStarted;
            m_gesture_recognizer.ManipulationUpdated += OnReaderManipulationUpdated;
            m_gesture_recognizer.ManipulationCompleted += OnReaderManipulationCompleted;

            InitializeComponent();
        }

        // Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_manager.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            m_tab_manager.OnNavigatedFrom(e);
        }

        private void OnTabRegister(object shared)
        {
            Shared.NavigationPageShared = (NavigationPageShared)shared;

            Shared.NavigationPageShared.OnKeyDown += OnKeyDown;
            Shared.NavigationPageShared.OnSwitchFavorites += OnSwitchFavorites;
            Shared.NavigationPageShared.OnZoomIn += ZoomIn;
            Shared.NavigationPageShared.OnZoomOut += ZoomOut;
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
        }

        private void OnTabUnregister()
        {
            Shared.NavigationPageShared.OnKeyDown -= OnKeyDown;
            Shared.NavigationPageShared.OnSwitchFavorites -= OnSwitchFavorites;
            Shared.NavigationPageShared.OnZoomIn -= ZoomIn;
            Shared.NavigationPageShared.OnZoomOut -= ZoomOut;
            Shared.NavigationPageShared.OnPreviewModeChanged -= Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane -= ExpandInfoPane;
            Shared.ReaderSettings.OnVerticalChanged -= OnReaderSwitched;
            Shared.ReaderSettings.OnContinuousChanged -= OnReaderContinuousChanged;
            Shared.ReaderSettings.OnHorizontalContinuousChanged -= HorizontalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged -= VerticalReader.OnPageRearrangeEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged -= HorizontalReader.OnPageRearrangeEventSealed;
        }

        private void OnTabUpdate()
        {
            Utils.C0.Run(async delegate
            {
                Shared.BottomTilePinned = false;
                await LoadComicInfo();
            });
        }

        private void OnTabStart(Common.Tab.TabIdentifier tab_id)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                Shared.NavigationPageShared.CurrentPageType = Common.Tab.PageType.Reader;
                tab_id.Type = Common.Tab.PageType.Reader;

                ComicData comic = (ComicData)tab_id.RequestArgs;
                tab_id.Tab.Header = comic.Title;
                tab_id.Tab.IconSource = new muxc.SymbolIconSource { Symbol = Symbol.Pictures };
                await LoadComic(db, comic);
            });
        }

        public static string PageUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Location;
        }

        // Load
        private async Task LoadComic(LockContext db, ComicData comic)
        {
            if (comic == null)
            {
                return;
            }

            await m_lock_load_comic.WaitAsync();
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

                m_comic = comic;
                VerticalReader.Comic = m_comic;
                HorizontalReader.Comic = m_comic;

                if (!m_comic.IsExternal)
                {
                    // Mark as read.
                    m_comic.SetAsRead();

                    // Add to history
                    await HistoryDataManager.Add(m_comic.Id, m_comic.Title1, true);

                    // Update image files.
                    TaskResult result = await m_comic.UpdateImages(cover_only: false, reload: true);

                    if (!result.Successful)
                    {
                        Log("Failed to load images of '" + m_comic.Location + "'. " + result.ExceptionType.ToString());
                        Shared.ReaderStatus = ReaderStatusEnum.Error;
                        return;
                    }
                }

                // Load info.
                await LoadComicInfo();

                // Load image frames.
                if (!m_comic.IsExternal)
                {
                    // Set initial page.
                    reader.InitialPage = m_comic.LastPosition;

                    // Load frames.
                    for (int i = 0; i < m_comic.ImageAspectRatios.Count; ++i)
                    {
                        await VerticalReader.LoadFrame(i);
                        await HorizontalReader.LoadFrame(i);
                    }

                    // Refresh reader.
                    await reader.UpdateImages(db, true);
                }

                await LoadImages(db);
                await reader.Finalize();

                // Refresh reader.
                await reader.UpdateImages(db, true);
                UpdatePage(reader);
            }
            finally
            {
                m_lock_load_comic.Release();
            }
        }

        private async Task LoadImages(LockContext db)
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

            var preview_img_loader_tokens = new List<Utils.ImageLoader.Token>();
            ComicData comic = m_comic; // Stores locally.
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
                        double image_aspect_ratio = (double)img.PixelWidth / img.PixelHeight;

                        // Normally image aspect ratio items will be added one by one.
                        if (index > comic.ImageAspectRatios.Count)
                        {
                            // Unexpected item.
                            System.Diagnostics.Debug.Assert(false);
                            return;
                        }

                        if (index < comic.ImageAspectRatios.Count)
                        {
                            comic.ImageAspectRatios[index] = image_aspect_ratio;
                        }
                        else
                        {
                            comic.ImageAspectRatios.Add(image_aspect_ratio);
                        }

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

                        // Update reader frames.
                        await VerticalReader.LoadFrame(index);
                        await HorizontalReader.LoadFrame(index);
                    }
                });
            }

            save_timer.Start();
            await new Utils.ImageLoader.Builder(preview_img_loader_tokens, m_lock_load_comic)
                .WidthConstrain(preview_width).HeightConstrain(preview_height).Commit();
        }

        private async Task LoadComicInfo()
        {
            if (m_comic == null)
            {
                return;
            }

            Shared.NavigationPageShared.IsExternal = m_comic.IsExternal;

            if (m_comic.Title1.Length == 0)
            {
                Shared.ComicTitle1 = m_comic.Title;
            }
            else
            {
                Shared.ComicTitle1 = m_comic.Title1;
                Shared.ComicTitle2 = m_comic.Title2;
            }

            Shared.ComicDir = m_comic.Location;
            Shared.CanDirOpenInFileExplorer = m_comic is ComicFolderData;
            Shared.IsEditable = m_comic.IsEditable;
            Shared.Progress = "";

            LoadComicTag();

            if (!m_comic.IsExternal)
            {
                Shared.NavigationPageShared.IsFavorite = await FavoriteDataManager.FromId(m_comic.Id) != null;
                Shared.Rating = m_comic.Rating;
            }
        }

        private void LoadComicTag()
        {
            if (m_comic == null)
            {
                return;
            }

            ObservableCollection<TagCollectionViewModel> new_collection = new ObservableCollection<TagCollectionViewModel>();

            for (int i = 0; i < m_comic.Tags.Count; ++i)
            {
                TagData tags = m_comic.Tags[i];
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
                m_comic.SaveProgress(progress, reader.PageSource);
            }
        }

        // Reader
        public void OnReaderSwitched()
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

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
                await reader.UpdateImages(db, true);
                Shared.UpdateReaderUI();
                await last_reader.UpdateImages(db, false);
            });
        }

        private void OnReaderContinuousChanged()
        {
            m_gesture_recognizer.AutoProcessInertia = Shared.ReaderSettings.IsContinuous;
        }

        private void OnReaderScrollViewerViewChanged(ReaderModel control, ScrollViewerViewChangedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                if (await control.OnViewChanged(db, !e.IsIntermediate))
                {
                    UpdatePage(control);
                    UpdateProgress(control, save: !e.IsIntermediate);
                    BottomTileSetHold(false);
                }
            });
        }

        private void OnReaderScrollViewerSizeChanged(ReaderModel control, SizeChangedEventArgs e)
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

                // Check if the reader is in the right state to perform a page turn.
                ReaderModel reader = GetCurrentReader();
                
                if (reader == null || reader.IsContinuous || reader.Zoom > 105)
                {
                    return;
                }

                // Page turning.
                PointerPoint pt = e.GetCurrentPoint(null);
                int delta = -pt.Properties.MouseWheelDelta / 120;
                await reader.MoveFrame(delta);

                // Set Handled flag to suppress the default behavior of scroll viewer (which will override ours).
                e.Handled = true;
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
            OnReaderScrollViewerSizeChanged(VerticalReader, e);
        }

        private void OnHorizontalReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnReaderScrollViewerSizeChanged(HorizontalReader, e);
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
            m_gesture_recognizer.ProcessDownEvent(pointer_point);

#if DEBUG_LOG_POINTER
            Log("Pointer pressed");
#endif
        }

        private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));

#if DEBUG_LOG_POINTER
            //Log("Pointer moved");
#endif
        }

        private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
            m_gesture_recognizer.ProcessUpEvent(pointer_point);
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!m_gesture_recognizer.AutoProcessInertia)
            {
                m_gesture_recognizer.CompleteGesture();
            }

#if DEBUG_LOG_POINTER
            Log("Pointer released");
#endif
        }

        private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pointer_point = e.GetCurrentPoint(ManipulationReference);
            m_gesture_recognizer.ProcessUpEvent(pointer_point);
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!m_gesture_recognizer.AutoProcessInertia)
            {
                m_gesture_recognizer.CompleteGesture();
            }

#if DEBUG_LOG_POINTER
            Log("Pointer canceled");
#endif
        }

        private void OnReaderTapped(object sender, TappedEventArgs e)
        {
            BottomTileSetHold(!m_bottom_tile_showed);
        }

        private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            GetCurrentReader()?.OnReaderManipulationStarted(sender, e);
        }

        private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            GetCurrentReader()?.OnReaderManipulationUpdated(sender, e);
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
                await reader.OnReaderManipulationCompleted(sender, e);
            });
        }

        // Zooming
        public void ReaderSetZoom(int level)
        {
            ReaderModel reader = GetCurrentReader();

            if (reader == null)
            {
                return;
            }

            double zoom = reader.Zoom;
            const double scale = 1.2;

            for (int i = 0; i < level; ++i)
            {
                zoom *= scale;
            }

            for (int i = 0; i > level; --i)
            {
                zoom /= scale;
            }

            ReaderModel.ScrollManager.BeginTransaction(reader)
                .Zoom((float)zoom)
                .EnableAnimation()
                .Commit();
        }

        private void ZoomIn()
        {
            ReaderSetZoom(1);
        }

        private void ZoomOut()
        {
            ReaderSetZoom(-1);
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            ZoomOut();
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
                await FavoriteDataManager.Add(m_comic.Id, m_comic.Title1, final: true);
            }
            else
            {
                await FavoriteDataManager.RemoveWithId(m_comic.Id, final: true);
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

        private void OnFavoritesClick(object sender, RoutedEventArgs e)
        {
            OnSwitchFavorites();
        }

        // Info Pane
        public void ExpandInfoPane()
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        }

        private void OnComicInfoClick(object sender, RoutedEventArgs e)
        {
            ExpandInfoPane();
        }

        private void OnRatingControlValueChanged(muxc.RatingControl sender, object args)
        {
            m_comic.SaveRating((int)sender.Value);
        }

        private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                StorageFolder folder = await Utils.Storage.TryGetFolder(m_comic.Location);

                if (folder != null)
                {
                    _ = await Launcher.LaunchFolderAsync(folder);
                }
            });
        }

        private void OnInfoPaneTagClicked(object sender, RoutedEventArgs e)
        {
            TagViewModel ctx = (TagViewModel)((Button)sender).DataContext;
            MainPage.Current.LoadTab(null, Common.Tab.PageType.Search, "<tag: " + ctx.Tag + ">");
        }

        private void OnEditInfoClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                if (m_comic == null)
                {
                    return;
                }

                EditComicInfoDialog dialog = new EditComicInfoDialog(m_comic);
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await LoadComicInfo();
                }
            });
        }

        // Fullscreen
        private void EnterFullscreen()
        {
            MainPage.Current.EnterFullscreen();
            BottomTileShow();
            BottomTileHide(5000);
        }

        private void ExitFullscreen()
        {
            MainPage.Current.ExitFullscreen();
            BottomTileShow();
            BottomTileHide(5000);
        }

        private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
        {
            EnterFullscreen();
        }

        private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
        {
            ExitFullscreen();
        }

        // Bottom Tile
        private void BottomTileShow()
        {
            if (m_bottom_tile_showed)
            {
                return;
            }

            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 1.0);
            BottomGridStoryboard.Begin();
            m_bottom_tile_showed = true;
        }

        private void BottomTileHide(int timeout)
        {
            m_bottom_tile_hide_request_time = DateTimeOffset.Now;

            if (timeout > 0)
            {
                _ = Task.Run(() =>
                {
                    Task.Delay(timeout + 1).Wait();

                    if ((DateTimeOffset.Now - m_bottom_tile_hide_request_time).TotalMilliseconds < timeout)
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

            if (!m_bottom_tile_showed || Shared.BottomTilePinned
                || m_bottom_tile_hold || m_bottom_tile_pointer_in)
            {
                return;
            }

            BottomGridForceHide();
        }

        private void BottomGridForceHide()
        {
            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 0.0);
            BottomGridStoryboard.Begin();
            m_bottom_tile_showed = false;
            m_bottom_tile_hold = false;
        }

        private void BottomTileSetHold(bool val)
        {
            m_bottom_tile_hold = val;

            if (m_bottom_tile_hold)
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

        private void OnBottomTilePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            m_bottom_tile_pointer_in = true;
            BottomTileShow();
        }

        private void OnBottomTilePointerExited(object sender, PointerRoutedEventArgs e)
        {
            m_bottom_tile_pointer_in = false;

            if (!m_bottom_tile_showed || m_bottom_tile_hold)
            {
                return;
            }

            BottomTileHide(3000);
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

                    case VirtualKey.F:
                        if (Shared.NavigationPageShared.MainPageShared.IsFullscreen)
                        {
                            ExitFullscreen();
                        }
                        else
                        {
                            EnterFullscreen();
                        }
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
