#if DEBUG
//#define DEBUG_LOG_POINTER
#endif

using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Common.Router;
using ComicReader.Controls;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;
using ComicReader.Utils.Image;
using ComicReader.Utils.KVDatabase;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace ComicReader.Views.Reader
{
    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }

    internal class ReaderPageShared : INotifyPropertyChanged
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

        public ReaderSettingViewModel ReaderSettings => NavigationPageShared.ReaderSettings;

        public void UpdateReaderUI()
        {
            bool isWorking = ReaderStatus == ReaderStatusEnum.Working;
            bool previewVisible = isWorking && NavigationPageShared?.IsPreviewButtonToggled == true;
            bool readerVisible = isWorking && !previewVisible;
            bool verticalReaderVisible = readerVisible && ReaderSettings.IsVertical;
            bool horizontalReaderVisible = readerVisible && !verticalReaderVisible;

            IsReaderStatusTextBlockVisible = !isWorking;
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

            IsGridViewVisible = previewVisible;

            if (NavigationPageShared != null)
            {
                NavigationPageShared.IsVerticalReaderVisible = verticalReaderVisible;
                NavigationPageShared.IsHorizontalReaderVisible = horizontalReaderVisible;
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

    sealed internal partial class ReaderPage : NavigatablePage
    {
        private const string KEY_TIP_SHOWN = "ReaderTipShown";

        public ReaderPageShared Shared { get; set; } = new ReaderPageShared();
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

        private readonly TagItemHandler _tagItemHandler;
        private readonly GestureHandler _gestureHandler;

        private ComicData _comic;

        // Pointer events
        private ReaderGestureRecognizer _gestureRecognizer = new ReaderGestureRecognizer();
        private bool mPendingTap = false;
        private bool mTapCancelled = false;

        // Bottom Tile
        private bool mBottomTileShowed = false;
        private bool mBottomTileHold = false;
        private bool mBottomTilePointerIn = false;
        private DateTimeOffset mBottomTileHideRequestTime = DateTimeOffset.Now;

        // Locks
        private readonly Utils.CancellationLock _loadComicLock = new Utils.CancellationLock();
        private readonly Utils.CancellationSession _loadImageSession = new Utils.CancellationSession();
        private volatile bool _updatingProgress = false;

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

            VerticalReader = new ReaderModel(Shared, true);
            HorizontalReader = new ReaderModel(Shared, false);
            PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

            InitializeComponent();
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            Shared.NavigationPageShared = (NavigationPageShared)p.Params;

            bool tipShown = KVDatabase.GetInstance().GetDefaultMethod().GetBoolean(KVLib.TIPS, KEY_TIP_SHOWN, false);
            if (!tipShown)
            {
                ReaderTip.IsOpen = !tipShown;
            }
        }

        public override void OnResume()
        {
            base.OnResume();
            ObserveData();

            Shared.NavigationPageShared.IsPreviewButtonToggled = false;
            OnReaderContinuousChanged();
            Shared.UpdateReaderUI();

            var comic = (ComicData)GetTabId().RequestArgs;
            GetTabId().Tab.Header = comic.Title;
            GetTabId().Tab.IconSource = new SymbolIconSource { Symbol = Symbol.Pictures };

            _ = LoadComic(comic);
            if (!comic.IsExternal)
            {
                AppStatusPreserver.Instance.SetReadingComic(comic.Id);
            }
        }

        public override void OnPause()
        {
            base.OnPause();
            _loadImageSession.Next();
            HorizontalReader.StopLoadingImage();
            VerticalReader.StopLoadingImage();

            if (_comic != null && !_comic.IsExternal)
            {
                AppStatusPreserver.Instance.UnsetReadingComic(_comic.Id);
            }
        }

        public override void OnSelected()
        {
            Utils.C0.Run(async delegate
            {
                await LoadComicInfo();
            });
        }

        private void ObserveData()
        {
            GetTabId().TabEventBus.With<bool>(EventId.PreviewModeChanged).Observe(this, delegate
            {
                Shared.UpdateReaderUI();
            });

            GetTabId().TabEventBus.With(EventId.SwitchFavorites).Observe(this, delegate
            {
                OnSwitchFavorites();
            });

            GetTabId().TabEventBus.With(EventId.ExpandInfoPane).Observe(this, delegate
            {
                ExpandInfoPane();
            });

            EventBus.Default.With(EventId.ReaderVerticalChanged).Observe(this, delegate
            {
                OnReaderSwitched();
            });

            EventBus.Default.With(EventId.ReaderContinuousChanged).Observe(this, delegate
            {
                OnReaderContinuousChanged();
                GetCurrentReader()?.OnPageRearrangeEventSealed();
            });

            EventBus.Default.With(EventId.ReaderPageArrangementChanged).Observe(this, delegate
            {
                GetCurrentReader()?.OnPageRearrangeEventSealed();
            });

            EventBus.Default.With<bool>(EventId.FullscreenChanged).ObserveSticky(this, delegate (bool fullscreen)
            {
                Shared.IsFullscreen = fullscreen;
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
        }

        // Load
        private async Task LoadComic(ComicData comic)
        {
            if (comic == null || comic == _comic)
            {
                return;
            }

            await _loadComicLock.LockAsync(async delegate (CancellationLock.Token token)
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
                    TaskException result = await _comic.UpdateImages(cover_only: false, reload: true);
                    if (!result.Successful())
                    {
                        Log("Failed to load images of '" + _comic.Location + "'. " + result.ToString());
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

                LoadImages();
                await reader.Finalize();

                // Refresh reader.
                await reader.UpdateImages(true);
                UpdatePage(reader);
            });
        }

        private void LoadImages()
        {
            CancellationSession.Token token = _loadImageSession.Next();

            double preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
            double preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
            PreviewDataSource.Clear();

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
                    Callback = new LoadPreviewImageCallback(this, index, save_timer)
                });
            }

            save_timer.Start();
            new ImageLoader.Transaction(preview_img_loader_tokens)
                .SetWidthConstraint(preview_width).SetHeightConstraint(preview_height).Commit();
        }

        private class LoadPreviewImageCallback : ImageLoader.ICallback
        {
            private readonly ReaderPage _page;
            private readonly int _index;
            private readonly ComicData _comic;
            private readonly Stopwatch _saveTimer;

            public LoadPreviewImageCallback(ReaderPage page, int index, Stopwatch saveTimer)
            {
                _page = page;
                _index = index;
                _comic = page._comic;
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

        // Reader
        public void OnReaderSwitched()
        {
            Utils.C0.Run(async delegate
            {
                ReaderModel reader = GetCurrentReader();
                if (reader == null)
                {
                    System.Diagnostics.Debug.Assert(false);
                    return;
                }

                OnReaderContinuousChanged();
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
            _gestureRecognizer.AutoProcessInertia = Shared.ReaderSettings.IsContinuous;
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
                CoreVirtualKeyStates ctrl_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

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
            Shared.NavigationPageShared.IsPreviewButtonToggled = false;

            ReaderModel reader = GetCurrentReader();
            if (reader == null)
            {
                return;
            }

            ReaderModel.ScrollManager.BeginTransaction(reader).Page(ctx.Page).Commit();
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

            if (reader == null)
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

        private void OnFavoritesUnchecked(object sender, RoutedEventArgs e)
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

        private void OnRatingControlValueChanged(RatingControl sender, object args)
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
            var ctx = (TagViewModel)((Button)sender).DataContext;
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

                var dialog = new EditComicInfoDialog(_comic);
                ContentDialogResult result = await C0.ShowDialogAsync(dialog, XamlRoot);
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

            if (!mBottomTileShowed || Shared.IsGridViewVisible || mBottomTileHold || mBottomTilePointerIn)
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

        private class TagItemHandler : TagViewModel.IItemHandler
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
}
