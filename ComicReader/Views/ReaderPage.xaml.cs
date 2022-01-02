#define DEBUG_LOG_READER_LOAD
#define DEBUG_LOG_READER_JUMP
#define DEBUG_LOG_VIEW_CHANGE

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Views
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;

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

        private ObservableCollection<TagsModel> m_ComicTags;
        public ObservableCollection<TagsModel> ComicTags
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
        public bool? IsReaderVertical = null;

        private bool m_IsVerticalReaderVisible = false;
        public bool IsVerticalReaderVisible
        {
            get => m_IsVerticalReaderVisible;
            set
            {
                m_IsVerticalReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsVerticalReaderVisible"));
            }
        }

        private bool m_IsHorizontalReaderVisible = false;
        public bool IsHorizontalReaderVisible
        {
            get => m_IsHorizontalReaderVisible;
            set
            {
                m_IsHorizontalReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsHorizontalReaderVisible"));
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
            bool grid_view_visible = NavigationPageShared.IsPreviewModeEnabled;
            bool reader_visible = !grid_view_visible && IsReaderVertical.HasValue;
            bool vertical_reader_visible = reader_visible && IsReaderVertical.Value;
            bool horizontal_reader_visible = reader_visible && !vertical_reader_visible;

            IsGridViewVisible = grid_view_visible;
            IsVerticalReaderVisible = vertical_reader_visible;
            IsHorizontalReaderVisible = horizontal_reader_visible;
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
                return BottomTilePinned ? "Unpin" : "Pin";
            }
        }

        public Action BottomTilePinnedChanged;
    }

    public class ReaderModel
    {
        private const int max_zoom = 250;
        private const int min_zoom = 90;

        public ReaderModel(ReaderPageShared shared, ScrollViewer scroll_viewer, ListView list_view, bool is_vertical)
        {
            IsVertical = is_vertical;
            IsOnePage = is_vertical;

            Shared = shared;
            ThisScrollViewer = scroll_viewer;
            ThisListView = list_view;

            DisplayInformation display_info = DisplayInformation.GetForCurrentView();
        }

        private bool m_final_value_set = false;
        private bool m_disable_animation_final;
        private Utils.CancellationLock m_update_image_lock = new Utils.CancellationLock();

        public ReaderPageShared Shared;
        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;

        public bool IsActive =>
            Shared.IsReaderVertical.HasValue &&
            Shared.IsReaderVertical.Value == IsVertical;

        public bool IsVertical { get; private set; }
        public bool IsHorizontal => !IsVertical;
        public bool IsOnePage { get; private set; }
        public bool IsTwoPages => !IsOnePage;
        public ComicData Comic { get; set; } = null;
        public int Pages => Comic.ImageFiles.Count;

        // True if ThisScrollViewer and ThisListView were loaded.
        public bool IsFrameworkLoaded => ThisScrollViewer != null && ThisListView != null;

        // True if IsFrameworkLoaded is true, and ThisScrollViewer was initialized.
        private bool IsFrameworkInitialized = false;

        // True if IsFrameworkInitialized and IsInitialPageLoaded is true, and initial page was set.
        public bool IsLoaded { get; private set; } = false;

        // True if the last page was loaded.
        private bool IsLastPageLoaded = false;

        // initial page
        private double m_initial_page = 0.0;
        private bool IsInitialPageLoaded = false;
        private bool IsInitialPageReached = false;

        public void SetInitialPage(double page_real)
        {
            m_initial_page = page_real;
        }

        // data source
        public ObservableCollection<ReaderFrameModel> DataSource { get; private set; }
            = new ObservableCollection<ReaderFrameModel>();

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
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            if (index >= DataSource.Count)
            {
                bool top_padding = IsVertical;
                bool bottom_padding = IsVertical;
                bool left_padding = IsHorizontal && (IsOnePage || index == 0 || index % 2 == 1);
                bool right_padding = IsHorizontal && (IsOnePage || index == Pages - 1 || index % 2 == 0);

                DataSource.Add(new ReaderFrameModel
                {
                    Page = IndexToPage(index),
                    TopPadding = top_padding,
                    BottomPadding = bottom_padding,
                    LeftPadding = left_padding,
                    RightPadding = right_padding,
                    ImageWidth = new_image_width,
                    ImageHeight = new_image_height,
                    OnContainerLoadedAsync = OnContainerLoaded
                });
            }
            else
            {
                ReaderFrameModel item = DataSource[index];
                item.ImageWidth = new_image_width;
                item.ImageHeight = new_image_height;
            }
        }

        // other properties
        private double m_page = 0.0;
        public int Page => (int)m_page;
        public double PageReal => m_page;

        private int m_zoom = 90;
        public int Zoom => m_zoom;
        
        // scroll viewer
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

        public double? ZoomCoefficient()
        {
            if (DataSource.Count == 0)
            {
                return null;
            }

            double viewport_width = ThisScrollViewer.ViewportWidth;
            double viewport_height = ThisScrollViewer.ViewportHeight;
            double image_width = DataSource[0].ImageWidth;
            double image_height = DataSource[0].ImageHeight;

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

        public double? PageOffset(int page, bool use_page_center)
        {
            page = Math.Max(1, page);
            page = Math.Min(Pages, page);
            int page_idx = page - 1;

            if (page_idx >= DataSource.Count || page_idx < 0)
            {
                return null;
            }

            ReaderFrameModel item = DataSource[page_idx];
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

        public double? PageOffsetTransformed(double page, bool use_page_center)
        {
            page = Math.Max(1.0, page);
            page = Math.Min(Pages, page);

            int page_int = (int)page;
            double page_frac = page - page_int;

            double offset;

            // Calculate parallel offset of this page.
            double? this_offset = PageOffset(page_int, use_page_center);

            if (!this_offset.HasValue)
            {
                return null;
            }

            offset = this_offset.Value;

            // Calculate parallel offset of next page and mix with this page.
            double? _next_offset = PageOffset(page_int + 1, use_page_center);

            if (_next_offset.HasValue)
            {
                offset += page_frac * (_next_offset.Value - this_offset.Value);
            }

            offset = offset * ZoomFactorFinal - ViewportParallelLength * 0.5;
            offset = Math.Max(0.0, offset);
            return offset;
        }

        public void IncreasePage(int increment, bool disable_animation)
        {
            int new_page_int = Page + increment * (IsTwoPages ? 2 : 1);
            double new_page = new_page_int;

            if (IsTwoPages && new_page_int > 1)
            {
                // stick to the center of two pages
                new_page = new_page_int + (new_page_int % 2 == 0 ? 0.5 : -0.5);
            }

            double? new_parallel_offset = PageOffsetTransformed(new_page, use_page_center: true);

            if (new_parallel_offset == null || Math.Abs(new_parallel_offset.Value - ParallelOffsetFinal) < 1.0)
            {
                // IMPORTANT: Ignore the request if target offset is really close to the current offset,
                // or else it could stuck in a dead loop. (See reference in OnScrollViewerViewChanged())
                return;
            }

            SetScrollViewer(null, new_page, use_page_center: true, disable_animation);
            m_page = new_page_int;
        }

        public bool SetScrollViewer(int? zoom, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (!IsLoaded)
            {
                return false;
            }

            return _SetScrollViewer(zoom, horizontal_offset, vertical_offset, null, /* whatever */false, disable_animation);
        }

        public bool SetScrollViewer(int? zoom, double? page, bool use_page_center, bool disable_animation)
        {
            if (!IsLoaded)
            {
                return false;
            }

            return _SetScrollViewer(zoom, null, null, page, use_page_center, disable_animation);
        }

        private bool _SetScrollViewer(int? zoom, double? horizontal_offset, double? vertical_offset,
            double? page, bool use_page_center, bool disable_animation)
        {
            if (!IsFrameworkLoaded) return false;

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("ParamIn:"
                + " Z=" + zoom.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + ",P=" + page.ToString()
                + ",Pc=" + use_page_center.ToString()
                + ",D=" + disable_animation.ToString()
                + "\n");
#endif

            bool r = _SetScrollViewerPage(page, use_page_center, ref horizontal_offset, ref vertical_offset);
            if (!r) return false;

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("ParamSetPage:"
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + "\n");
#endif

            _SetScrollViewerZoom(ref horizontal_offset, ref vertical_offset, ref zoom, out float? zoom_out);

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("ParamSetZoom:"
                + " Z=" + zoom.ToString()
                + ",Zo=" + zoom_out.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + "\n");
#endif
            
            if (!_ChangeView(zoom_out, horizontal_offset, vertical_offset, disable_animation))
            {
                return false;
            }

            m_zoom = (int)zoom;
            Shared.NavigationPageShared.ZoomInEnabled = m_zoom < max_zoom;
            Shared.NavigationPageShared.ZoomOutEnabled = m_zoom > min_zoom;
            _FixParallelOffset();
            return true;
        }

        private void _SetScrollViewerZoom(ref double? horizontal_offset, ref double? vertical_offset, ref int? zoom, out float? zoom_out)
        {
            double? zoom_coefficient_boxed = ZoomCoefficient();

            if (zoom_coefficient_boxed == null)
            {
                zoom = m_zoom;
                zoom_out = null;
                return;
            }

            double zoom_coefficient = zoom_coefficient_boxed.Value;
            bool zoom_sat = false;
            bool zoom_null = zoom == null;
            int zoom_cpy = zoom_null ? (int)(ZoomFactorFinal / zoom_coefficient) : (int)zoom;

            // accept an error less than 1
            if (zoom_cpy - max_zoom > 1)
            {
                zoom_sat = true;
                zoom_cpy = max_zoom;
            }
            else if (min_zoom - zoom_cpy > 1)
            {
                zoom_sat = true;
                zoom_cpy = min_zoom;
            }

            if (zoom_null && !zoom_sat)
            {
                zoom = Math.Abs(zoom_cpy - m_zoom) <= 1 ? m_zoom : zoom_cpy;
                zoom_out = null;
                return;
            }

            zoom = zoom_cpy;
            zoom_out = (float)(zoom * zoom_coefficient);

            if (horizontal_offset == null)
            {
                horizontal_offset = HorizontalOffsetFinal;
            }

            if (vertical_offset == null)
            {
                vertical_offset = VerticalOffsetFinal;
            }

            horizontal_offset += ThisScrollViewer.ViewportWidth * 0.5;
            horizontal_offset *= (float)zoom_out / ZoomFactorFinal;
            horizontal_offset -= ThisScrollViewer.ViewportWidth * 0.5;

            vertical_offset += ThisScrollViewer.ViewportHeight * 0.5;
            vertical_offset *= (float)zoom_out / ZoomFactorFinal;
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

            double? parallel_offset = PageOffsetTransformed(page.Value, use_page_center);

            if (parallel_offset == null)
            {
                return false;
            }

            ConvertOffset(ref horizontal_offset, ref vertical_offset, parallel_offset, null);
            return true;
        }

        private bool _FixParallelOffset()
        {
            if (DataSource.Count == 0)
            {
                return false;
            }

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("FixOffset\n");
#endif

            if (DataSource[0].Container == null)
            {
                return false;
            }

            double space = PaddingStartFinal * ZoomFactorFinal - ParallelOffsetFinal;
            double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;
            double image_center_offset = (PaddingStartFinal + FrameParallelLength(0) * 0.5) * ZoomFactorFinal;
            double image_center_to_screen_center = image_center_offset - screen_center_offset;
            double movement_forward = Math.Min(space, image_center_to_screen_center);

            if (movement_forward > 0)
            {
                double parallel_offset = ParallelOffsetFinal + movement_forward;
                return _ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }

            if (DataSource[DataSource.Count - 1].Container == null)
            {
                return false;
            }

            space = PaddingEndFinal * ZoomFactorFinal - (ExtentParallelLengthFinal
                - ParallelOffsetFinal - ViewportParallelLength);
            image_center_offset = ExtentParallelLengthFinal - (PaddingEndFinal
                + FrameParallelLength(DataSource.Count - 1) * 0.5) * ZoomFactorFinal;
            image_center_to_screen_center = screen_center_offset - image_center_offset;
            double movement_backward = Math.Min(space, image_center_to_screen_center);

            if (movement_backward > 0)
            {
                double parallel_offset = ParallelOffsetFinal - movement_backward;
                return _ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }

            return false;
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

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_disable_animation_final + "\n");
#endif

            return true;
        }

        // list view
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

        public void FixPadding()
        {
            if (!IsFrameworkLoaded) return;

            double? zoom_coefficient_boxed = ZoomCoefficient();
            if (zoom_coefficient_boxed == null) return;
            double zoom_coefficient = zoom_coefficient_boxed.Value;

            if (DataSource[0].Container == null || DataSource[DataSource.Count - 1].Container == null) return;

#if DEBUG_LOG_READER_JUMP
            System.Diagnostics.Debug.Print("FixPadding\n");
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

        // conversions
        public void ConvertOffset(ref double? to_horizontal, ref double? to_vertical, double? from_parallel, double? from_perpendicular)
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

        public double? HorizontalVal(double? parallel_val, double? perpendicular_val) => IsVertical ? perpendicular_val : parallel_val;
        public double? VerticalVal(double? parallel_val, double? perpendicular_val) => IsVertical ? parallel_val : perpendicular_val;
        private double FinalVal(double val) => val / ZoomFactor * ZoomFactorFinal;
        public int PageToIndex(int page) => Math.Max(page - 1, 0);
        public int IndexToPage(int index) => Math.Max(index + 1, 1);

        // events
        private Utils.CancellationLock m_container_loaded_lock = new Utils.CancellationLock();
        public async Task OnContainerLoaded(ReaderFrameModel ctx)
        {
            DatabaseContext db = new DatabaseContext();

            System.Diagnostics.Debug.Assert(ctx != null);
            System.Diagnostics.Debug.Assert(ctx.Container != null);

            await m_container_loaded_lock.WaitAsync();
            try
            {
                // Wait until the framework was fully loaded.
                if (!IsFrameworkLoaded)
                {
                    await Utils.Methods.WaitFor(() => IsFrameworkLoaded);
#if DEBUG_LOG_READER_LOAD
                    System.Diagnostics.Debug.Print("Reader framework loaded.\n");
#endif
                }

                // Initialize the framework if first page was loaded.
                if (!IsFrameworkInitialized && ctx.Page == 1)
                {
                    await Utils.Methods.Sync(delegate
                    {
                        SetScrollViewer(m_zoom, null, null, true);
                        FixPadding();
                    });

                    IsFrameworkInitialized = true;
#if DEBUG_LOG_READER_LOAD
                    System.Diagnostics.Debug.Print("Reader framework initialized.\n");
#endif
                }

                // Update margin if last page was loaded.
                if (!IsLastPageLoaded && ctx.Page == Pages)
                {
                    await Utils.Methods.Sync(delegate
                    {
                        FixPadding();
                    });

                    IsLastPageLoaded = true;
#if DEBUG_LOG_READER_LOAD
                    System.Diagnostics.Debug.Print("Last page loaded.\n");
#endif
                }

                // Check if the reader was loaded.
                if (!IsLoaded && IsFrameworkInitialized)
                {
                    IsLoaded = true;
#if DEBUG_LOG_READER_LOAD
                    System.Diagnostics.Debug.Print("Reader loaded.\n");
#endif
                }

                // Check if the initial page was loaded.
                if (!IsInitialPageReached && IsLoaded)
                {
                    if (!IsInitialPageLoaded)
                    {
                        int page_idx = PageToIndex((int)m_initial_page);

                        if (page_idx < DataSource.Count && DataSource[page_idx].Container != null)
                        {
                            int next_page = (int)m_initial_page + 1;
                            page_idx = PageToIndex(next_page);

                            if (next_page > Pages || (page_idx < DataSource.Count && DataSource[page_idx].Container != null))
                            {
                                IsInitialPageLoaded = true;
#if DEBUG_LOG_READER_LOAD
                                System.Diagnostics.Debug.Print("Initial page loaded.\n");
#endif
                            }
                        }
                    }

                    // Jump to the initial page if initial page was loaded.
                    if (IsInitialPageLoaded)
                    {
                        IsInitialPageReached = _SetScrollViewer(null, null, null, m_initial_page, false, true);
#if DEBUG_LOG_READER_LOAD
                        if (IsInitialPageReached)
                        {
                            await UpdateImages(db);
                            System.Diagnostics.Debug.Print("Initial page reached.\n");
                        }
#endif
                    }
                }
            }
            finally
            {
                m_container_loaded_lock.Release();
            }
        }

        public async Task OnScrollViewerViewChanged(DatabaseContext db, bool is_intermediate)
        {
            if (!IsLoaded)
            {
                return;
            }

            if (!is_intermediate)
            {
                _SyncFinalVal();

                // Notify the scroll viewer to update its inner states.
                SetScrollViewer(null, null, null, false);

                if (IsTwoPages && Zoom <= 100)
                {
                    // Stick our view to the center of two pages.
                    IncreasePage(0, false);
                }
#if DEBUG_LOG_VIEW_CHANGE
                System.Diagnostics.Debug.Print("ViewChanged:"
                    + " Z=" + ZoomFactorFinal.ToString()
                    + ",H=" + HorizontalOffsetFinal.ToString()
                    + ",V=" + VerticalOffsetFinal.ToString()
                    + ",P=" + PageReal.ToString()
                    + "\n");
#endif
            }

            await UpdateImages(db);
        }

        // others
        public void Reset()
        {
            DataSource.Clear();
            _SyncFinalVal();
            IsLastPageLoaded = false;
            IsFrameworkInitialized = false;
            IsInitialPageLoaded = false;
            IsInitialPageReached = false;
            IsLoaded = false;
        }

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

        private Utils.CancellationLock m_update_page_lock = new Utils.CancellationLock();
        private async Task<bool> UpdatePage()
        {
            if (!IsFrameworkLoaded)
            {
                return false;
            }

            await m_update_page_lock.WaitAsync();
            try
            {
                double current_offset = (ParallelOffset + ViewportParallelLength * 0.5) / ZoomFactor;

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
                    double? page_offset = PageOffset(p, use_page_center: false);

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

                double? this_offset = PageOffset(page, use_page_center: false);
                double? next_offset = PageOffset(page + 1, use_page_center: false);

                if (this_offset.HasValue && next_offset.HasValue &&
                    this_offset.Value < current_offset && current_offset <= next_offset.Value)
                {
                    m_page = page + (current_offset - this_offset.Value) / (next_offset.Value - this_offset.Value);
                }
                else
                {
                    m_page = page;
                }

                return true;
            }
            finally
            {
                m_update_page_lock.Release();
            }
        }

        private async Task UpdateImages(DatabaseContext db)
        {
            if (!IsLoaded || !await UpdatePage())
            {
                return;
            }

            await m_update_image_lock.WaitAsync();
            try
            {
                if (!IsActive)
                {
                    foreach (ReaderFrameModel m in DataSource)
                    {
                        m.ImageSource = null;
                    }

                    return;
                }

                List<ImageLoaderToken> img_loader_tokens = new List<ImageLoaderToken>();
                int page_begin = Math.Max(Page - 5, 1);
                int page_end = Math.Min(Page + 10, Pages);
                int idx_begin = PageToIndex(page_begin);
                int idx_end = PageToIndex(page_end);
                idx_end = Math.Min(DataSource.Count - 1, idx_end);
                
                for (int i = idx_begin; i <= idx_end; ++i)
                {
                    ReaderFrameModel m = DataSource[i]; // Stores locally.

                    if (m.ImageSource != null)
                    {
                        continue;
                    }

                    img_loader_tokens.Add(new ImageLoaderToken
                    {
                        Index = i,
                        Comic = Comic,
                        Callback = (BitmapImage img) =>
                        {
                            m.ImageSource = img;
                        }
                    });
                }

                await ComicDataManager.LoadImages(db, img_loader_tokens,
                    double.PositiveInfinity, double.PositiveInfinity,
                    m_update_image_lock);

                for (int i = 0; i < DataSource.Count; ++i)
                {
                    ReaderFrameModel m = DataSource[i];

                    if (m.Page >= page_begin || m.Page <= page_end)
                    {
                        continue;
                    }

                    if (m.ImageSource != null)
                    {
                        m.ImageSource = null;
                    }
                }
            }
            finally
            {
                m_update_image_lock.Release();
            }
        }
    }

    public sealed partial class ReaderPage : Page
    {
        public ReaderPageShared Shared { get; set; }
        private ReaderModel VerticalReader { get; set; }
        private ReaderModel HorizontalReader { get; set; }
        private ObservableCollection<ReaderFrameModel> PreviewDataSource { get; set; }

        private Utils.Tab.TabManager m_tab_manager;
        private ComicData m_comic;
        private Utils.TaskQueue.TaskQueue m_load_image_queue;
        private double m_reader_position;
        private GestureRecognizer m_gesture_recognizer;

        // bottom tile
        private bool m_bottom_tile_showed;
        private bool m_bottom_tile_hold;
        private bool m_bottom_tile_pointer_in;
        private int m_bottom_tile_exit_requests;

        // lock
        private SemaphoreSlim m_core_data_lock = new SemaphoreSlim(1);
        private Utils.CancellationLock m_load_image_lock = new Utils.CancellationLock();

        public ReaderPage()
        {
            Shared = new ReaderPageShared();
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicDir = "";
            Shared.ComicTags = new ObservableCollection<TagsModel>();
            Shared.IsEditable = false;
            Shared.BottomTilePinned = false;
            Shared.BottomTilePinnedChanged = OnBottomTilePinnedChanged;

            VerticalReader = new ReaderModel(Shared, VerticalReaderScrollViewer, VerticalReaderListView, true);
            HorizontalReader = new ReaderModel(Shared, HorizontalReaderScrollViewer, HorizontalReaderListView, false);
            PreviewDataSource = new ObservableCollection<ReaderFrameModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnTabUpdate = OnTabUpdate;
            m_tab_manager.OnTabRegister = OnTabRegister;
            m_tab_manager.OnTabUnregister = OnTabUnregister;
            m_tab_manager.OnTabStart = OnTabStart;
            Unloaded += m_tab_manager.OnTabUnloaded;

            m_comic = null;
            m_reader_position = 0.0;

            m_gesture_recognizer = new GestureRecognizer();
            m_gesture_recognizer.GestureSettings =
                GestureSettings.ManipulationTranslateX |
                GestureSettings.ManipulationTranslateY;
            m_gesture_recognizer.ManipulationStarted += OnManipulationStarted;
            m_gesture_recognizer.ManipulationUpdated += OnManipulationUpdated;
            m_gesture_recognizer.ManipulationCompleted += OnManipulationCompleted;

            m_bottom_tile_showed = false;
            m_bottom_tile_hold = false;
            m_bottom_tile_pointer_in = true;
            m_bottom_tile_exit_requests = 0;
            
            InitializeComponent();
        }

        // navigation
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

            Shared.NavigationPageShared.OnSwitchFavorites += OnSwitchFavorites;
            Shared.NavigationPageShared.MainPageShared.OnExitFullscreenMode += BottomGridForceHide;
            Shared.NavigationPageShared.OnZoomIn += ZoomIn;
            Shared.NavigationPageShared.OnZoomOut += ZoomOut;
            Shared.NavigationPageShared.OnSwitchReaderOrientation += SwitchReaderOrientation;
            Shared.NavigationPageShared.OnPreviewModeChanged += Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane += ExpandInfoPane;

            Shared.NavigationPageShared.IsPreviewModeEnabled = false;
            Shared.UpdateReaderUI();
        }

        private void OnTabUnregister()
        {
            Shared.NavigationPageShared.OnSwitchFavorites -= OnSwitchFavorites;
            Shared.NavigationPageShared.MainPageShared.OnExitFullscreenMode -= BottomGridForceHide;
            Shared.NavigationPageShared.OnZoomIn -= ZoomIn;
            Shared.NavigationPageShared.OnZoomOut -= ZoomOut;
            Shared.NavigationPageShared.OnSwitchReaderOrientation -= SwitchReaderOrientation;
            Shared.NavigationPageShared.OnPreviewModeChanged -= Shared.UpdateReaderUI;
            Shared.NavigationPageShared.OnExpandComicInfoPane -= ExpandInfoPane;
        }

        private void OnTabUpdate()
        {
            Shared.ReaderFlowDirection = Database.AppSettings.RightToLeft ?
                FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Shared.BottomTilePinned = false;
        }

        private void OnTabStart(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.Methods.Run(async delegate
            {
                DatabaseContext db = new DatabaseContext();

                Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Reader;
                tab_id.Type = Utils.Tab.PageType.Reader;

                if (m_comic != tab_id.RequestArgs)
                {
                    ComicData comic = (ComicData)tab_id.RequestArgs;
                    tab_id.Tab.Header = comic.Title1;
                    tab_id.Tab.IconSource = new muxc.SymbolIconSource { Symbol = Symbol.Document };
                    await LoadComic(db, comic);
                }
            });
        }

        public static string PageUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Directory;
        }

        // utilities
        private ReaderModel GetCurrentReader()
        {
            if (!Shared.IsReaderVertical.HasValue)
            {
                return null;
            }

            if (Shared.IsReaderVertical.Value)
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
                image_count = m_comic.ImageFiles.Count.ToString();
            }

            m_reader_position = control.PageReal;
            PageIndicator.Text = control.Page.ToString() + " of " + image_count;
        }

        private async Task UpdateProgress(DatabaseContext db, ReaderModel control)
        {
            await m_core_data_lock.WaitAsync(); // Data protection on.

            int progress;

            if (m_comic.ImageFiles.Count == 0)
            {
                progress = 0;
            }
            else if (control.Page == m_comic.ImageFiles.Count)
            {
                progress = 100;
            }
            else
            {
                progress = (int)((float)control.Page / control.Pages * 100);
            }

            if (progress > 100)
            {
                progress = 100;
            }

            Shared.Progress = progress.ToString() + "%";
            m_comic.Progress = progress;
            m_comic.LastPosition = control.PageReal;
            await m_comic.SaveBasic(db);

            m_core_data_lock.Release(); // Data protection off.
        }

        // loading
        private async Task LoadComic(DatabaseContext db, ComicData comic)
        {
            // Load the comic.
            if (comic == null)
            {
                return;
            }

            await m_core_data_lock.WaitAsync(); // Data protection on.

            m_comic = comic;
            m_load_image_queue = Utils.TaskQueue.TaskQueueManager.EmptyQueue();

            VerticalReader.Comic = m_comic;
            HorizontalReader.Comic = m_comic;
            VerticalReader.Reset();
            HorizontalReader.Reset();

            await SetActiveReader(db, VerticalReader);
            ReaderModel reader = GetCurrentReader();
            System.Diagnostics.Debug.Assert(reader != null);

            // Additional procedures for comics in the library.
            if (!m_comic.IsExternal)
            {
                // Add to history
                await HistoryDataManager.Add(m_comic.Id, m_comic.Title1, true);

                // Update "last visit".
                m_comic.LastVisit = DateTimeOffset.Now;
                await m_comic.SaveBasic(db);

                // Update image files.
                await ComicDataManager.UpdateImages(db, m_comic);

                // Set initial page.
                reader.SetInitialPage(m_comic.LastPosition);

                // Call UpdateDataSource() to load frames into readers.
                for (int i = 0; i < m_comic.ImageAspectRatios.Count; ++i)
                {
                    double image_aspect_ratio = m_comic.ImageAspectRatios[i];
                    VerticalReader.UpdateDataSource(i, image_aspect_ratio);
                    HorizontalReader.UpdateDataSource(i, image_aspect_ratio);
                }
            }

            m_core_data_lock.Release(); // Data protection off.

            Utils.TaskQueue.TaskQueueManager.AppendTask(delegate (RawTask _t)
            {
                // Stop loading if failed to retrieve image folder.
                if (!_t.Result.Successful)
                {
                    return _t.Result;
                }

                return LoadImagesAsync(db).Result;
            }, "", m_load_image_queue);

            await LoadComicInfo();
        }

        private async RawTask LoadImagesAsync(DatabaseContext db)
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

            List<ImageLoaderToken> preview_img_loader_tokens = new List<ImageLoaderToken>();
            Utils.Stopwatch save_timer = new Utils.Stopwatch();
            save_timer.Start();
            ComicData comic = m_comic; // Stores locally.

            for (int i = 0; i < comic.ImageFiles.Count; ++i)
            {
                int index = i; // Stores locally.
                int page = i + 1; // Stores locally.

                preview_img_loader_tokens.Add(new ImageLoaderToken
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
                        if (save_timer.LapSpan().TotalSeconds > 5.0 || index == comic.ImageFiles.Count - 1)
                        {
                            await comic.SaveImageAspectRatios(db);
                            save_timer.Lap();
                        }

                        // Update previews.
                        PreviewDataSource.Add(new ReaderFrameModel
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

            Task preview_loader_task = ComicDataManager.LoadImages(db, 
                preview_img_loader_tokens, preview_width, preview_height,
                m_load_image_lock);
            await preview_loader_task.AsAsyncAction();

            m_load_image_lock.Release();
            return new TaskResult();
        }

        private async Task LoadComicInfo()
        {
            System.Diagnostics.Debug.Assert(m_comic != null);

            Shared.NavigationPageShared.NotExternal = !m_comic.IsExternal;
            Shared.ComicTitle1 = m_comic.Title1;
            Shared.ComicTitle2 = m_comic.Title2;
            Shared.ComicDir = m_comic.Directory;
            Shared.IsEditable = !(m_comic.IsExternal && m_comic.InfoFile == null);
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

            ObservableCollection<TagsModel> new_collection = new ObservableCollection<TagsModel>();

            for (int i = 0; i < m_comic.Tags.Count; ++i)
            {
                TagData tags = m_comic.Tags[i];
                TagsModel tags_model = new TagsModel(tags.Name);

                foreach (string tag in tags.Tags)
                {
                    TagModel tag_model = new TagModel
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

        // reader
        private async Task<bool> SetActiveReader(DatabaseContext db, ReaderModel reader)
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
            System.Diagnostics.Debug.Assert(last_reader == null || !last_reader.IsActive);
            System.Diagnostics.Debug.Assert(this_reader.IsActive);
            
            if (last_reader != null)
            {
                int zoom = Math.Min(100, last_reader.Zoom);
                double position = m_reader_position;

                Shared.UpdateReaderUI();
                await last_reader.OnScrollViewerViewChanged(db, false);

                await Utils.Methods.WaitFor(() => this_reader.IsLoaded);
                this_reader.SetScrollViewer(zoom, position, use_page_center: false, true);
            }
            else
            {
                Shared.UpdateReaderUI();
            }

            return true;
        }

        public void SwitchReaderOrientation()
        {
            Utils.Methods.Run(async delegate
            {
                DatabaseContext db = new DatabaseContext();

                if (!Shared.IsReaderVertical.HasValue)
                {
                    return;
                }

                ReaderModel reader = null;

                if (Shared.IsReaderVertical.Value)
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
            Utils.Methods.Run(async delegate
            {
                DatabaseContext db = new DatabaseContext();

                await control.OnScrollViewerViewChanged(db, e.IsIntermediate);

                if (control.IsLoaded || !e.IsIntermediate)
                {
                    UpdatePage(control);
                    await UpdateProgress(db, control);
                    BottomTileSetHold(false);
                }
            });
        }

        private void OnHorizontalReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
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
            control.IncreasePage(delta, false);

            // Set e.Handled to true to suppress the default behavior of scroll viewer (which will override ours)
            e.Handled = true;
        }

        private void OnVerticalReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            OnReaderScrollViewerViewChanged(VerticalReader, e);
        }

        private void OnHorizontalReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            OnReaderScrollViewerViewChanged(HorizontalReader, e);
        }

        private void OnVerticalReaderScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            VerticalReader.ThisScrollViewer = VerticalReaderScrollViewer;
        }

        private void OnVerticalReaderListViewLoaded(object sender, RoutedEventArgs e)
        {
            VerticalReader.ThisListView = VerticalReaderListView;
        }

        private void OnHorizontalReaderScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            HorizontalReader.ThisScrollViewer = HorizontalReaderScrollViewer;
        }

        private void OnHorizontalReaderListViewLoaded(object sender, RoutedEventArgs e)
        {
            HorizontalReader.ThisListView = HorizontalReaderListView;
        }

        // preview
        private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderFrameModel ctx = (ReaderFrameModel)e.ClickedItem;

                Shared.NavigationPageShared.IsPreviewModeEnabled = false;

                ReaderModel reader = GetCurrentReader();

                if (reader == null)
                {
                    return;
                }

                await Utils.Methods.WaitFor(() => reader.IsLoaded);
                reader.SetScrollViewer(null, ctx.Page, use_page_center: true, true);
            });
        }
        
        // manipulating
        private void OnReaderScrollViewerPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            (sender as UIElement).CapturePointer(e.Pointer);
            m_gesture_recognizer.ProcessDownEvent(e.GetCurrentPoint(ManipulationReference));
        }

        private void OnReaderScrollViewerPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));
        }

        private void OnReaderScrollViewerPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessUpEvent(e.GetCurrentPoint(ManipulationReference));
            (sender as UIElement).ReleasePointerCapture(e.Pointer);
        }

        void OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            // do nothing.
        }

        void OnManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            ReaderModel reader = GetCurrentReader();

            if (reader == null)
            {
                return;
            }

            double dx = e.Delta.Translation.X;
            double dy = e.Delta.Translation.Y;

            if (reader.IsHorizontal && Shared.ReaderFlowDirection == FlowDirection.RightToLeft)
            {
                dx = -dx;
            }

            reader.SetScrollViewer(null, reader.HorizontalOffsetFinal - dx, reader.VerticalOffsetFinal - dy, false);
        }

        void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
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
                reader.IncreasePage(-1, false);
            }
            else if (velocity < -1.0)
            {
                reader.IncreasePage(1, false);
            }
        }

        // zooming
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

        // favorites
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
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(!Shared.NavigationPageShared.IsFavorite);
            });
        }

        private void OnFavoriteBtChecked(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(true);
            });
        }

        private void OnFavoriteBtUnchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(false);
            });
        }

        private void OnFavoritesBtClicked(object sender, RoutedEventArgs e)
        {
            OnSwitchFavorites();
        }

        // info pane
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
            Utils.Methods.Run(async delegate
            {
                DatabaseContext db = new DatabaseContext();
                m_comic.Rating = (int)sender.Value;
                await m_comic.SaveBasic(db);
            });
        }

        private void OnInfoPaneTagClicked(object sender, RoutedEventArgs e)
        {
            TagModel ctx = (TagModel)((Button)sender).DataContext;
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Search, "<tag:" + ctx.Tag + ">");
        }

        private void OnEditBtClicked(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
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

        // fullscreen
        private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.EnterFullscreen();
        }

        private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.ExitFullscreen();
            Shared.BottomTilePinned = false;
        }

        // bottom tile
        private void BottomTileShow()
        {
            if (m_bottom_tile_showed || !Shared.NavigationPageShared.MainPageShared.IsFullscreen)
            {
                return;
            }

            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 1.0);
            BottomGridStoryboard.Begin();
            m_bottom_tile_showed = true;
        }

        private void BottomTileHide()
        {
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
                BottomTileHide();
            }
        }

        private void OnBottomTilePinnedChanged()
        {
            if (Shared.BottomTilePinned)
            {
                BottomTileShow();
            }
        }

        private void OnBottomTilePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            m_bottom_tile_pointer_in = true;
            BottomTileShow();
        }

        private void OnBottomTilePointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!m_bottom_tile_showed || m_bottom_tile_hold)
            {
                return;
            }

            m_bottom_tile_pointer_in = false;

            _ = Task.Run(() =>
            {
                _ = Interlocked.Increment(ref m_bottom_tile_exit_requests);
                Task.Delay(1000).Wait();
                int r = Interlocked.Decrement(ref m_bottom_tile_exit_requests);

                if (!m_bottom_tile_showed || m_bottom_tile_pointer_in || r != 0)
                {
                    return;
                }

                _ = Utils.Methods.Sync(delegate
                {
                    BottomTileHide();
                });
            });
        }

        private void OnReaderTapped(object sender, TappedRoutedEventArgs e)
        {
            BottomTileSetHold(!m_bottom_tile_showed);
        }

        private void OnSwitchReaderOrientationClicked(object sender, RoutedEventArgs e)
        {
            SwitchReaderOrientation();
        }
    }
}