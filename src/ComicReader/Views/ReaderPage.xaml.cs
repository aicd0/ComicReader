#if DEBUG
//#define DEBUG_LOG_LOAD
//#define DEBUG_LOG_JUMP
//#define DEBUG_LOG_VIEW_CHANGE
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_MANIPULATION
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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

    public class ReaderModel
    {
        private const float max_zoom = 250f;
        private const float min_zoom = 90f;

        // Modifiable
        public ObservableCollection<ReaderFrameViewModel> DataSource { get; private set; }
            = new ObservableCollection<ReaderFrameViewModel>();
        public ComicData Comic { get; set; } = null;
        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;
        public double InitialPage { private get; set; } = 0.0;

        public Action OnLoaded;

        // Observable
        //  Common
        public bool IsLoaded { get; private set; } = false;
        public bool IsCurrentReader => m_shared.IsReaderVertical == IsVertical;
        public bool IsVertical { get; private set; }
        public bool IsHorizontal => !IsVertical;
        public bool IsOnePage { get; private set; }
        public bool IsTwoPages => !IsOnePage;
        public int Pages => Comic.ImageCount;

        private double m_page = 0.0;
        public int Page => (int)m_page;
        public double PageReal => m_page;

        private float m_zoom = 90f;
        public float Zoom => m_zoom;

        //  Scroll viewer
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;

        private float m_zoom_factor_final;
        public float ZoomFactorFinal
        {
            get
            {
                _FillFinalVal();
                return m_zoom_factor_final;
            }
            set
            {
                m_zoom_factor_final = value;
            }
        }

        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;

        private double m_horizontal_offset_final;
        public double HorizontalOffsetFinal
        {
            get
            {
                _FillFinalVal();
                return m_horizontal_offset_final;
            }
            set
            {
                _FillFinalVal();
                m_horizontal_offset_final = value;
            }
        }

        public double VerticalOffset => ThisScrollViewer.VerticalOffset;

        private double m_vertical_offset_final;
        public double VerticalOffsetFinal
        {
            get
            {
                _FillFinalVal();
                return m_vertical_offset_final;
            }
            set
            {
                _FillFinalVal();
                m_vertical_offset_final = value;
            }
        }

        public double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
        public double ParallelOffsetFinal => IsVertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
        public double ViewportParallelLength => IsVertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => IsVertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
        public double FrameParallelLength(int i) => IsVertical ? DataSource[i].Height : DataSource[i].Width;

        //  List view
        private double m_padding_start_final;
        public double PaddingStartFinal
        {
            get
            {
                _FillFinalVal();
                return m_padding_start_final;
            }
        }

        private double m_padding_end_final;
        public double PaddingEndFinal
        {
            get
            {
                _FillFinalVal();
                return m_padding_end_final;
            }
        }

        // Reader state
        private bool IsFrameworkLoaded => ThisScrollViewer != null && ThisListView != null;
        private bool IsFrameworkReady = false;
        private bool IsLastPageLoaded = false;
        private bool IsInitialPageReached = false;
        private bool IsImageUpdateSucceeded = false;

        private ReaderPageShared m_shared;
        private bool m_final_value_set = false;
        private bool m_disable_animation_final;
        private Utils.CancellationLock m_update_image_lock = new Utils.CancellationLock();

        public ReaderModel(ReaderPageShared shared, bool is_vertical)
        {
            m_shared = shared;

            IsVertical = is_vertical;
            IsOnePage = is_vertical;
        }

        // Modifiers
        public void Reset()
        {
            IsLoaded = false;
            IsFrameworkReady = false;
            IsLastPageLoaded = false;
            IsInitialPageReached = false;
            IsImageUpdateSucceeded = false;

            Comic = null;
            InitialPage = 0.0;
            OnLoaded = null;
            DataSource.Clear();

            _SyncFinalVal();
#if DEBUG_LOG_LOAD
            Log("========== Reset ==========");
#endif
        }

        /// <summary>
        /// Add or update DataSource with index and image aspect ratio. Index has to be<br/>
        /// continuous. As a new item is added to DataSource, the reader will start loading<br/>
        /// it automatically. Make sure to set everything up before the initial call to this<br/>
        /// function.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="image_aspect_ratio"></param>
        public void UpdateDataSource(int index, double image_aspect_ratio)
        {
            double new_image_width = IsVertical ? 500.0 : 300.0 * image_aspect_ratio;
            double new_image_height = IsVertical ? 500.0 / image_aspect_ratio : 300.0;

            // Normally data source items will be added one by one.
            if (index > DataSource.Count)
            {
                // Unexpected item.
                return;
            }

            if (index >= DataSource.Count)
            {
                bool top_padding = IsVertical;
                bool bottom_padding = IsVertical;
                bool left_padding = IsHorizontal && (IsOnePage || index == 0 || index % 2 == 1);
                bool right_padding = IsHorizontal && (IsOnePage || index == Pages - 1 || index % 2 == 0);

                DataSource.Add(new ReaderFrameViewModel
                {
                    Page = IndexToPage(index),
                    TopPadding = top_padding ? 10 : 0,
                    BottomPadding = bottom_padding ? 10 : 0,
                    LeftPadding = left_padding ? 100 : 0,
                    RightPadding = right_padding ? 100 : 0,
                    ImageWidth = new_image_width,
                    ImageHeight = new_image_height,
                    OnContainerLoadedAsync = OnContainerLoaded
                });
            }
            else
            {
                ReaderFrameViewModel item = DataSource[index];
                item.ImageWidth = new_image_width;
                item.ImageHeight = new_image_height;
            }
        }

        // Controlling
        public async Task<bool> IncreasePage(int increment, bool disable_animation)
        {
            if (!await _UpdatePage(true))
            {
                return false;
            }

            _IncreasePage(increment, disable_animation);
            return true;
        }

        public bool SetScrollViewer(float? zoom, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (!IsLoaded)
            {
                return false;
            }

            return _SetScrollViewer(zoom, horizontal_offset, vertical_offset, null, /* whatever */false, disable_animation);
        }

        public bool SetScrollViewer(float? zoom, double? page, bool use_page_center, bool disable_animation)
        {
            if (!IsLoaded)
            {
                return false;
            }

            return _SetScrollViewer(zoom, null, null, page, use_page_center, disable_animation);
        }

        public async Task<bool> UpdateImage(LockContext db, bool use_final)
        {
            if (!await _UpdatePage(use_final))
            {
                return false;
            }

            await _UpdateImages(db);
            return true;
        }

        // Events
        private Utils.CancellationLock m_container_loaded_lock = new Utils.CancellationLock();
        public async Task OnContainerLoaded(ReaderFrameViewModel ctx)
        {
            LockContext db = new LockContext();

            System.Diagnostics.Debug.Assert(ctx != null);
            System.Diagnostics.Debug.Assert(ctx.Container != null);

            await m_container_loaded_lock.WaitAsync();
            try
            {
                // Wait until the framework was fully loaded.
                if (!IsFrameworkLoaded)
                {
                    await Utils.C0.WaitFor(() => IsFrameworkLoaded);
#if DEBUG_LOG_LOAD
                    Log("Framework loaded");
#endif
                }

                // Initialize the framework if first page was loaded.
                if (!IsFrameworkReady && ctx.Page == 1)
                {
                    await Utils.C0.Sync(delegate
                    {
                        SetScrollViewer(m_zoom, null, null, true);
                        _AdjustPadding();
                    });

                    IsFrameworkReady = true;
#if DEBUG_LOG_LOAD
                    Log("Framework ready");
#endif
                }

                // Update margin if last page was loaded.
                if (!IsLastPageLoaded && ctx.Page == Pages)
                {
                    await Utils.C0.Sync(delegate
                    {
                        _AdjustPadding();
                    });

                    IsLastPageLoaded = true;
#if DEBUG_LOG_LOAD
                    Log("Last page loaded");
#endif
                }

                // Check if the initial page was loaded.
                if (!IsInitialPageReached && IsFrameworkReady)
                {
                    // Try jump to the initial page.
                    IsInitialPageReached = _SetScrollViewer(null, null, null, InitialPage, false, true);
#if DEBUG_LOG_LOAD
                    if (IsInitialPageReached)
                    {
                        Log("Initial page reached");
                    }
#endif
                }

                if (!IsImageUpdateSucceeded && IsInitialPageReached)
                {
                    if (await _UpdatePage(true))
                    {
                        await _UpdateImages(db);
                        IsImageUpdateSucceeded = true;
#if DEBUG_LOG_LOAD
                        Log("Image updated");
#endif
                    }
                }

                // Check if the reader was loaded.
                if (!IsLoaded && IsInitialPageReached)
                {
                    IsLoaded = true;
#if DEBUG_LOG_LOAD
                    Log("Reader loaded");
#endif
                    OnLoaded?.Invoke();
                }
            }
            finally
            {
                m_container_loaded_lock.Release();
            }
        }

        public async Task<bool> OnViewChanged(LockContext db, bool final)
        {
            if (!IsLoaded)
            {
                return false;
            }

            if (!IsCurrentReader)
            {
                // Clear images.
                await _UpdateImages(db);
                return false;
            }

            if (!await _UpdatePage(false))
            {
                return false;
            }

            if (final)
            {
                _SyncFinalVal();

                // Notify the scroll viewer to update its inner states.
                SetScrollViewer(null, null, null, false);

                if (IsTwoPages && Zoom <= 100)
                {
                    // Stick our view to the center of two pages.
                    _IncreasePage(0, false);
                }
#if DEBUG_LOG_VIEW_CHANGE
                Log("ViewChanged:"
                    + " Z=" + ZoomFactorFinal.ToString()
                    + ",H=" + HorizontalOffsetFinal.ToString()
                    + ",V=" + VerticalOffsetFinal.ToString()
                    + ",P=" + PageReal.ToString());
#endif
            }

            await _UpdateImages(db, final);
            return true;
        }

        public void OnSizeChanged()
        {
            _AdjustPadding();
        }

        // Internal functions
        //  Controlling
        private double? _ZoomCoefficient()
        {
            if (DataSource.Count == 0)
            {
                return null;
            }

            int idx = PageToIndex(Page);

            if (DataSource.Count <= idx)
            {
                idx = 0;
            }

            if (DataSource.Count <= idx)
            {
                return null;
            }

            double viewport_width = ThisScrollViewer.ViewportWidth;
            double viewport_height = ThisScrollViewer.ViewportHeight;
            double image_width = DataSource[idx].ImageWidth;
            double image_height = DataSource[idx].ImageHeight;

            if (IsTwoPages)
            {
                // For two-pages reader, double the image width(height) to
                // approximate the width(height) of two pages.
                if (IsVertical)
                {
                    image_height *= 2.0;
                }
                else
                {
                    image_width *= 2.0;
                }
            }

            double viewport_ratio = viewport_width / viewport_height;
            double image_ratio = image_width / image_height;
            return 0.01 * (viewport_ratio > image_ratio ?
                viewport_height / image_height :
                viewport_width / image_width);
        }

        private double? _PageOffset(int page, bool use_page_center)
        {
            page = Math.Max(1, page);
            page = Math.Min(Pages, page);
            int page_idx = page - 1;

            if (page_idx >= DataSource.Count || page_idx < 0)
            {
                return null;
            }

            ReaderFrameViewModel item = DataSource[page_idx];
            Grid page_container = item.Container;

            if (page_container == null)
            {
                return null;
            }

            var page_transform = page_container.TransformToVisual(ThisListView);
            Point page_position = page_transform.TransformPoint(new Point(0.0, 0.0));
            double parallel_offset = IsVertical ? page_position.Y : page_position.X;

            if (use_page_center)
            {
                parallel_offset += IsVertical ?
                    item.Margin.Top + item.ImageHeight * 0.5 :
                    item.Margin.Left + item.ImageWidth * 0.5;
            }

            return parallel_offset;
        }

        private double? _PageOffsetTransformed(double page, bool use_page_center)
        {
            page = Math.Max(1.0, page);
            page = Math.Min(Pages, page);

            int page_int = (int)page;
            double page_frac = page - page_int;

            double offset;

            // Calculate parallel offset of this page.
            double? this_offset = _PageOffset(page_int, use_page_center);

            if (!this_offset.HasValue)
            {
                return null;
            }

            offset = this_offset.Value;

            // Calculate parallel offset of next page and mix with this page.
            double? _next_offset = _PageOffset(page_int + 1, use_page_center);

            if (_next_offset.HasValue)
            {
                offset += page_frac * (_next_offset.Value - this_offset.Value);
            }

            offset = offset * ZoomFactorFinal - ViewportParallelLength * 0.5;
            offset = Math.Max(0.0, offset);
            return offset;
        }

        private void _IncreasePage(int increment, bool disable_animation)
        {
            int new_page_int = Page + increment * (IsTwoPages ? 2 : 1);
            double new_page = new_page_int;

            if (IsTwoPages && new_page_int > 1)
            {
                // stick to the center of two pages
                new_page = new_page_int + (new_page_int % 2 == 0 ? 0.5 : -0.5);
            }

            double? new_parallel_offset = _PageOffsetTransformed(new_page, use_page_center: true);

            if (new_parallel_offset == null || Math.Abs(new_parallel_offset.Value - ParallelOffsetFinal) < 1.0)
            {
                // IMPORTANT: Ignore the request if target offset is really close to the current offset,
                // or else it could stuck in a dead loop. (See reference in OnScrollViewerViewChanged())
                return;
            }

            float? zoom = Zoom > 101f ? 100f : (float?)null;
            SetScrollViewer(zoom, new_page, use_page_center: true, disable_animation);
            m_page = new_page_int;
        }

        private bool _SetScrollViewer(float? zoom, double? horizontal_offset, double? vertical_offset,
            double? page, bool use_page_center, bool disable_animation)
        {
            if (!IsFrameworkLoaded) return false;

#if DEBUG_LOG_JUMP
            Log("ParamIn: "
                + "Z=" + zoom.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + ",P=" + page.ToString()
                + ",Pc=" + use_page_center.ToString()
                + ",D=" + disable_animation.ToString());
#endif

            bool r = _SetScrollViewerPage(page, use_page_center, ref horizontal_offset, ref vertical_offset);

            if (!r)
            {
                return false;
            }

#if DEBUG_LOG_JUMP
            Log("ParamSetPage: "
                + "H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString());
#endif

            _SetScrollViewerZoom(ref horizontal_offset, ref vertical_offset, ref zoom, out float? zoom_out);

#if DEBUG_LOG_JUMP
            Log("ParamSetZoom: "
                + "Z=" + zoom.ToString()
                + ",Zo=" + zoom_out.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString());
#endif
            
            if (!_ChangeView(zoom_out, horizontal_offset, vertical_offset, disable_animation))
            {
                return false;
            }

            m_zoom = zoom.Value;
            m_shared.NavigationPageShared.ZoomInEnabled = m_zoom < max_zoom;
            m_shared.NavigationPageShared.ZoomOutEnabled = m_zoom > min_zoom;
            _AdjustParallelOffset();
            return true;
        }

        private void _SetScrollViewerZoom(ref double? horizontal_offset, ref double? vertical_offset, ref float? zoom, out float? zoom_factor)
        {
            double? zoom_coefficient_boxed = _ZoomCoefficient();

            if (zoom_coefficient_boxed == null)
            {
                zoom = m_zoom;
                zoom_factor = null;
                return;
            }

            double zoom_coefficient = zoom_coefficient_boxed.Value;
            bool zoom_sat = false;
            bool zoom_null = zoom == null;
            float zoom_rectified = zoom_null ? (float)(ZoomFactorFinal / zoom_coefficient) : (float)zoom;

            // accept an error less than 1
            if (zoom_rectified - max_zoom > 1)
            {
                zoom_sat = true;
                zoom_rectified = max_zoom;
            }
            else if (min_zoom - zoom_rectified > 1)
            {
                zoom_sat = true;
                zoom_rectified = min_zoom;
            }

            if (zoom_null && !zoom_sat)
            {
                zoom = Math.Abs(zoom_rectified - m_zoom) <= 1 ? m_zoom : zoom_rectified;
                zoom_factor = null;
                return;
            }

            zoom = zoom_rectified;
            zoom_factor = (float)(zoom_rectified * zoom_coefficient);

            if (horizontal_offset == null)
            {
                horizontal_offset = HorizontalOffsetFinal;
            }

            if (vertical_offset == null)
            {
                vertical_offset = VerticalOffsetFinal;
            }

            horizontal_offset += ThisScrollViewer.ViewportWidth * 0.5;
            horizontal_offset *= (float)zoom_factor / ZoomFactorFinal;
            horizontal_offset -= ThisScrollViewer.ViewportWidth * 0.5;

            vertical_offset += ThisScrollViewer.ViewportHeight * 0.5;
            vertical_offset *= (float)zoom_factor / ZoomFactorFinal;
            vertical_offset -= ThisScrollViewer.ViewportHeight * 0.5;

            horizontal_offset = Math.Max(0.0, horizontal_offset.Value);
            vertical_offset = Math.Max(0.0, vertical_offset.Value);
        }

        private bool _SetScrollViewerPage(double? page, bool use_page_center, ref double? horizontal_offset, ref double? vertical_offset)
        {
            if (page == null)
            {
                return true;
            }

            double? parallel_offset = _PageOffsetTransformed(page.Value, use_page_center);

            if (parallel_offset == null)
            {
                return false;
            }

            ConvertOffset(ref horizontal_offset, ref vertical_offset, parallel_offset, null);
            return true;
        }

        private void _AdjustParallelOffset()
        {
            if (DataSource.Count == 0)
            {
                return;
            }

#if DEBUG_LOG_JUMP
            Log("Adjusting offset");
#endif

            double? movement_forward = null;
            double? movement_backward = null;
            double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;

            if (DataSource[0].Container != null)
            {
                double space = PaddingStartFinal * ZoomFactorFinal - ParallelOffsetFinal;
                double image_center_offset = (PaddingStartFinal + FrameParallelLength(0) * 0.5) * ZoomFactorFinal;
                double image_center_to_screen_center = image_center_offset - screen_center_offset;
                movement_forward = Math.Min(space, image_center_to_screen_center);
            }

            if (DataSource[DataSource.Count - 1].Container != null)
            {
                double space = PaddingEndFinal * ZoomFactorFinal - (ExtentParallelLengthFinal
                    - ParallelOffsetFinal - ViewportParallelLength);
                double image_center_offset = ExtentParallelLengthFinal - (PaddingEndFinal
                    + FrameParallelLength(DataSource.Count - 1) * 0.5) * ZoomFactorFinal;
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
                _ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
            else if (movement_backward.HasValue && movement_backward.Value > 0)
            {
                double parallel_offset = ParallelOffsetFinal - movement_backward.Value;
                _ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
        }

        private bool _ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
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
                m_disable_animation_final = true;
            }

            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_disable_animation_final);

#if DEBUG_LOG_JUMP
            Log("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_disable_animation_final);
#endif

            return true;
        }

        private void _AdjustPadding()
        {
            if (!IsFrameworkLoaded)
            {
                return;
            }

            double? zoom_coefficient_boxed = _ZoomCoefficient();
            if (zoom_coefficient_boxed == null) return;
            double zoom_coefficient = zoom_coefficient_boxed.Value;

            if (DataSource[0].Container == null || DataSource[DataSource.Count - 1].Container == null)
            {
                return;
            }

#if DEBUG_LOG_JUMP
            Log("Adjusting padding");
#endif

            double zoom_factor = min_zoom * zoom_coefficient;
            double inner_length = ViewportParallelLength / zoom_factor;
            double new_start = (inner_length - FrameParallelLength(0)) / 2;
            double new_end = (inner_length - FrameParallelLength(DataSource.Count - 1)) / 2;

            new_start = Math.Max(0.0, new_start);
            new_end = Math.Max(0.0, new_end);

            _FillFinalVal();
            m_padding_start_final = new_start;
            m_padding_end_final = new_end;

            if (IsVertical)
            {
                ThisListView.Padding = new Thickness(0.0, new_start, 0.0, new_end);
            }
            else
            {
                ThisListView.Padding = new Thickness(new_start, 0.0, new_end, 0.0);
            }
        }

        private Utils.CancellationLock m_update_page_lock = new Utils.CancellationLock();
        private async Task<bool> _UpdatePage(bool use_final)
        {
            if (!IsFrameworkLoaded)
            {
                return false;
            }

            await m_update_page_lock.WaitAsync();
            try
            {
                double parallel_offset = use_final ? ParallelOffsetFinal : ParallelOffset;
                double zoom_factor = use_final ? ZoomFactorFinal : ZoomFactor;
                double current_offset = (parallel_offset + ViewportParallelLength * 0.5) / zoom_factor;

                // Use binary search to locate the current page.
                if (DataSource.Count == 0)
                {
                    return false;
                }

                int begin = 1;
                int end = DataSource.Count + 1;

                while (begin < end)
                {
                    if (m_update_page_lock.CancellationRequested)
                    {
                        return false;
                    }

                    int p = (begin + end) / 2;
                    double? page_offset = _PageOffset(p, use_page_center: false);

                    if (!page_offset.HasValue)
                    {
                        return false;
                    }

                    if (page_offset.Value < current_offset)
                    {
                        begin = p + 1;
                    }
                    else
                    {
                        end = p;
                    }
                }

                int page = begin - 1;

                if (page <= 0)
                {
                    return false;
                }

                double? this_offset = _PageOffset(page, use_page_center: false);
                double? next_offset = _PageOffset(page + 1, use_page_center: false);
                double res;

                if (this_offset.HasValue && next_offset.HasValue &&
                    this_offset.Value < current_offset && current_offset <= next_offset.Value)
                {
                    res = page + (current_offset - this_offset.Value) / (next_offset.Value - this_offset.Value);
                }
                else
                {
                    res = page;
                }

                m_page = res;
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
                m_update_page_lock.Release();
            }
        }

        private async Task _UpdateImages(LockContext db, bool remove_out_of_view = true)
        {
#if DEBUG_LOG_LOAD
            Log("Updating images (page " + Page.ToString() + ")");
#endif
            await m_update_image_lock.WaitAsync();
            try
            {
                if (!IsCurrentReader)
                {
                    foreach (ReaderFrameViewModel m in DataSource)
                    {
                        m.ImageSource = null;
                    }

                    return;
                }

                var img_loader_tokens = new List<Utils.ImageLoaderToken>();
                int page_begin = Math.Max(Page - 5, 1);
                int page_end = Math.Min(Page + 10, Pages);
                int idx_begin = PageToIndex(page_begin);
                int idx_end = PageToIndex(page_end);
                idx_end = Math.Min(DataSource.Count - 1, idx_end);
                
                for (int i = idx_begin; i <= idx_end; ++i)
                {
                    ReaderFrameViewModel m = DataSource[i]; // Stores locally.

                    if (m.ImageSource != null)
                    {
                        continue;
                    }

                    img_loader_tokens.Add(new Utils.ImageLoaderToken
                    {
                        Index = i,
                        Comic = Comic,
                        Callback = (BitmapImage img) =>
                        {
                            m.ImageSource = img;
                        }
                    });
                }

                await Utils.ImageLoader.Load(db, img_loader_tokens,
                    double.PositiveInfinity, double.PositiveInfinity,
                    m_update_image_lock);

                if (remove_out_of_view)
                {
                    for (int i = 0; i < DataSource.Count; ++i)
                    {
                        ReaderFrameViewModel m = DataSource[i];

                        if (m.Page >= page_begin && m.Page <= page_end)
                        {
                            continue;
                        }

                        if (m.ImageSource != null)
                        {
                            m.ImageSource = null;
                        }
                    }
                }
            }
            finally
            {
                m_update_image_lock.Release();
            }
        }

        //  State management
        private void _FillFinalVal()
        {
            if (!IsFrameworkLoaded)
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

        private void _SyncFinalVal()
        {
            m_final_value_set = false;
        }

        //  Conversions
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

        private double? HorizontalVal(double? parallel_val, double? perpendicular_val) => IsVertical ? perpendicular_val : parallel_val;
        private double? VerticalVal(double? parallel_val, double? perpendicular_val) => IsVertical ? parallel_val : perpendicular_val;
        private double FinalVal(double val) => val / ZoomFactor * ZoomFactorFinal;
        private int PageToIndex(int page) => Math.Max(page - 1, 0);
        private int IndexToPage(int index) => Math.Max(index + 1, 1);

        //  Others
        private void Log(string text)
        {
            if (!IsCurrentReader)
            {
                return;
            }

            Utils.Debug.Log("Reader: " + text);
        }
    }

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

        private FlowDirection m_ReaderFlowDirection;
        public FlowDirection ReaderFlowDirection
        {
            get => m_ReaderFlowDirection;
            set
            {
                m_ReaderFlowDirection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReaderFlowDirection"));
            }
        }

        // comic info
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

        // read record
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

        // reader
        public ReaderStatusEnum ReaderStatus = ReaderStatusEnum.Loading;
        public bool IsReaderVertical = true;

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

        public void UpdateReaderUI()
        {
            bool is_working = ReaderStatus == ReaderStatusEnum.Working;
            bool grid_view_visible = is_working && NavigationPageShared.IsPreviewButtonToggled;
            bool reader_visible = is_working && !grid_view_visible;
            bool vertical_reader_visible = reader_visible && IsReaderVertical;
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
            NavigationPageShared.IsSwitchToVerticalReaderButtonVisible = !IsReaderVertical;
            NavigationPageShared.IsSwitchToHorizontalReaderButtonVisible = IsReaderVertical;
            NavigationPageShared.IsVerticalReaderVisible = vertical_reader_visible;
            NavigationPageShared.IsHorizontalReaderVisible = horizontal_reader_visible;
        }

        // bottom tile
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
        public ReaderPageShared Shared { get; set; }
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderFrameViewModel> PreviewDataSource { get; set; }

        private Utils.Tab.TabManager m_tab_manager;
        private ComicData m_comic;
        private Utils.TaskQueue m_load_image_queue;
        private GestureRecognizer m_gesture_recognizer;

        // Bottom Tile
        private bool m_bottom_tile_showed = false;
        private bool m_bottom_tile_hold = false;
        private bool m_bottom_tile_pointer_in = false;
        private DateTimeOffset m_bottom_tile_hide_request_time = DateTimeOffset.Now;

        // Locks
        private Utils.CancellationLock m_load_image_lock = new Utils.CancellationLock();

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
            PreviewDataSource = new ObservableCollection<ReaderFrameViewModel>();

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
            Shared.NavigationPageShared.OnSwitchReaderOrientation += SwitchReaderOrientation;
            Shared.NavigationPageShared.OnPreviewModeChanged += Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane += ExpandInfoPane;

            Shared.NavigationPageShared.IsPreviewButtonToggled = false;
            Shared.UpdateReaderUI();
        }

        private void OnTabUnregister()
        {
            Shared.NavigationPageShared.OnKeyDown -= OnKeyDown;
            Shared.NavigationPageShared.OnSwitchFavorites -= OnSwitchFavorites;
            Shared.NavigationPageShared.OnZoomIn -= ZoomIn;
            Shared.NavigationPageShared.OnZoomOut -= ZoomOut;
            Shared.NavigationPageShared.OnSwitchReaderOrientation -= SwitchReaderOrientation;
            Shared.NavigationPageShared.OnPreviewModeChanged -= Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane -= ExpandInfoPane;
        }

        private void OnTabUpdate()
        {
            Shared.ReaderFlowDirection = XmlDatabase.Settings.LeftToRight ?
                FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            Shared.BottomTilePinned = false;
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
                tab_id.Tab.IconSource = new muxc.SymbolIconSource { Symbol = Symbol.Document };
                await LoadComic(db, comic);
            });
        }

        public static string PageUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Location;
        }

        // Utilities
        private ReaderModel GetCurrentReader()
        {
            if (Shared.IsReaderVertical)
            {
                return VerticalReader;
            }
            else
            {
                return HorizontalReader;
            }
        }

        private void UpdatePage(ReaderModel control)
        {
            if (PageIndicator == null)
            {
                return;
            }

            string image_count = "?";

            if (m_comic != null)
            {
                image_count = m_comic.ImageCount.ToString();
            }

            PageIndicator.Text = control.Page.ToString() + " / " + image_count;
        }

        private async Task UpdateProgress(LockContext db, ReaderModel control)
        {
            int progress;

            if (m_comic.ImageCount == 0)
            {
                progress = 0;
            }
            else if (control.Page >= m_comic.ImageCount ||
                control.IsTwoPages && control.Page + 1 >= m_comic.ImageCount)
            {
                progress = 100;
            }
            else
            {
                progress = (int)((float)control.Page / control.Pages * 100);
            }

            progress = Math.Min(progress, 100);
            Shared.Progress = progress.ToString() + "%";
            await m_comic.SaveProgress(db, progress, control.PageReal);
        }

        // Loading
        private async Task LoadComic(LockContext db, ComicData comic)
        {
            if (comic == null)
            {
                return;
            }

            Shared.ReaderStatus = ReaderStatusEnum.Loading;
            Shared.UpdateReaderUI();

            VerticalReader.Reset();
            HorizontalReader.Reset();

            await SetActiveReader(db, VerticalReader);
            ReaderModel reader = GetCurrentReader();
            System.Diagnostics.Debug.Assert(reader != null);

            reader.OnLoaded = () =>
            {
                Shared.ReaderStatus = ReaderStatusEnum.Working;
                Shared.UpdateReaderUI();
                UpdatePage(reader);
                BottomTileShow();
                BottomTileHide(5000);
            };

            m_comic = comic;
            m_load_image_queue = Utils.TaskQueueManager.EmptyQueue();

            VerticalReader.Comic = m_comic;
            HorizontalReader.Comic = m_comic;

            // Additional procedures for comics in the library.
            if (!m_comic.IsExternal)
            {
                // Mark as read.
                await m_comic.SetAsRead(db);

                // Add to history
                await HistoryDataManager.Add(m_comic.Id, m_comic.Title1, true);

                // Update image files.
                TaskResult r = await m_comic.UpdateImages(db, cover: false, reload: true);

                if (!r.Successful)
                {
                    Log("Failed to load images of '" + m_comic.Location + "'. " + r.ExceptionType.ToString());
                    Shared.ReaderStatus = ReaderStatusEnum.Error;
                    Shared.UpdateReaderUI();
                    return;
                }

                // Set initial page.
                reader.InitialPage = m_comic.LastPosition;

                // Call UpdateDataSource() to load frames into readers.
                for (int i = 0; i < m_comic.ImageAspectRatios.Count; ++i)
                {
                    double image_aspect_ratio = m_comic.ImageAspectRatios[i];
                    VerticalReader.UpdateDataSource(i, image_aspect_ratio);
                    HorizontalReader.UpdateDataSource(i, image_aspect_ratio);
                }
            }

            Utils.TaskQueueManager.AppendTask(LoadImagesSealed(), "", m_load_image_queue);
            await LoadComicInfo();
        }

        private SealedTask LoadImagesSealed() =>
            (RawTask _) => LoadImagesUnsealed(new LockContext()).Result;

        private async RawTask LoadImagesUnsealed(LockContext db)
        {
            await m_load_image_lock.WaitAsync();

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
            Utils.Stopwatch save_timer = new Utils.Stopwatch();
            save_timer.Start();
            ComicData comic = m_comic; // Stores locally.

            for (int i = 0; i < comic.ImageCount; ++i)
            {
                int index = i; // Stores locally.
                int page = i + 1; // Stores locally.

                preview_img_loader_tokens.Add(new Utils.ImageLoaderToken
                {
                    Comic = comic,
                    Index = index,
                    Callback = async (BitmapImage img) =>
                    {
                        // Save image aspect ratio info.
                        double image_aspect_ratio = (double)img.PixelWidth / img.PixelHeight;

                        // Normally IAR items will be added one by one.
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
                            await comic.SaveImageAspectRatios(db);
                            save_timer.Lap();
                        }

                        // Update previews.
                        PreviewDataSource.Add(new ReaderFrameViewModel
                        {
                            ImageSource = img,
                            Page = page,
                        });

                        // Update reader frames.
                        VerticalReader.UpdateDataSource(index, image_aspect_ratio);
                        HorizontalReader.UpdateDataSource(index, image_aspect_ratio);
                    }
                });
            }

            await Utils.ImageLoader.Load(db,  preview_img_loader_tokens,
                preview_width, preview_height, m_load_image_lock);

            m_load_image_lock.Release();
            return new TaskResult();
        }

        private async Task LoadComicInfo()
        {
            System.Diagnostics.Debug.Assert(m_comic != null);

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

        // Reader
        private async Task<bool> SetActiveReader(LockContext db, ReaderModel reader)
        {
            System.Diagnostics.Debug.Assert(reader != null);

            ReaderModel last_reader = GetCurrentReader();

            if (last_reader != null)
            {
                if (!last_reader.IsLoaded || last_reader == reader)
                {
                    return false;
                }
            }

            Shared.IsReaderVertical = reader.IsVertical;
            ReaderModel this_reader = GetCurrentReader();

            System.Diagnostics.Debug.Assert(this_reader == reader);
            System.Diagnostics.Debug.Assert(last_reader == null || !last_reader.IsCurrentReader);
            System.Diagnostics.Debug.Assert(this_reader.IsCurrentReader);
            
            if (last_reader != null)
            {
                float zoom = Math.Min(100f, last_reader.Zoom);
                double position = last_reader.PageReal;

                Shared.UpdateReaderUI();
                await last_reader.UpdateImage(db, false);

                await Utils.C0.WaitFor(() => this_reader.IsLoaded, 1000);
                this_reader.SetScrollViewer(zoom, position, use_page_center: false, true);
            }
            else
            {
                Shared.UpdateReaderUI();
            }

            await this_reader.UpdateImage(db, true);
            return true;
        }

        public void SwitchReaderOrientation()
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                ReaderModel reader = null;

                if (Shared.IsReaderVertical)
                {
                    reader = HorizontalReader;
                }
                else
                {
                    reader = VerticalReader;
                }

                await SetActiveReader(db, reader);
            });
        }

        private void OnReaderScrollViewerViewChanged(ReaderModel control, ScrollViewerViewChangedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                if (await control.OnViewChanged(db, !e.IsIntermediate))
                {
                    UpdatePage(control);
                    await UpdateProgress(db, control);
                    BottomTileSetHold(false);
                }
            });
        }

        private void OnReaderScrollViewerSizeChanged(ReaderModel control, SizeChangedEventArgs e)
        {
            control.OnSizeChanged();
        }

        private void OnHorizontalReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                CoreVirtualKeyStates ctrl_state = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control);

                if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
                {
                    return;
                }

                ReaderModel control = HorizontalReader;

                if (control == null || control.Zoom > 105)
                {
                    return;
                }

                PointerPoint pt = e.GetCurrentPoint(null);
                int delta = -pt.Properties.MouseWheelDelta / 120;
                await control.IncreasePage(delta, false);

                // Set e.Handled to true to suppress the default behavior of scroll viewer (which will override ours)
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
                ReaderFrameViewModel ctx = (ReaderFrameViewModel)e.ClickedItem;

                Shared.NavigationPageShared.IsPreviewButtonToggled = false;

                ReaderModel reader = GetCurrentReader();

                if (reader == null)
                {
                    return;
                }

                await Utils.C0.WaitFor(() => reader.IsLoaded);
                reader.SetScrollViewer(null, ctx.Page, use_page_center: true, true);
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

            if (reader.IsHorizontal && Shared.ReaderFlowDirection == FlowDirection.RightToLeft)
            {
                dx = -dx;
            }

            reader.SetScrollViewer(reader.Zoom * scale,
                reader.HorizontalOffsetFinal - dx, reader.VerticalOffsetFinal - dy, false);
        }

        private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ReaderModel reader = GetCurrentReader();

                if (reader == null || reader.IsVertical || reader.Zoom > 105)
                {
                    return;
                }

                double velocity = e.Velocities.Linear.X;

                if (Shared.ReaderFlowDirection == FlowDirection.RightToLeft)
                {
                    velocity = -velocity;
                }

                if (velocity > 1.0)
                {
                    await reader.IncreasePage(-1, false);
                }
                else if (velocity < -1.0)
                {
                    await reader.IncreasePage(1, false);
                }
            });

#if DEBUG_LOG_MANIPULATION
            Log("Manipulation completed");
#endif
        }

        private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e, bool horizontal)
        {
            if (horizontal || e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                (sender as UIElement).CapturePointer(e.Pointer);
                m_gesture_recognizer.ProcessDownEvent(e.GetCurrentPoint(ManipulationReference));
#if DEBUG_LOG_MANIPULATION
                Log("Pointer pressed");
#endif
            }
        }

        private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e, bool horizontal)
        {
            if (horizontal || e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                m_gesture_recognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));
#if DEBUG_LOG_MANIPULATION
                //Log("Pointer moved");
#endif
            }
        }

        private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e, bool horizontal)
        {
            if (horizontal || e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                m_gesture_recognizer.ProcessUpEvent(e.GetCurrentPoint(ManipulationReference));
                (sender as UIElement).ReleasePointerCapture(e.Pointer);
#if DEBUG_LOG_MANIPULATION
                Log("Pointer released");
#endif
            }
        }

        private void OnVerticalReaderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerPressed(sender, e, false);
        }

        private void OnVerticalReaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerMoved(sender, e, false);
        }

        private void OnVerticalReaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, false);
        }

        private void OnVerticalReaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, false);
        }

        private void OnVerticalReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, false);
        }

        private void OnHorizontalReaderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerPressed(sender, e, true);
        }

        private void OnHorizontalReaderPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerMoved(sender, e, true);
        }

        private void OnHorizontalReaderPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, true);
        }

        private void OnHorizontalReaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, true);
        }

        private void OnHorizontalReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            OnReaderPointerReleased(sender, e, true);
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

            reader.SetScrollViewer((int)zoom, null, null, false);
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
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                await m_comic.SaveRating(db, (int)sender.Value);
            });
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

        private void OnSwitchReaderOrientationClicked(object sender, RoutedEventArgs e)
        {
            SwitchReaderOrientation();
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
                        if (reader.IsHorizontal && !XmlDatabase.Settings.LeftToRight)
                        {
                            await reader.IncreasePage(-1, false);
                        }
                        else
                        {
                            await reader.IncreasePage(1, false);
                        }
                        break;

                    case Windows.System.VirtualKey.Left:
                        if (reader.IsHorizontal && !XmlDatabase.Settings.LeftToRight)
                        {
                            await reader.IncreasePage(1, false);
                        }
                        else
                        {
                            await reader.IncreasePage(-1, false);
                        }
                        break;

                    case Windows.System.VirtualKey.Up:
                        await reader.IncreasePage(-1, false);
                        break;

                    case Windows.System.VirtualKey.Down:
                        await reader.IncreasePage(1, false);
                        break;

                    case Windows.System.VirtualKey.PageUp:
                        await reader.IncreasePage(-1, false);
                        break;

                    case Windows.System.VirtualKey.PageDown:
                        await reader.IncreasePage(1, false);
                        break;

                    case Windows.System.VirtualKey.Home:
                        reader.SetScrollViewer(null, 1, use_page_center: true, true);
                        break;

                    case Windows.System.VirtualKey.End:
                        reader.SetScrollViewer(null, reader.Pages, use_page_center: true, true);
                        break;

                    case Windows.System.VirtualKey.Space:
                        await reader.IncreasePage(1, false);
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