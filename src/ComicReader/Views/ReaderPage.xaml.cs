#define DEBUG_LOG_LOAD
#if DEBUG
//#define DEBUG_LOG_JUMP
//#define DEBUG_LOG_VIEW_CHANGE
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_UPDATE_IMAGE
//#define DEBUG_LOG_MANIPULATION
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Views
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;

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

    public class ReaderModel
    {
        // Constants
        public static readonly float MaxZoom = 250f;
        public static readonly float MinZoom = 90f;
        public static readonly float ForceContinuousZoomThreshold = 105f;

        // Modifier - Configurations
        public ComicData Comic { get; set; } = null;
        public double InitialPage { get; set; } = 0.0;

        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;

        public Action OnLoaded;

        // Modifier - General
        public ReaderModel(ReaderPageShared shared, bool is_vertical)
        {
            m_shared = shared;
            IsVertical = is_vertical;
        }

        public void Reset()
        {
#if DEBUG_LOG_LOAD
            Log("========== Reset ==========");
#endif

            Comic = null;
            InitialPage = 0.0;
            OnLoaded = null;
            ResetFrames();
            SyncFinalVal();

            Loaded = false;
            LoadedFirstPage = false;
            LoadedLastPage = false;
            LoadedInitialPage = false;
            LoadedImages = false;
        }

        private void ResetFrames()
        {
            for (int i = 0; i < Frames.Count; ++i)
            {
                ReaderFrameViewModel item = Frames[i];
                item.Notify(cancel: true);
                item.PageL = -1;
                item.PageR = -1;
                item.ImageL = null;
                item.ImageR = null;
            }
        }

        private Utils.CancellationLock m_lock_update_frame = new Utils.CancellationLock();
        /// <summary>
        /// Add or update Frames with index and image aspect ratio. Index has to be<br/>
        /// continuous. As a new item is added to Frames, the reader will start loading<br/>
        /// it automatically. Make sure to set everything up before the initial call to this<br/>
        /// function.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="image_aspect_ratio"></param>
        public async Task UpdateFrame(int image_index)
        {
            await m_lock_update_frame.WaitAsync();
            try
            {
                if (image_index >= Comic.ImageAspectRatios.Count)
                {
                    return;
                }

                double aspect_ratio = Comic.ImageAspectRatios[image_index];

                int frame_idx = PageToFrame(image_index + 1, out bool left_side, out int neighbor);

                if (frame_idx < 0)
                {
                    return;
                }

                if (neighbor != -1)
                {
                    int neighbor_idx = neighbor - 1;

                    if (neighbor_idx >= 0 && neighbor_idx < Comic.ImageAspectRatios.Count)
                    {
                        aspect_ratio += Comic.ImageAspectRatios[neighbor_idx];
                    }
                }

                while (frame_idx >= Frames.Count)
                {
                    Frames.Add(new ReaderFrameViewModel());
                }

                int page = image_index + 1;
                bool dual = neighbor != -1;

                double default_width = 500.0;
                double default_height = 300.0;
                double default_vertical_padding = 10.0;
                double default_horizontal_padding = 100.0;

                if (dual)
                {
                    default_width *= 2;
                }

                if (IsContinuous)
                {
                    default_horizontal_padding = 10.0;
                }

                double frame_width = IsVertical ? default_width : default_height * aspect_ratio;
                double frame_height = IsVertical ? default_width / aspect_ratio : default_height;
                double vertical_padding = IsVertical ? default_vertical_padding : 0;
                double horizontal_padding = IsHorizontal ? default_horizontal_padding : 0;

                ReaderFrameViewModel item = Frames[frame_idx];
                item.FrameWidth = frame_width;
                item.FrameHeight = frame_height;
                item.FrameMargin = new Thickness(horizontal_padding, vertical_padding, horizontal_padding, vertical_padding);

                if (left_side)
                {
                    if (item.PageL != page)
                    {
                        item.PageL = page;
                        item.ImageL = null;
                    }

                    if (!dual)
                    {
                        item.PageR = -1;
                        item.ImageR = null;
                    }
                }
                else
                {
                    if (item.PageR != page)
                    {
                        item.PageR = page;
                        item.ImageR = null;
                    }

                    if (!dual)
                    {
                        item.PageL = -1;
                        item.ImageL = null;
                    }
                }

                // Wait for the frame to be ready.
                item.Reset();
                item.Notify();

                if (!item.Processed)
                {
                    await Task.Run(delegate
                    {
                        lock (item)
                        {
                            while (!item.Processed)
                            {
                                _ = Monitor.Wait(item);
                            }
                        }
                    });
                }

                if (!item.Ready)
                {
                    return;
                }

                await OnContainerLoaded(item);
            }
            finally
            {
                m_lock_update_frame.Release();
            }
        }

        public async Task CompleteFrameUpdate()
        {
            await m_lock_update_frame.WaitAsync();
            try
            {
                for (int i = Frames.Count - 1; i >= 0; --i)
                {
                    ReaderFrameViewModel frame = Frames[i];

                    if (frame.PageL == -1 && frame.PageR == -1)
                    {
                        Frames.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }

                AdjustPadding();
            }
            finally
            {
                m_lock_update_frame.Release();
            }
        }

        // Modifier - Manipulation
        public async Task<bool> MoveFrame(int increment)
        {
            if (!await UpdatePage(true))
            {
                return false;
            }

            InternalMoveFrame(increment, !XmlDatabase.Settings.TransitionAnimation);
            return true;
        }

        public bool SetScrollViewer1(float? zoom, double? parallel_offset, bool disable_animation)
        {
            if (!Loaded)
            {
                return false;
            }

            double? horizontal_offset = IsHorizontal ? parallel_offset : null;
            double? vertical_offset = IsVertical ? parallel_offset : null;

            return InternalSetScrollViewer(new SetScrollViewerContext
            {
                Zoom = zoom,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        public bool SetScrollViewer2(float? zoom, double? page, bool disable_animation)
        {
            if (!Loaded)
            {
                return false;
            }

            return InternalSetScrollViewer2(zoom, page, disable_animation);
        }

        public bool SetScrollViewer3(float? zoom, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (!Loaded)
            {
                return false;
            }

            return InternalSetScrollViewer(new SetScrollViewerContext
            {
                Zoom = zoom,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        public async Task<bool> UpdateImages(LockContext db, bool use_final)
        {
            if (!await UpdatePage(use_final))
            {
                return false;
            }

            await InternalUpdateImages(db);
            return true;
        }

        // Modifier - Events
        private readonly Utils.CancellationLock m_lock_container_loaded = new Utils.CancellationLock();
        public async Task OnContainerLoaded(ReaderFrameViewModel ctx)
        {
            LockContext db = new LockContext();

            if (ctx == null || ctx.Container == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            } 

            await m_lock_container_loaded.WaitAsync();
            try
            {
                // Wait until the framework was fully loaded.
                if (!LoadedFramework)
                {
                    await Utils.C0.WaitFor(() => LoadedFramework, 5000);

                    if (!LoadedFramework)
                    {
                        return;
                    }

#if DEBUG_LOG_LOAD
                    Log("Framework loaded");
#endif
                }

                // Initialize the framework if first page was loaded.
                if (!LoadedFirstPage && (ctx.PageL == 1 || ctx.PageR == 1))
                {
                    await Utils.C0.Sync(delegate
                    {
                        SetScrollViewer1(Zoom, null, true);
                        AdjustPadding();
                    });

                    LoadedFirstPage = true;

#if DEBUG_LOG_LOAD
                    Log("First page loaded");
#endif
                }

                // Update margin if last page was loaded.
                if (!LoadedLastPage && (ctx.PageL == PageCount || ctx.PageR == PageCount))
                {
                    await Utils.C0.Sync(delegate
                    {
                        AdjustPadding();
                    });

                    LoadedLastPage = true;

#if DEBUG_LOG_LOAD
                    Log("Last page loaded");
#endif
                }

                // Check if the initial page was loaded.
                if (!LoadedInitialPage && LoadedFirstPage)
                {
                    // Try jump to the initial page.
                    LoadedInitialPage = InternalSetScrollViewer2(null, InitialPage, true);

#if DEBUG_LOG_LOAD
                    if (LoadedInitialPage)
                    {
                        Log("Initial page loaded");
                    }
#endif
                }

                if (!LoadedImages && LoadedInitialPage)
                {
                    if (await UpdatePage(true))
                    {
                        await InternalUpdateImages(db);
                        LoadedImages = true;

#if DEBUG_LOG_LOAD
                        Log("Images loaded");
#endif
                    }
                }

                // Check if the reader was loaded.
                if (!Loaded && LoadedInitialPage)
                {
                    Loaded = true;
                    OnLoaded?.Invoke();

#if DEBUG_LOG_LOAD
                    Log("Reader loaded");
#endif
                }
            }
            finally
            {
                m_lock_container_loaded.Release();
            }
        }

        public async Task<bool> OnViewChanged(LockContext db, bool final)
        {
            if (!Loaded)
            {
                return false;
            }

            if (!IsCurrentReader)
            {
                // Clear images.
                await InternalUpdateImages(db);
                return false;
            }

            if (!await UpdatePage(true))
            {
                return false;
            }

            if (final)
            {
                SyncFinalVal();

                // Notify the scroll viewer to update its inner states.
                SetScrollViewer1(null, null, false);

                if (!IsContinuous && Zoom < ForceContinuousZoomThreshold)
                {
                    // Stick our view to the center of two pages.
                    InternalMoveFrame(0, false);
                }

#if DEBUG_LOG_VIEW_CHANGE
                Log("ViewChanged:"
                    + " Z=" + ZoomFactorFinal.ToString()
                    + ",H=" + HorizontalOffsetFinal.ToString()
                    + ",V=" + VerticalOffsetFinal.ToString()
                    + ",P=" + PageReal.ToString());
#endif
            }

            await InternalUpdateImages(db, final);
            return true;
        }

        public void OnSizeChanged()
        {
            AdjustPadding();
        }

        private Utils.CancellationLock m_lock_page_rearrange = new Utils.CancellationLock();
        public void PageRearrangeEventSealed()
        {
            Utils.C0.Run(async delegate
            {
                var db = new LockContext();

                await m_lock_page_rearrange.WaitAsync();
                try
                {
                    // Set reader status to Loading.
                    m_shared.ReaderStatus = ReaderStatusEnum.Loading;

                    // Save previous states.
                    double page = PageReal;
                    float zoom = Math.Min(Zoom, 100f);

                    // Update Frames.
                    ResetFrames();
                    
                    for (int i = 0; i < PageCount; ++i)
                    {
                        if (m_lock_page_rearrange.CancellationRequested)
                        {
                            return;
                        }

                        await UpdateFrame(i);
                    }

                    await CompleteFrameUpdate();

                    // Jump to previous page.
                    // DO NOT disable animation here or else TransformToVisual (which will be
                    // called later in OnViewChanged) will give erroneous results.
                    // Still don't know why. Been stuck here for 4h.
                    SetScrollViewer2(zoom, page, false);

                    // Update images.
                    await UpdateImages(db, true);

                    // Recover reader status.
                    m_shared.ReaderStatus = ReaderStatusEnum.Working;
                }
                finally
                {
                    m_lock_page_rearrange.Release();
                }
            });
        }

        // Observer - General
        public bool IsCurrentReader => m_shared.ReaderSettings.IsVertical == IsVertical;
        public bool IsVertical { get; private set; }
        public bool IsHorizontal => !IsVertical;
        public bool IsLastPage => PageToFrame(Page, out _, out _) >= Frames.Count - 1;
        public bool IsContinuous => IsVertical ?
            m_shared.ReaderSettings.IsVerticalContinuous :
            m_shared.ReaderSettings.IsHorizontalContinuous;
        public PageArrangementEnum PageArrangement => IsVertical ?
            m_shared.ReaderSettings.VerticalPageArrangement :
            m_shared.ReaderSettings.HorizontalPageArrangement;

        // Observer - Frames
        public ObservableCollection<ReaderFrameViewModel> Frames { get; private set; } = new ObservableCollection<ReaderFrameViewModel>();

        // Observer - Pages
        public int PageCount => Comic.ImageCount;

        public double PageReal { get; private set; } = 0.0;
        public int Page => (int)Math.Round(PageReal);

        public float Zoom { get; private set; } = 90f;

        // Observer - Scroll Viewer
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;

        private float m_zoom_factor_final;
        public float ZoomFactorFinal
        {
            get
            {
                FillFinalVal();
                return m_zoom_factor_final;
            }
            set
            {
                FillFinalVal();
                m_zoom_factor_final = value;
            }
        }

        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;

        private double m_horizontal_offset_final;
        public double HorizontalOffsetFinal
        {
            get
            {
                FillFinalVal();
                return m_horizontal_offset_final;
            }
            set
            {
                FillFinalVal();
                m_horizontal_offset_final = value;
            }
        }

        public double VerticalOffset => ThisScrollViewer.VerticalOffset;

        private double m_vertical_offset_final;
        public double VerticalOffsetFinal
        {
            get
            {
                FillFinalVal();
                return m_vertical_offset_final;
            }
            set
            {
                FillFinalVal();
                m_vertical_offset_final = value;
            }
        }

        private bool m_disable_animation_final;
        public bool DisableAnimationFinal
        {
            get
            {
                FillFinalVal();
                return m_disable_animation_final;
            }
            set
            {
                FillFinalVal();
                m_disable_animation_final = value;
            }
        }

        public double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
        public double ParallelOffsetFinal => IsVertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
        public double ViewportParallelLength => IsVertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => IsVertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
        public double FrameParallelLength(int i) => IsVertical ? Frames[i].Height : Frames[i].Width;

        // Observer - List View
        private double m_padding_start_final;
        public double PaddingStartFinal
        {
            get
            {
                FillFinalVal();
                return m_padding_start_final;
            }
            set
            {
                FillFinalVal();
                m_padding_start_final = value;
            }
        }

        private double m_padding_end_final;
        public double PaddingEndFinal
        {
            get
            {
                FillFinalVal();
                return m_padding_end_final;
            }
            set
            {
                FillFinalVal();
                m_padding_end_final = value;
            }
        }

        // Internal - Variables
        private readonly ReaderPageShared m_shared;
        private bool m_final_value_set = false;

        // Internal - Loader States
        private bool LoadedFramework => ThisScrollViewer != null && ThisListView != null;
        private bool LoadedFirstPage { get; set; } = false;
        private bool LoadedLastPage { get; set; } = false;
        private bool LoadedInitialPage { get; set; } = false;
        private bool LoadedImages { get; set; } = false;
        public bool Loaded { get; private set; } = false;

        // Internal - General
        private double? ZoomCoefficient(int frame_idx)
        {
            if (Frames.Count == 0)
            {
                return null;
            }
            
            if (frame_idx < 0 || frame_idx >= Frames.Count)
            {
                System.Diagnostics.Debug.Assert(false);
                return null;
            }

            double viewport_width = ThisScrollViewer.ViewportWidth;
            double viewport_height = ThisScrollViewer.ViewportHeight;
            double frame_width = Frames[frame_idx].FrameWidth;
            double frame_height = Frames[frame_idx].FrameHeight;

            double viewport_ratio = viewport_width / viewport_height;
            double image_ratio = frame_width / frame_height;

            return 0.01 * (viewport_ratio > image_ratio ?
                viewport_height / frame_height :
                viewport_width / frame_width);
        }

        private Tuple<double, double> PageOffset(double page)
        {
            int page_int = (int)page;
            page_int = Math.Max(page_int, 1);
            page_int = Math.Min(page_int, PageCount + 1);

            int frame = PageToFrame(page_int, out _, out int neighbor);
            FrameOffsets offsets = GetFrameOffsets(frame);

            if (offsets == null)
            {
                return null;
            }

            double perpendicular_offset = offsets.PerpendicularCenter * ZoomFactorFinal - ViewportPerpendicularLength * 0.5;

            int page_min;
            int page_max;

            if (neighbor == -1)
            {
                page_min = page_max = page_int;
            }
            else
            {
                page_min = Math.Min(page_int, neighbor);
                page_max = Math.Max(page_int, neighbor);
            }

            double parallel_offset;

            if (page_min <= page && page <= page_max)
            {
                parallel_offset = offsets.ParallelCenter;
            }
            else if (page < page_min)
            {
                double page_frac = (0.5 - page_min + page) * 2.0;
                parallel_offset = offsets.ParallelBegin + page_frac * (offsets.ParallelCenter - offsets.ParallelBegin);
            }
            else
            {
                double page_frac = (page - page_max) * 2.0;
                parallel_offset = offsets.ParallelCenter + page_frac * (offsets.ParallelEnd - offsets.ParallelCenter);
            }

            parallel_offset = parallel_offset * ZoomFactorFinal - ViewportParallelLength * 0.5;
            return new Tuple<double, double>(parallel_offset, perpendicular_offset);
        }

        private class FrameOffsets
        {
            public double ParallelBegin;
            public double ParallelCenter;
            public double ParallelEnd;
            public double PerpendicularCenter;
        };

        private FrameOffsets GetFrameOffsets(int frame)
        {
            if (frame < 0 || frame >= Frames.Count)
            {
                return null;
            }

            ReaderFrameViewModel item = Frames[frame];
            Grid container = item.Container;

            if (container == null)
            {
                return null;
            }

            GeneralTransform frame_transform = container.TransformToVisual(ThisListView);
            Point frame_position = frame_transform.TransformPoint(new Point(0.0, 0.0));

            double parallel_offset = IsVertical ? frame_position.Y : frame_position.X;
            double perpendicular_offset = IsVertical ? frame_position.X : frame_position.Y;

            bool left_to_right = m_shared.ReaderSettings.IsLeftToRight;

            if (IsHorizontal && !left_to_right)
            {
                parallel_offset -= item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right;
            }

            return new FrameOffsets
            {
                ParallelBegin = parallel_offset,
                ParallelCenter = parallel_offset + (IsVertical ?
                    item.FrameMargin.Top + item.FrameHeight * 0.5 :
                    item.FrameMargin.Left + item.FrameWidth * 0.5),
                ParallelEnd = parallel_offset + (IsVertical ?
                    item.FrameMargin.Top + item.FrameHeight + item.FrameMargin.Bottom :
                    item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right),
                PerpendicularCenter = perpendicular_offset + (IsVertical ?
                    item.FrameMargin.Left + item.FrameWidth * 0.5 :
                    item.FrameMargin.Top + item.FrameHeight * 0.5),
            };
        }

        /// <summary>
        /// We assume that Page is already up-to-date at this moment.
        /// </summary>
        /// <param name="increment"></param>
        /// <param name="disable_animation"></param>
        private bool InternalMoveFrame(int increment, bool disable_animation)
        {
            if (Frames.Count == 0)
            {
                return false;
            }

            int frame = PageToFrame(Page, out _, out _);
            frame += increment;
            frame = Math.Min(Frames.Count - 1, frame);
            frame = Math.Max(0, frame);
            double page = Frames[frame].Page;
            float? zoom = Zoom > 101f ? 100f : (float?)null;

            return InternalSetScrollViewer2(zoom, page, disable_animation);
        }

        private bool InternalSetScrollViewer2(float? zoom, double? page, bool disable_animation)
        {
            double? horizontal_offset = null;
            double? vertical_offset = null;

            if (page.HasValue)
            {
                Tuple<double, double> offsets = PageOffset(page.Value);

                if (offsets == null)
                {
                    return false;
                }

                if (Math.Abs(offsets.Item1 - ParallelOffsetFinal) < 1.0)
                {
                    // IMPORTANT: Ignore the request if target offset is really close to the current offset,
                    // or else it could stuck in a dead loop. (See reference in OnScrollViewerViewChanged())
                    return true;
                }

                ConvertOffset(ref horizontal_offset, ref vertical_offset, offsets.Item1, offsets.Item2);
            }

            return InternalSetScrollViewer(new SetScrollViewerContext
            {
                Zoom = zoom,
                PagePrediction = page,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        private class SetScrollViewerContext
        {
            // Zoom
            public float? Zoom = null;
            public double? PagePrediction = null;

            // Offset
            public double? HorizontalOffset = null;
            public double? VerticalOffset = null;
            
            // Animation
            public bool DisableAnimation = false;
        };

        private bool InternalSetScrollViewer(SetScrollViewerContext ctx)
        {
            if (!LoadedFramework)
            {
                return false;
            }

#if DEBUG_LOG_JUMP
            Log("ParamIn: "
                + "Z=" + zoom.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + ",D=" + disable_animation.ToString());
#endif

            SetScrollViewerZoom(ctx, out float? zoom_out);

#if DEBUG_LOG_JUMP
            Log("ParamSetZoom: "
                + "Z=" + zoom.ToString()
                + ",Zo=" + zoom_out.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString());
#endif

            if (!ChangeView(zoom_out, ctx.HorizontalOffset, ctx.VerticalOffset, ctx.DisableAnimation))
            {
                return false;
            }

            Zoom = ctx.Zoom.Value;
            m_shared.NavigationPageShared.ZoomInEnabled = Zoom < MaxZoom - 1.0f;
            m_shared.NavigationPageShared.ZoomOutEnabled = Zoom > MinZoom + 1.0f;
            AdjustParallelOffset();
            return true;
        }

        private void SetScrollViewerZoom(SetScrollViewerContext ctx, out float? zoom_factor)
        {
            // Calculate zoom coefficient prediction.
            double zoom_coefficient_pred;
            int frame_pred;
            {
                int page = ctx.PagePrediction.HasValue ? (int)ctx.PagePrediction.Value : Page;
                frame_pred = PageToFrame(page, out _, out _);

                if (frame_pred < 0 || frame_pred >= Frames.Count)
                {
                    frame_pred = 0;
                }

                double? zoom_coefficient_pred_boxed = ZoomCoefficient(frame_pred);

                if (!zoom_coefficient_pred_boxed.HasValue)
                {
                    ctx.Zoom = Zoom;
                    zoom_factor = null;
                    return;
                }

                zoom_coefficient_pred = zoom_coefficient_pred_boxed.Value;
            }

            // Calculate zoom in percentage.
            float zoom;

            if (ctx.Zoom.HasValue)
            {
                zoom = ctx.Zoom.Value;
            }
            else
            {
                int frame = PageToFrame(Page, out _, out _);

                if (frame < 0 || frame >= Frames.Count)
                {
                    frame = 0;
                }

                double zoom_coefficient = zoom_coefficient_pred;

                if (frame != frame_pred)
                {
                    double? zoom_coefficient_boxed = ZoomCoefficient(frame);

                    if (zoom_coefficient_boxed.HasValue)
                    {
                        zoom_coefficient = zoom_coefficient_boxed.Value;
                    }
                }

                zoom = (float)(ZoomFactorFinal / zoom_coefficient);
            }

            zoom = Math.Min(zoom, MaxZoom);
            zoom = Math.Max(zoom, MinZoom);
            ctx.Zoom = zoom;

            // A zoom factor vary less than 1% will be ignored.
            float zoom_factor_pred = (float)(zoom * zoom_coefficient_pred);

            if (Math.Abs(zoom_factor_pred / ZoomFactorFinal - 1.0f) <= 0.01f)
            {
                zoom_factor = null;
                return;
            }

            zoom_factor = zoom_factor_pred;

            // Apply zooming.
            if (ctx.HorizontalOffset == null)
            {
                ctx.HorizontalOffset = HorizontalOffsetFinal;
            }

            if (ctx.VerticalOffset == null)
            {
                ctx.VerticalOffset = VerticalOffsetFinal;
            }

            ctx.HorizontalOffset += ThisScrollViewer.ViewportWidth * 0.5;
            ctx.HorizontalOffset *= (float)zoom_factor / ZoomFactorFinal;
            ctx.HorizontalOffset -= ThisScrollViewer.ViewportWidth * 0.5;

            ctx.VerticalOffset += ThisScrollViewer.ViewportHeight * 0.5;
            ctx.VerticalOffset *= (float)zoom_factor / ZoomFactorFinal;
            ctx.VerticalOffset -= ThisScrollViewer.ViewportHeight * 0.5;

            ctx.HorizontalOffset = Math.Max(0.0, ctx.HorizontalOffset.Value);
            ctx.VerticalOffset = Math.Max(0.0, ctx.VerticalOffset.Value);
        }

        private bool ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (horizontal_offset == null && vertical_offset == null && zoom_factor == null)
            {
                return true;
            }

            if (horizontal_offset != null)
            {
                HorizontalOffsetFinal = horizontal_offset.Value;
            }

            if (vertical_offset != null)
            {
                VerticalOffsetFinal = vertical_offset.Value;
            }

            if (zoom_factor != null)
            {
                ZoomFactorFinal = zoom_factor.Value;
            }

            if (disable_animation)
            {
                DisableAnimationFinal = true;
            }

            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, DisableAnimationFinal);

#if DEBUG_LOG_JUMP
            Log("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + DisableAnimationFinal.ToString());
#endif

            return true;
        }

        private void AdjustParallelOffset()
        {
            if (Frames.Count == 0)
            {
                return;
            }

#if DEBUG_LOG_JUMP
            Log("Adjusting offset");
#endif

            double? movement_forward = null;
            double? movement_backward = null;
            double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;

            if (Frames[0].Container != null)
            {
                double space = PaddingStartFinal * ZoomFactorFinal - ParallelOffsetFinal;
                double image_center_offset = (PaddingStartFinal + FrameParallelLength(0) * 0.5) * ZoomFactorFinal;
                double image_center_to_screen_center = image_center_offset - screen_center_offset;
                movement_forward = Math.Min(space, image_center_to_screen_center);
            }

            if (Frames[Frames.Count - 1].Container != null)
            {
                double space = PaddingEndFinal * ZoomFactorFinal - (ExtentParallelLengthFinal
                    - ParallelOffsetFinal - ViewportParallelLength);
                double image_center_offset = ExtentParallelLengthFinal - (PaddingEndFinal
                    + FrameParallelLength(Frames.Count - 1) * 0.5) * ZoomFactorFinal;
                double image_center_to_screen_center = screen_center_offset - image_center_offset;
                movement_backward = Math.Min(space, image_center_to_screen_center);
            }

            if (movement_forward.HasValue && movement_backward.HasValue)
            {
                if (movement_forward.Value >= 0 && movement_backward.Value >= 0)
                {
                    return;
                }

                if (movement_forward.Value <= 0 && movement_backward.Value <= 0)
                {
                    return;
                }

                if (movement_forward.Value + movement_backward.Value >= 0)
                {
                    return;
                }
            }

            if (movement_forward.HasValue && movement_forward.Value > 0)
            {
                double parallel_offset = ParallelOffsetFinal + movement_forward.Value;
                ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
            else if (movement_backward.HasValue && movement_backward.Value > 0)
            {
                double parallel_offset = ParallelOffsetFinal - movement_backward.Value;
                ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
        }

        private void AdjustPadding()
        {
            if (!LoadedFramework)
            {
                return;
            }

            if (Frames.Count == 0)
            {
                return;
            }

#if DEBUG_LOG_JUMP
            Log("Adjusting padding");
#endif

            double padding_start = PaddingStartFinal;
            do
            {
                int frame_idx = 0;

                if (Frames[frame_idx].Container == null)
                {
                    break;
                }

                double? zoom_coefficient = ZoomCoefficient(frame_idx);

                if (!zoom_coefficient.HasValue)
                {
                    break;
                }

                double zoom_factor = MinZoom * zoom_coefficient.Value;
                double inner_length = ViewportParallelLength / zoom_factor;
                padding_start = (inner_length - FrameParallelLength(frame_idx)) / 2;
                padding_start = Math.Max(0.0, padding_start);
            } while (false);

            double padding_end = PaddingEndFinal;
            do
            {
                int frame_idx = Frames.Count - 1;

                if (Frames[frame_idx].Container == null)
                {
                    break;
                }

                double? zoom_coefficient = ZoomCoefficient(frame_idx);

                if (!zoom_coefficient.HasValue)
                {
                    break;
                }

                double zoom_factor = MinZoom * zoom_coefficient.Value;
                double inner_length = ViewportParallelLength / zoom_factor;
                padding_end = (inner_length - FrameParallelLength(frame_idx)) / 2;
                padding_end = Math.Max(0.0, padding_end);
            } while (false);

            PaddingStartFinal = padding_start;
            PaddingEndFinal = padding_end;

            if (IsVertical)
            {
                ThisListView.Padding = new Thickness(0.0, padding_start, 0.0, padding_end);
            }
            else
            {
                ThisListView.Padding = new Thickness(padding_start, 0.0, padding_end, 0.0);
            }
        }

        private readonly Utils.CancellationLock m_lock_update_page = new Utils.CancellationLock();
        private async Task<bool> UpdatePage(bool use_final)
        {
            if (!LoadedFramework)
            {
                return false;
            }

            await m_lock_update_page.WaitAsync();
            try
            {
                double offset;
                {
                    double parallel_offset = use_final ? ParallelOffsetFinal : ParallelOffset;
                    double zoom_factor = use_final ? ZoomFactorFinal : ZoomFactor;
                    offset = (parallel_offset + ViewportParallelLength * 0.5) / zoom_factor;
                }

                // Locate current frame using binary search.
                if (Frames.Count == 0)
                {
                    return false;
                }

                int begin = 0;
                int end = Frames.Count - 1;

                while (begin < end)
                {
                    if (m_lock_update_page.CancellationRequested)
                    {
                        return false;
                    }

                    int i = (begin + end + 1) / 2;
                    ReaderFrameViewModel item = Frames[i];
                    FrameOffsets offsets = GetFrameOffsets(i);

                    if (offsets == null)
                    {
                        return false;
                    }

                    if (offsets.ParallelBegin < offset)
                    {
                        begin = i;
                    }
                    else
                    {
                        end = i - 1;
                    }
                }

                ReaderFrameViewModel frame = Frames[begin];
                FrameOffsets frame_offsets = GetFrameOffsets(begin);

                // Convert offset to page.
                int page_min;
                int page_max;

                if (frame.PageL == -1 && frame.PageR == -1)
                {
                    // This could happen when we are rearranging pages.
                    return false;
                }

                if (frame.PageL == -1)
                {
                    page_min = page_max = frame.PageR;
                }
                else if (frame.PageR == -1)
                {
                    page_min = page_max = frame.PageL;
                }
                else
                {
                    page_min = Math.Min(frame.PageL, frame.PageR);
                    page_max = Math.Max(frame.PageL, frame.PageR);
                }

                double page;

                if (offset < frame_offsets.ParallelCenter)
                {
                    double page_frac = (offset - frame_offsets.ParallelBegin) / (frame_offsets.ParallelCenter - frame_offsets.ParallelBegin);
                    page = page_min - 0.5 + page_frac * 0.5;
                }
                else
                {
                    double page_frac = (offset - frame_offsets.ParallelCenter) / (frame_offsets.ParallelEnd - frame_offsets.ParallelCenter);
                    page = page_max + page_frac * 0.5;
                }

                PageReal = page;

#if DEBUG_LOG_UPDATE_PAGE
                Log("Page updated (" +
                    "Page=" + m_page.ToString() +
                    ",UseF=" + use_final.ToString() +
                    ",PO=" + parallel_offset.ToString() +
                    ",ZF=" + zoom_factor.ToString() +
                    ",O=" + current_offset.ToString() + ")");
#endif

                return true;
            }
            finally
            {
                m_lock_update_page.Release();
            }
        }

        private readonly Utils.CancellationLock m_lock_update_images = new Utils.CancellationLock();
        private async Task InternalUpdateImages(LockContext db, bool remove_out_of_view = true)
        {
#if DEBUG_LOG_UPDATE_IMAGE
            Log("Updating images (page " + Page.ToString() + ")");
#endif

            await m_lock_update_images.WaitAsync();
            try
            {
                if (!IsCurrentReader)
                {
                    foreach (ReaderFrameViewModel m in Frames)
                    {
                        m.ImageL = null;
                        m.ImageR = null;
                    }

                    return;
                }

                var img_loader_tokens = new List<Utils.ImageLoaderToken>();
                int page_begin = Math.Max(Page - 5, 1);
                int page_end = Math.Min(Page + 10, PageCount);
                int idx_begin = PageToFrame(page_begin, out _, out _);
                int idx_end = PageToFrame(page_end, out _, out _);
                idx_end = Math.Min(Frames.Count - 1, idx_end);
                
                for (int i = idx_begin; i <= idx_end; ++i)
                {
                    ReaderFrameViewModel m = Frames[i]; // Stores locally.

                    if (m.ImageL == null && m.PageL > 0)
                    {
                        img_loader_tokens.Add(new Utils.ImageLoaderToken
                        {
                            Index = m.PageL - 1,
                            Comic = Comic,
                            Callback = (BitmapImage img) =>
                            {
                                m.ImageL = img;
                            }
                        });
                    }

                    if (m.ImageR == null && m.PageR > 0)
                    {
                        img_loader_tokens.Add(new Utils.ImageLoaderToken
                        {
                            Index = m.PageR - 1,
                            Comic = Comic,
                            Callback = (BitmapImage img) =>
                            {
                                m.ImageR = img;
                            }
                        });
                    }
                }

                await Utils.ImageLoader.Load(db, img_loader_tokens,
                    double.PositiveInfinity, double.PositiveInfinity,
                    m_lock_update_images);

                if (remove_out_of_view)
                {
                    for (int i = 0; i < Frames.Count; ++i)
                    {
                        ReaderFrameViewModel m = Frames[i];

                        if ((m.PageL < page_begin || m.PageL > page_end) && m.ImageL != null)
                        {
                            m.ImageL = null;
                        }

                        if ((m.PageR < page_begin || m.PageR > page_end) && m.ImageR != null)
                        {
                            m.ImageR = null;
                        }
                    }
                }
            }
            finally
            {
                m_lock_update_images.Release();
            }
        }

        // Internal - Final Value
        private void FillFinalVal()
        {
            if (!LoadedFramework)
            {
                return;
            }

            if (m_final_value_set)
            {
                return;
            }

            m_padding_start_final = IsVertical ? ThisListView.Padding.Top : ThisListView.Padding.Left;
            m_padding_end_final = IsVertical ? ThisListView.Padding.Bottom : ThisListView.Padding.Right;
            m_horizontal_offset_final = HorizontalOffset;
            m_vertical_offset_final = VerticalOffset;
            m_zoom_factor_final = ZoomFactor;
            m_disable_animation_final = false;
            m_final_value_set = true;
        }

        private void SyncFinalVal()
        {
            m_final_value_set = false;
        }

        // Internal - Conversions
        private void ConvertOffset(ref double? to_horizontal, ref double? to_vertical, double? from_parallel, double? from_perpendicular)
        {
            if (IsVertical)
            {
                if (from_parallel != null)
                {
                    to_vertical = from_parallel;
                }

                if (from_perpendicular != null)
                {
                    to_horizontal = from_perpendicular;
                }
            }
            else
            {
                if (from_parallel != null)
                {
                    to_horizontal = from_parallel;
                }

                if (from_perpendicular != null)
                {
                    to_vertical = from_perpendicular;
                }
            }
        }

        private double? HorizontalVal(double? parallel_val, double? perpendicular_val)
        {
            return IsVertical ? perpendicular_val : parallel_val;
        }

        private double? VerticalVal(double? parallel_val, double? perpendicular_val)
        {
            return IsVertical ? parallel_val : perpendicular_val;
        }

        private double FinalVal(double val)
        {
            return val / ZoomFactor * ZoomFactorFinal;
        }

        private int PageToFrame(int page, out bool left_side, out int neighbor)
        {
            switch (PageArrangement)
            {
                case PageArrangementEnum.Single:
                    left_side = true;
                    neighbor = -1;
                    return page - 1;
                case PageArrangementEnum.DualCover:
                    left_side = page == 1 || page % 2 == 0;
                    neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page + 1 : page - 1) : -1;
                    return page / 2;
                case PageArrangementEnum.DualCoverMirror:
                    left_side = page == PageCount || page % 2 == 1;
                    neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page - 1 : page + 1) : -1;
                    return page / 2;
                case PageArrangementEnum.DualNoCover:
                    left_side = page % 2 == 1;
                    neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page + 1 : page - 1) : -1;
                    return (page - 1) / 2;
                case PageArrangementEnum.DualNoCoverMirror:
                    left_side = page == PageCount || page % 2 == 0;
                    neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page - 1 : page + 1) : -1;
                    return (page - 1) / 2;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    goto case PageArrangementEnum.Single;
            }
        }

        // Internal - Debug
        private void Log(string text)
        {
            if (!IsCurrentReader)
            {
                return;
            }

            Utils.Debug.Log("Reader: " + text);
        }
    }

    public sealed partial class ReaderPage : Page
    {
        public ReaderPageShared Shared { get; set; }
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;
        private ComicData m_comic;
        private readonly GestureRecognizer m_gesture_recognizer;

        // Bottom Tile
        private bool m_bottom_tile_showed = false;
        private bool m_bottom_tile_hold = false;
        private bool m_bottom_tile_pointer_in = false;
        private DateTimeOffset m_bottom_tile_hide_request_time = DateTimeOffset.Now;

        // Locks
        private readonly Utils.CancellationLock m_lock_load_comic = new Utils.CancellationLock();

        public ReaderPage()
        {
            Shared = new ReaderPageShared();
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

            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabUpdate = OnTabUpdate,
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabStart = OnTabStart
            };

            m_comic = null;

            m_gesture_recognizer = new GestureRecognizer();
            m_gesture_recognizer.GestureSettings =
                GestureSettings.ManipulationTranslateX |
                GestureSettings.ManipulationTranslateY |
                GestureSettings.ManipulationTranslateInertia |
                GestureSettings.ManipulationScale;
            m_gesture_recognizer.ManipulationStarted += OnManipulationStarted;
            m_gesture_recognizer.ManipulationUpdated += OnManipulationUpdated;
            m_gesture_recognizer.ManipulationCompleted += OnManipulationCompleted;
            
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
            Shared.ReaderSettings.OnHorizontalContinuousChanged += HorizontalReader.PageRearrangeEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged += VerticalReader.PageRearrangeEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged += HorizontalReader.PageRearrangeEventSealed;

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
            Shared.ReaderSettings.OnHorizontalContinuousChanged -= HorizontalReader.PageRearrangeEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged -= VerticalReader.PageRearrangeEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged -= HorizontalReader.PageRearrangeEventSealed;
        }

        private void OnTabUpdate()
        {
            Utils.C0.Run(async delegate
            {
                Shared.BottomTilePinned = false;
                await LoadComicInfo();
            });
        }

        private void OnTabStart(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Reader;
                tab_id.Type = Utils.Tab.PageType.Reader;

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
                    TaskResult result = await m_comic.UpdateImages(db, cover_only: false, reload: true);

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
                        await VerticalReader.UpdateFrame(i);
                        await HorizontalReader.UpdateFrame(i);
                    }

                    // Refresh reader.
                    await reader.UpdateImages(db, true);
                }

                await LoadImages(db);
                await reader.CompleteFrameUpdate();

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

            var preview_img_loader_tokens = new List<Utils.ImageLoaderToken>();
            ComicData comic = m_comic; // Stores locally.
            Utils.Stopwatch save_timer = new Utils.Stopwatch();

            for (int i = 0; i < comic.ImageCount; ++i)
            {
                int index = i; // Stores locally.
                int page = i + 1; // Stores locally.

                preview_img_loader_tokens.Add(new Utils.ImageLoaderToken
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
                        await VerticalReader.UpdateFrame(index);
                        await HorizontalReader.UpdateFrame(index);
                    }
                });
            }

            save_timer.Start();
            await Utils.ImageLoader.Load(db,  preview_img_loader_tokens,
                preview_width, preview_height, m_lock_load_comic);
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
            double page = reader.PageReal;

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
                m_comic.SaveProgress(progress, reader.PageReal);
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

                double page = last_reader.PageReal;
                float zoom = Math.Min(100f, last_reader.Zoom);

                await Utils.C0.WaitFor(() => reader.Loaded, 1000);
                reader.SetScrollViewer2(zoom, page, true);
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
                CoreVirtualKeyStates ctrl_state = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control);
                
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
                reader.SetScrollViewer2(null, ctx.Page, true);
            });
        }

        // Manipulation
        private void OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            // do nothing.

#if DEBUG_LOG_MANIPULATION
            Log("Manipulation started");
#endif
        }

        private void OnManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            ReaderModel reader = GetCurrentReader();

            if (reader == null)
            {
                return;
            }

            double dx = e.Delta.Translation.X;
            double dy = e.Delta.Translation.Y;
            float scale = e.Delta.Scale;

            if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
            {
                dx = -dx;
            }

            float? zoom = null;

            if (Math.Abs(scale - 1.0f) > 0.01f)
            {
                zoom = reader.Zoom * scale;
            }

            reader.SetScrollViewer3(zoom, reader.HorizontalOffsetFinal - dx, reader.VerticalOffsetFinal - dy, false);
        }

        private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ReaderModel reader = GetCurrentReader();

                if (reader == null || reader.IsContinuous || reader.Zoom >= ReaderModel.ForceContinuousZoomThreshold)
                {
                    return;
                }

                double velocity = reader.IsVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

                if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
                {
                    velocity = -velocity;
                }

                if (velocity > 1.0)
                {
                    await reader.MoveFrame(-1);
                }
                else if (velocity < -1.0)
                {
                    await reader.MoveFrame(1);
                }

#if DEBUG_LOG_MANIPULATION
                Log("Manipulation completed, V=" + velocity.ToString());
#endif
            });
        }

        private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            (sender as UIElement).CapturePointer(e.Pointer);
            m_gesture_recognizer.ProcessDownEvent(e.GetCurrentPoint(ManipulationReference));

#if DEBUG_LOG_MANIPULATION
            Log("Pointer pressed");
#endif
        }

        private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));

#if DEBUG_LOG_MANIPULATION
            //Log("Pointer moved");
#endif
        }

        private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessUpEvent(e.GetCurrentPoint(ManipulationReference));
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!m_gesture_recognizer.AutoProcessInertia)
            {
                m_gesture_recognizer.CompleteGesture();
            }

#if DEBUG_LOG_MANIPULATION
            Log("Pointer released");
#endif
        }

        private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessUpEvent(e.GetCurrentPoint(ManipulationReference));
            (sender as UIElement).ReleasePointerCapture(e.Pointer);

            if (!m_gesture_recognizer.AutoProcessInertia)
            {
                m_gesture_recognizer.CompleteGesture();
            }

#if DEBUG_LOG_MANIPULATION
            Log("Pointer canceled");
#endif
        }

        private void OnReaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
#if DEBUG_LOG_MANIPULATION
            Log("Pointer capture lost");
#endif
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

            reader.SetScrollViewer3((int)zoom, null, null, false);
        }

        private void ZoomIn()
        {
            ReaderSetZoom(1);
        }

        private void ZoomOut()
        {
            ReaderSetZoom(-1);
        }

        private void OnZoomInBtClicked(object sender, RoutedEventArgs e)
        {
            ZoomIn();
        }

        private void OnZoomOutBtClicked(object sender, RoutedEventArgs e)
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

        private void OnFavoriteBtChecked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await SetIsFavorite(true);
            });
        }

        private void OnFavoriteBtUnchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await SetIsFavorite(false);
            });
        }

        private void OnFavoritesBtClicked(object sender, RoutedEventArgs e)
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

        private void OnComicInfoClicked(object sender, RoutedEventArgs e)
        {
            ExpandInfoPane();
        }

        private void OnRatingControlValueChanged(muxc.RatingControl sender, object args)
        {
            m_comic.SaveRating((int)sender.Value);
        }

        private void OnInfoPaneTagClicked(object sender, RoutedEventArgs e)
        {
            TagViewModel ctx = (TagViewModel)((Button)sender).DataContext;
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Search, "<tag: " + ctx.Tag + ">");
        }

        private void OnEditBtClicked(object sender, RoutedEventArgs e)
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

        private void OnPinButtonClick(object sender, RoutedEventArgs e)
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

        private void OnReaderTapped(object sender, TappedRoutedEventArgs e)
        {
            BottomTileSetHold(!m_bottom_tile_showed);
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
                    case Windows.System.VirtualKey.Right:
                        if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
                        {
                            await reader.MoveFrame(-1);
                        }
                        else
                        {
                            await reader.MoveFrame(1);
                        }
                        break;

                    case Windows.System.VirtualKey.Left:
                        if (reader.IsHorizontal && !Shared.ReaderSettings.IsLeftToRight)
                        {
                            await reader.MoveFrame(1);
                        }
                        else
                        {
                            await reader.MoveFrame(-1);
                        }
                        break;

                    case Windows.System.VirtualKey.Up:
                        await reader.MoveFrame(-1);
                        break;

                    case Windows.System.VirtualKey.Down:
                        await reader.MoveFrame(1);
                        break;

                    case Windows.System.VirtualKey.PageUp:
                        await reader.MoveFrame(-1);
                        break;

                    case Windows.System.VirtualKey.PageDown:
                        await reader.MoveFrame(1);
                        break;

                    case Windows.System.VirtualKey.Home:
                        reader.SetScrollViewer2(null, 1, true);
                        break;

                    case Windows.System.VirtualKey.End:
                        reader.SetScrollViewer2(null, reader.PageCount, true);
                        break;

                    case Windows.System.VirtualKey.Space:
                        await reader.MoveFrame(1);
                        break;

                    case Windows.System.VirtualKey.F:
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