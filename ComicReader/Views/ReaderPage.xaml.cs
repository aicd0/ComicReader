//#define DEBUG_LOG_READER_CONTROL_ACTIVITY
//#define DEBUG_LOG_VIEW_CHANGE

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
        private bool m_IsOnePageReaderVisible;
        public bool IsOnePageReaderVisible
        {
            get => m_IsOnePageReaderVisible;
            set
            {
                m_IsOnePageReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsOnePageReaderVisible"));
            }
        }

        private bool m_IsTwoPagesReaderVisible;
        public bool IsTwoPagesReaderVisible
        {
            get => m_IsTwoPagesReaderVisible;
            set
            {
                m_IsTwoPagesReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTwoPagesReaderVisible"));
            }
        }

        private bool m_IsGridViewVisible;
        public bool IsGridViewVisible
        {
            get => m_IsGridViewVisible;
            set
            {
                m_IsGridViewVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGridViewVisible"));
            }
        }

        public void UpdateReaderVisibility()
        {
            IsGridViewVisible = NavigationPageShared.PreviewMode;
            IsOnePageReaderVisible = !NavigationPageShared.PreviewMode && !NavigationPageShared.TwoPagesMode;
            IsTwoPagesReaderVisible = !NavigationPageShared.PreviewMode && NavigationPageShared.TwoPagesMode;
        }

        private bool m_BottomGridPinned;
        public bool BottomGridPinned
        {
            get => m_BottomGridPinned;
            set
            {
                m_BottomGridPinned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BottomGridPinned"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PinButtonToolTip"));
                BottomGridPinnedChanged?.Invoke();
            }
        }

        public string PinButtonToolTip
        {
            get
            {
                return BottomGridPinned ? "Unpin" : "Pin";
            }
        }

        public Action BottomGridPinnedChanged;
    }

    public class ReaderControl
    {
        private const int max_zoom = 250;
        private const int min_zoom = 90;

        public ReaderControl(ReaderPageShared shared, ScrollViewer scroll_viewer, ListView list_view, bool vertical)
        {
            m_is_vertical = vertical;

            Shared = shared;
            ThisScrollViewer = scroll_viewer;
            ThisListView = list_view;

            DisplayInformation display_info = DisplayInformation.GetForCurrentView();
            LastViewportPerpendicularLength = display_info.ScreenWidthInRawPixels / display_info.RawPixelsPerViewPixel;
        }

        private bool m_is_vertical;
        private int m_zoom = 90;
        private int m_page = -1;
        private bool m_final_value_set = false;
        private bool m_calc_using_ratio_method;
        private double m_margin_start_final;
        private double m_margin_end_final;
        private double m_parallel_ratio_final;
        private double m_horizontal_offset_final;
        private double m_vertical_offset_final;
        private float m_zoom_factor_final;
        private bool m_disable_animation_final;
        private Utils.CancellationLock m_update_image_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_source_changed_lock = new Utils.CancellationLock();

        public ReaderPageShared Shared;
        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;
        public double LastViewportPerpendicularLength;

        // TRUE if scroll viewer and list view were loaded
        public bool IsLayoutReady =>
            ThisScrollViewer != null && ThisListView != null && ImageSource.Count > 0;

        // TRUE if IsLayoutReady is true, and ScrollViewer were initialized
        public bool IsScrollViewerInitialized { get; set; } = false;

        // TRUE if IsScrollViewerInitialized is true, and all containers were loaded
        public bool IsAllImagesLoaded { get; set; } = false;

        public ComicItemData Comic { get; set; }
        public ObservableCollection<ReaderFrameModel> ImageSource { get; set; } = new ObservableCollection<ReaderFrameModel>();
        public int Pages => Comic.ImageFiles.Count;
        public int Page => m_page;
        public int Zoom => m_zoom;
        public bool IsVertical => m_is_vertical;
        public bool IsActive { get; set; }
        
        private void _CompleteFinalValue()
        {
            if (!IsLayoutReady)
            {
                return;
            }

            if (m_final_value_set)
            {
                return;
            }

            m_margin_start_final = m_is_vertical ? ThisListView.Margin.Top : ThisListView.Margin.Left;
            m_margin_end_final = m_is_vertical ? ThisListView.Margin.Bottom : ThisListView.Margin.Right;
            double margin_start = m_margin_start_final * ZoomFactor;
            double margin_end = m_margin_end_final * ZoomFactor;

            m_parallel_ratio_final = (ParallelOffset + ViewportParallelLength * 0.5 - margin_start)
                / (ExtentParallelLength - margin_start - margin_end);
            m_horizontal_offset_final = HorizontalOffset;
            m_vertical_offset_final = VerticalOffset;
            m_zoom_factor_final = ZoomFactor;
            m_disable_animation_final = false;
            m_final_value_set = true;
        }

        // scroll viewer
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;

        public float ZoomFactorFinal
        {
            get
            {
                _CompleteFinalValue();
                return m_zoom_factor_final;
            }
            set
            {
                m_zoom_factor_final = value;
            }
        }

        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;

        public double HorizontalOffsetFinal
        {
            get
            {
                if (!m_calc_using_ratio_method || m_is_vertical)
                {
                    _CompleteFinalValue();
                    return m_horizontal_offset_final;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                _CompleteFinalValue();
                m_horizontal_offset_final = value;
                m_calc_using_ratio_method = false;
            }
        }

        public double VerticalOffset => ThisScrollViewer.VerticalOffset;

        public double VerticalOffsetFinal
        {
            get
            {
                if (!m_calc_using_ratio_method || !m_is_vertical)
                {
                    _CompleteFinalValue();
                    return m_vertical_offset_final;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                _CompleteFinalValue();
                m_vertical_offset_final = value;
                m_calc_using_ratio_method = false;
            }
        }

        public double ParallelRatioFinal
        {
            get
            {
                if (m_calc_using_ratio_method)
                {
                    _CompleteFinalValue();
                    return m_parallel_ratio_final;
                }
                else
                {
                    return (ParallelOffsetFinal + ViewportParallelLength * 0.5 - MarginStartFinal)
                        / (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal);
                }
            }
            set
            {
                _CompleteFinalValue();
                m_parallel_ratio_final = value;
                m_calc_using_ratio_method = true;
            }
        }

        public double ParallelOffset => m_is_vertical ? VerticalOffset : HorizontalOffset;

        public double ParallelOffsetFinal
        {
            get
            {
                if (m_calc_using_ratio_method)
                {
                    return ParallelRatioFinal * (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal)
                        + MarginStartFinal - ViewportParallelLength * 0.5;
                }
                else
                {
                    return m_is_vertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
                }
            }
        }

        public double PerpendicularOffset => m_is_vertical ? ThisScrollViewer.HorizontalOffset : ThisScrollViewer.VerticalOffset;
        public double ViewportParallelLength => m_is_vertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => m_is_vertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => m_is_vertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
        public double FrameParallelLength(int i) => m_is_vertical ? ImageSource[i].Height : ImageSource[i].Width;

        public double ZoomCoefficient()
        {
            double viewport_width = ThisScrollViewer.ViewportWidth;
            double viewport_height = ThisScrollViewer.ViewportHeight;
            double image_width = ImageSource[0].ImageWidth;
            double image_height = ImageSource[0].ImageHeight;

            if (!m_is_vertical)
            {
                // for two-pages reader, double the image width to approximate the width of two pages
                image_width *= 2.0;
            }

            double viewport_ratio = viewport_width / viewport_height;
            double image_ratio = image_width / image_height;
            return 0.01 * (viewport_ratio > image_ratio ?
                viewport_height / image_height :
                viewport_width / image_width);
        }

        public double? PageOffset(int page, bool center)
        {
            page = Math.Max(1, page);
            page = Math.Min(Pages, page);
            int page_idx = page - 1;

            if (page_idx >= ImageSource.Count)
            {
                return null;
            }

            ReaderFrameModel item = ImageSource[page_idx];
            Grid page_container = item.Container;

            if (page_container == null)
            {
                return null;
            }

            var page_transform = page_container.TransformToVisual(ThisScrollViewer);
            Point page_position = page_transform.TransformPoint(new Point(0.0, 0.0));
            double parallel_offset = ParallelOffset;
            parallel_offset += IsVertical ? page_position.Y : page_position.X;
            parallel_offset /= ZoomFactor;

            if (center)
            {
                parallel_offset += IsVertical ?
                    item.Margin.Top + item.ImageHeight * 0.5 :
                    item.Margin.Left + item.ImageWidth * 0.5;
            }

            return parallel_offset;
        }

        public double? PageOffsetTransformed(double page, bool center)
        {
            page = Math.Max(1.0, page);
            page = Math.Min(Pages, page);

            int page_int = (int)page;
            double page_dec = page - page_int;
            double? parallel_offset = PageOffset(page_int, center);

            if (!parallel_offset.HasValue)
            {
                return null;
            }

            double? next_offset = PageOffset(page_int + 1, center);

            if (next_offset.HasValue)
            {
                parallel_offset += (next_offset.Value - parallel_offset) * page_dec;
            }

            return parallel_offset * ZoomFactorFinal - ViewportParallelLength * 0.5;
        }

        public void IncreasePage(int increment, bool disable_animation)
        {
            int new_page_int = Page + increment * (IsVertical ? 1 : 2);
            double new_page = new_page_int;

            if (!IsVertical && new_page_int > 1)
            {
                // stick to the center of two pages
                new_page = new_page_int + (new_page_int % 2 == 0 ? 0.5 : -0.5);
            }

            double? new_parallel_offset = PageOffsetTransformed(new_page, center: true);

            if (new_parallel_offset == null || Math.Abs(new_parallel_offset.Value - ParallelOffsetFinal) < 1.0)
            {
                // IMPORTANT: Ignore the request if target offset is really close to the current offset,
                // or else it could stuck in a dead loop. (See reference in OnScrollViewerViewChanged())
                return;
            }

            SetScrollViewer(new_page, disable_animation);
            m_page = new_page_int;
        }

        public void SetScrollViewer(int? zoom, bool disable_animation, double? horizontal_offset, double? vertical_offset)
        {
            _SetScrollViewer(zoom, disable_animation, null, null, horizontal_offset, vertical_offset);
        }

        public void SetScrollViewer(double? page, bool disable_animation)
        {
            _SetScrollViewer(null, disable_animation, page, null, null, null);
        }

        public void SetScrollViewer(int? zoom, bool disable_animation, double parallel_ratio)
        {
            _SetScrollViewer(zoom, disable_animation, null, parallel_ratio, null, null);
        }

        private void _SetScrollViewer(int? zoom, bool disable_animation, double? page, double? parallel_ratio, double? horizontal_offset, double? vertical_offset)
        {
            if (!IsLayoutReady)
            {
                return;
            }

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("ParamIn:"
                + " Z=" + zoom.ToString()
                + ",D=" + disable_animation.ToString()
                + ",Pg=" + page.ToString()
                + ",P=" + parallel_ratio.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + "\n");
#endif

            _SetScrollViewerPage(page, ref horizontal_offset, ref vertical_offset);
            _SetScrollViewerZoom(ref horizontal_offset, ref vertical_offset, ref zoom, out float? zoom_out);

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("ParamOut:"
                + " H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + ",Z=" + zoom.ToString()
                + ",Zo=" + zoom_out.ToString()
                + ",D=" + disable_animation.ToString()
                + "\n");
#endif

            if (parallel_ratio != null)
            {
                _ChangeView(zoom_out, (double)parallel_ratio, disable_animation);
            }
            else
            {
                _ChangeView(zoom_out, horizontal_offset, vertical_offset, disable_animation);
            }

            m_zoom = (int)zoom;
            Shared.NavigationPageShared.ZoomInEnabled = m_zoom < max_zoom;
            Shared.NavigationPageShared.ZoomOutEnabled = m_zoom > min_zoom;
            _FixParallelOffset();
        }

        private void _SetScrollViewerZoom(ref double? horizontal_offset, ref double? vertical_offset, ref int? zoom, out float? zoom_out)
        {
            if (ImageSource.Count == 0)
            {
                zoom = m_zoom;
                zoom_out = null;
                return;
            }

            double zoom_coefficient = ZoomCoefficient();
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

        private void _SetScrollViewerPage(double? page, ref double? horizontal_offset, ref double? vertical_offset)
        {
            if (page == null)
            {
                return;
            }

            double? parallel_offset = PageOffsetTransformed(page.Value, center: true);

            if (parallel_offset == null)
            {
                return;
            }

            ConvertOffset(ref horizontal_offset, ref vertical_offset, parallel_offset, null);
        }

        private bool _FixParallelOffset()
        {
            if (ImageSource.Count == 0)
            {
                return false;
            }

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("FixOffset\n");
#endif

            if (ImageSource[0].Container == null)
            {
                return false;
            }

            double space = MarginStartFinal - ParallelOffsetFinal;
            double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;
            double image_center_offset = MarginStartFinal
                + FrameParallelLength(0) * 0.5 * ZoomFactorFinal;
            double image_center_to_screen_center = image_center_offset - screen_center_offset;
            double movement_forward = Math.Min(space, image_center_to_screen_center);

            if (movement_forward > 0)
            {
                double parallel_offset = ParallelOffsetFinal + movement_forward;
                return _ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }

            if (ImageSource[ImageSource.Count - 1].Container == null)
            {
                return false;
            }

            space = MarginEndFinal - (ExtentParallelLengthFinal
                - ParallelOffsetFinal - ViewportParallelLength);
            image_center_offset = ExtentParallelLengthFinal - MarginEndFinal
                - FrameParallelLength(ImageSource.Count - 1) * 0.5 * ZoomFactorFinal;
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
            if (!IsLayoutReady)
            {
                return false;
            }

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

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_disable_animation_final);
#endif

            bool res = ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_disable_animation_final);

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print(" (R=" + res.ToString() + ")\n");
#endif

            return true;
        }

        private bool _ChangeView(float? zoom_factor, double parallel_ratio, bool disable_animation)
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            ParallelRatioFinal = parallel_ratio;

            if (zoom_factor != null)
            {
                ZoomFactorFinal = zoom_factor.Value;
            }

            if (disable_animation)
            {
                m_disable_animation_final = true;
            }

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("Commit:"
                + " P=" + ParallelRatioFinal.ToString()
                + ",Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_disable_animation_final);
#endif

            bool res = ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_disable_animation_final);

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print(" (R=" + res.ToString() + ")\n");
#endif

            return true;
        }

        // list view
        public double MarginStartFinal
        {
            get
            {
                _CompleteFinalValue();
                return m_margin_start_final * ZoomFactorFinal;
            }
        }

        public double MarginEndFinal
        {
            get
            {
                _CompleteFinalValue();
                return m_margin_end_final * ZoomFactorFinal;
            }
        }

        public void UpdateMargin()
        {
            if (!IsLayoutReady || ImageSource.Count == 0)
            {
                return;
            }

            if (ImageSource[0].Container == null || ImageSource[ImageSource.Count - 1].Container == null)
            {
                return;
            }

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("UpdateMargin\n");
#endif

            double zoom_coefficient = ZoomCoefficient();
            double zoom_factor = min_zoom * zoom_coefficient;
            double inner_length = ViewportParallelLength / zoom_factor;
            double new_start = (inner_length - FrameParallelLength(0)) / 2;
            double new_end = (inner_length - FrameParallelLength(ImageSource.Count - 1)) / 2;

            new_start = Math.Max(0.0, new_start);
            new_end = Math.Max(0.0, new_end);

            _SetMargin(new_start, new_end);
        }

        private void _SetMargin(double start, double end)
        {
            _CompleteFinalValue();
            m_margin_start_final = start;
            m_margin_end_final = end;

            if (m_is_vertical)
            {
                ThisListView.Margin = new Thickness(0.0, start, 0.0, end);
            }
            else
            {
                ThisListView.Margin = new Thickness(start, 0.0, end, 0.0);
            }
        }

        // conversions
        public void ConvertOffset(ref double? to_horizontal, ref double? to_vertical, double? from_parallel, double? from_perpendicular)
        {
            if (m_is_vertical)
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

        public double? HorizontalVal(double? parallel_val, double? perpendicular_val) => m_is_vertical ? perpendicular_val : parallel_val;
        public double? VerticalVal(double? parallel_val, double? perpendicular_val) => m_is_vertical ? parallel_val : perpendicular_val;
        private double FinalVal(double val) => val / ZoomFactor * ZoomFactorFinal;

        // events
        public void OnContainerLoaded(ReaderFrameModel ctx)
        {
            Utils.Methods.Run(async delegate
            {
                try
                {
                    await m_source_changed_lock.WaitAsync();

                    if (!IsLayoutReady)
                    {
                        await Utils.Methods.WaitFor(() => IsLayoutReady);
                    }

                    if (!IsScrollViewerInitialized)
                    {
                        await Utils.Methods.Sync(delegate
                        {
                            // set initial zoom
                            SetScrollViewer(m_zoom, true, null, null);
                            UpdateMargin();
                            IsScrollViewerInitialized = true;
                        });
                    }

                    if (!IsAllImagesLoaded && ImageSource.Count == Pages)
                    {
                        bool all_img_loaded = true;

                        foreach (ReaderFrameModel item in ImageSource)
                        {
                            if (item.Container == null)
                            {
                                all_img_loaded = false;
                                break;
                            }
                        }

                        if (all_img_loaded)
                        {
                            await Utils.Methods.Sync(delegate
                            {
                                IsAllImagesLoaded = true;
                            });
                        }
                    }
                }
                finally
                {
                    m_source_changed_lock.Release();
                }
            });
        }

        // others
        public void Clear()
        {
            ImageSource.Clear();
        }

        public void FinalValueExpired()
        {
            m_final_value_set = false;
        }

        public bool UpdatePage()
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            double current_offset = (ParallelOffsetFinal - MarginStartFinal
                + ViewportParallelLength * 0.5) / ZoomFactorFinal;

            // Use binary search to locate the current page.
            if (ImageSource.Count == 0)
            {
                return false;
            }

            int begin = 1;
            int end = ImageSource.Count + 1;

            while (begin < end)
            {
                int p = (begin + end) / 2;
                double? page_offset = PageOffset(p, center: false);

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

            m_page = page;
            return true;
        }

        public void UpdateImages()
        {
            Utils.Methods.Run(async delegate
            {
                int page_begin = Math.Max(Page - 5, 1);
                int page_end = Math.Min(Page + 10, Pages);
                await m_update_image_lock.WaitAsync();

                try
                {
                    List<ImageLoaderToken> img_loader_tokens = new List<ImageLoaderToken>();

                    if (!IsActive)
                    {
                        foreach (ReaderFrameModel m in ImageSource)
                        {
                            m.Image = null;
                        }
                        return;
                    }

                    for (int i = 0; i < ImageSource.Count; ++i)
                    {
                        ReaderFrameModel m = ImageSource[i];

                        if (m.Page < page_begin || page_end < m.Page)
                        {
                            m.Image = null;
                            continue;
                        }

                        if (m.Image != null)
                        {
                            continue;
                        }

                        img_loader_tokens.Add(new ImageLoaderToken
                        {
                            Index = i,
                            Comic = Comic,
                            Callback = (BitmapImage img) =>
                            {
                                m.Image = img;
                            }
                        });
                    }

                    await ComicDataManager.LoadImages(img_loader_tokens, double.PositiveInfinity, double.PositiveInfinity, m_update_image_lock);
                }
                finally
                {
                    m_update_image_lock.Release();
                }
            });
        }
    }

    public sealed partial class ReaderPage : Page
    {
        public static ReaderPage Current;
        public ReaderPageShared Shared { get; set; }
        private ReaderControl OnePageReader { get; set; }
        private ReaderControl TwoPagesReader { get; set; }
        private ObservableCollection<ReaderFrameModel> GridViewDataSource { get; set; }

        private Utils.Tab.TabManager m_tab_manager;
        private ComicItemData m_comic;
        private RecentReadItemData m_comic_record;
        private Utils.TaskQueue.TaskQueue m_load_image_queue = Utils.TaskQueue.TaskQueueManager.EmptyQueue();
        private double m_reader_position;
        private GestureRecognizer m_gesture_recognizer;

        // bottom tile
        private bool m_bottom_tile_showed;
        private bool m_bottom_tile_hold;
        private bool m_bottom_tile_pointer_in;
        private int m_bottom_tile_exit_requests;

        // lock
        private Utils.CancellationLock m_reader_h_img_loader_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_reader_v_img_loader_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_preview_img_loader_lock = new Utils.CancellationLock();

        public ReaderPage()
        {
            Current = this;
            Shared = new ReaderPageShared();
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicDir = "";
            Shared.ComicTags = new ObservableCollection<TagsModel>();
            Shared.IsEditable = false;
            Shared.BottomGridPinned = false;
            Shared.BottomGridPinnedChanged = OnBottomGridPinnedChanged;
            OnePageReader = new ReaderControl(Shared, OnePageVerticalScrollViewer, OnePageImageListView, true);
            TwoPagesReader = new ReaderControl(Shared, TwoPagesHorizontalScrollViewer, TwoPagesImageListView, false);
            GridViewDataSource = new ObservableCollection<ReaderFrameModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnRegister = OnRegister;
            m_tab_manager.OnUnregister = OnUnregister;
            m_tab_manager.OnUpdate = OnUpdate;
            Unloaded += m_tab_manager.OnUnloaded;

            m_comic = null;
            m_comic_record = null;
            m_reader_position = -1.0;

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

        private void OnRegister(object shared)
        {
            Shared.NavigationPageShared = (NavigationPageShared)shared;

            Shared.NavigationPageShared.OnSwitchFavorites += OnSwitchFavorites;
            Shared.NavigationPageShared.MainPageShared.OnExitFullscreenMode += BottomGridForceHide;
            Shared.NavigationPageShared.OnZoomIn += OnZoomIn;
            Shared.NavigationPageShared.OnZoomOut += OnZoomOut;
            Shared.NavigationPageShared.OnTwoPagesModeChanged += OnTwoPagesModeChanged;
            Shared.NavigationPageShared.OnTwoPagesModeChanged += Shared.UpdateReaderVisibility;
            Shared.NavigationPageShared.OnGridViewModeChanged += Shared.UpdateReaderVisibility;
            Shared.NavigationPageShared.PreviewMode = false;
            Shared.NavigationPageShared.TwoPagesMode = false;
        }

        private void OnUnregister()
        {
            Shared.NavigationPageShared.OnSwitchFavorites -= OnSwitchFavorites;
            Shared.NavigationPageShared.MainPageShared.OnExitFullscreenMode -= BottomGridForceHide;
            Shared.NavigationPageShared.OnZoomIn -= OnZoomIn;
            Shared.NavigationPageShared.OnZoomOut -= OnZoomOut;
            Shared.NavigationPageShared.OnTwoPagesModeChanged -= OnTwoPagesModeChanged;
            Shared.NavigationPageShared.OnTwoPagesModeChanged -= Shared.UpdateReaderVisibility;
            Shared.NavigationPageShared.OnGridViewModeChanged -= Shared.UpdateReaderVisibility;
        }

        private void OnPageEntered()
        {
            Shared.ReaderFlowDirection = Database.AppSettings.RightToLeft ?
                FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Shared.BottomGridPinned = false;
        }

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.Methods.Run(async delegate
            {
                Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Reader;
                tab_id.Type = Utils.Tab.PageType.Reader;

                if (m_comic != tab_id.RequestArgs)
                {
                    ComicItemData comic = (ComicItemData)tab_id.RequestArgs;
                    tab_id.Tab.Header = comic.Title1;
                    tab_id.Tab.IconSource = new muxc.SymbolIconSource { Symbol = Symbol.Document };
                    await LoadComic(comic);
                }
            });
        }

        public static string PageUniqueString(object args)
        {
            ComicItemData comic = (ComicItemData)args;
            return "Reader/" + comic.Directory;
        }

        // utilities
        private ReaderControl GetCurrentReaderControl()
        {
            if (Shared.NavigationPageShared.TwoPagesMode)
            {
                return TwoPagesReader;
            }
            else
            {
                return OnePageReader;
            }
        }

        private void UpdatePage(ReaderControl control)
        {
            if (!control.UpdatePage())
            {
                return;
            }

            if (PageIndicator == null)
            {
                return;
            }

            string image_count = "?";

            if (m_comic != null)
            {
                image_count = m_comic.ImageFiles.Count.ToString();
            }

            PageIndicator.Text = control.Page.ToString() + " of " + image_count;
        }

        private void UpdateProgress(ReaderControl control)
        {
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

            if (m_comic_record != null)
            {
                m_comic_record.Progress = progress;
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.ReadRecords));
            }
        }

        // loading
        public async Task LoadComic(ComicItemData comic)
        {
            // load comic
            if (comic == null)
            {
                return;
            }

            m_comic = comic;
            OnePageReader.Comic = m_comic;
            TwoPagesReader.Comic = m_comic;

            // additional procedures for internal comics
            if (!m_comic.IsExternal)
            {
                // fetch the read record. create one if not exists.
                m_comic_record = await RecentReadDataManager.FromId(m_comic.Id, create_if_not_exists: true);

                // add to history
                await HistoryDataManager.Add(m_comic.Id, m_comic.Title1, true);

                // update "last visit"
                await DatabaseManager.WaitLock();
                m_comic.LastVisit = DateTimeOffset.Now;
                DatabaseManager.ReleaseLock();
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    DatabaseManager.SaveSealed(DatabaseItem.Comics));
            }

            LoadImages();
            await LoadComicInformation();
        }

        private void LoadImages()
        {
            if (!m_comic.IsExternal)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    ComicDataManager.CompleteImagesSealed(m_comic), "Retriving images...", m_load_image_queue);
            }

            Utils.TaskQueue.TaskQueueManager.AppendTask(delegate (Task<Utils.TaskQueue.TaskResult> _t) {
                Utils.TaskQueue.TaskResult result = _t.Result;

                // stop the loading progress if failed to retrieve image folder
                if (result.ExceptionType != Utils.TaskQueue.TaskException.Success)
                {
                    return result;
                }

                Task<Utils.TaskQueue.TaskResult> task = LoadImagesAsync();
                task.Wait();
                return task.Result;
            }, "Loading images...", m_load_image_queue);
        }

        private async Task<Utils.TaskQueue.TaskResult> LoadImagesAsync()
        {
            double preview_width = 0.0;
            double preview_height = 0.0;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
                preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
                OnePageReader.Clear();
                TwoPagesReader.Clear();
                GridViewDataSource.Clear();
            });

            List<ImageLoaderToken> reader_h_img_loader_tokens = new List<ImageLoaderToken>();
            List<ImageLoaderToken> reader_v_img_loader_tokens = new List<ImageLoaderToken>();
            List<ImageLoaderToken> preview_img_loader_tokens = new List<ImageLoaderToken>();

            for (int i = 0; i < m_comic.ImageFiles.Count; ++i)
            {
                int page = i + 1;
                bool is_last_page = page == m_comic.ImageFiles.Count;

                reader_v_img_loader_tokens.Add(new ImageLoaderToken
                {
                    Comic = m_comic,
                    Index = i,
                    Callback = (BitmapImage img) =>
                    {
                        OnePageReader.ImageSource.Add(new ReaderFrameModel
                        {
                            Page = page,
                            TopPadding = true,
                            BottomPadding = true,
                            LeftPadding = false,
                            RightPadding = false,
                            ImageWidth = 500.0,
                            ImageHeight = 500.0 / img.PixelWidth * img.PixelHeight,
                            OnContainerLoaded = OnePageReader.OnContainerLoaded
                        });
                        OnePageReader.UpdateImages();
                    }
                });

                reader_h_img_loader_tokens.Add(new ImageLoaderToken
                {
                    Comic = m_comic,
                    Index = i,
                    Callback = (BitmapImage img) =>
                    {
                        TwoPagesReader.ImageSource.Add(new ReaderFrameModel
                        {
                            Page = page,
                            TopPadding = false,
                            BottomPadding = false,
                            LeftPadding = page == 1 || page % 2 == 0,
                            RightPadding = is_last_page || page % 2 == 1,
                            ImageWidth = 300.0 / img.PixelHeight * img.PixelWidth,
                            ImageHeight = 300.0,
                            OnContainerLoaded = TwoPagesReader.OnContainerLoaded
                        });
                        TwoPagesReader.UpdateImages();
                    }
                });

                preview_img_loader_tokens.Add(new ImageLoaderToken
                {
                    Comic = m_comic,
                    Index = i,
                    Callback = (BitmapImage img) =>
                    {
                        GridViewDataSource.Add(new ReaderFrameModel
                        {
                            Image = img,
                            Page = page,
                        });
                    }
                });
            }

            Task reader_v_loader_task = ComicDataManager.LoadImages(reader_v_img_loader_tokens,
                1.0, 1.0, m_reader_v_img_loader_lock);
            Task reader_h_loader_task = ComicDataManager.LoadImages(reader_h_img_loader_tokens,
                1.0, 1.0, m_reader_h_img_loader_lock);
            Task preview_loader_task = ComicDataManager.LoadImages(preview_img_loader_tokens,
                preview_width, preview_height, m_preview_img_loader_lock);

            await reader_v_loader_task.AsAsyncAction();
            await reader_h_loader_task.AsAsyncAction();
            await preview_loader_task.AsAsyncAction();
            return new Utils.TaskQueue.TaskResult();
        }

        private async Task LoadComicInformation()
        {
            if (m_comic == null)
            {
                throw new Exception();
            }

            Shared.NavigationPageShared.NotExternal = !m_comic.IsExternal;
            Shared.ComicTitle1 = m_comic.Title1;
            Shared.ComicTitle2 = m_comic.Title2;
            Shared.ComicDir = m_comic.Directory;
            Shared.IsEditable = !(m_comic.IsExternal && m_comic.InfoFile == null);
            Shared.Progress = "";
            LoadComicTag();

            if (!m_comic.IsExternal)
            {
                Shared.NavigationPageShared.IsFavorite = await FavoritesDataManager.FromId(m_comic.Id) != null;
                Shared.Rating = m_comic_record.Rating;
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
        public void OnTwoPagesModeChanged()
        {
            Utils.Methods.Run(async delegate
            {
                int last_zoom = 0;

                if (Shared.NavigationPageShared.TwoPagesMode)
                {
                    OnePageReader.IsActive = false;
                    TwoPagesReader.IsActive = true;
                    last_zoom = OnePageReader.Zoom;
                }
                else
                {
                    OnePageReader.IsActive = true;
                    TwoPagesReader.IsActive = false;
                    last_zoom = TwoPagesReader.Zoom;
                }

                if (m_reader_position < 0)
                {
                    return;
                }

                double position = m_reader_position;

                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsAllImagesLoaded);

                control.UpdateMargin();
                control.SetScrollViewer(Math.Min(last_zoom, 100), true, position);
            });
        }

        private void OnScrollViewerSizeChanged(ReaderControl control, SizeChangedEventArgs e)
        {
            if (!control.IsAllImagesLoaded)
            {
                return;
            }

#if DEBUG_LOG_READER_CONTROL_ACTIVITY
            System.Diagnostics.Debug.Print("SizeChanged\n");
#endif

            control.UpdateMargin();
            int zoom = (int)(control.Zoom / control.ViewportPerpendicularLength * control.LastViewportPerpendicularLength);

            if (zoom > 100)
            {
                zoom = 100;
            }

            control.SetScrollViewer(zoom, true, m_reader_position);
            control.LastViewportPerpendicularLength = control.ViewportPerpendicularLength;
        }

        private void OnScrollViewerViewChanged(ReaderControl control, ScrollViewerViewChangedEventArgs e)
        {
            if (!control.IsScrollViewerInitialized || e.IsIntermediate)
            {
                return;
            }

            control.FinalValueExpired();

            if (control.IsAllImagesLoaded)
            {
                m_reader_position = control.ParallelRatioFinal;
            }

#if DEBUG_LOG_VIEW_CHANGE
            System.Diagnostics.Debug.Print("ViewChanged:"
                + " H=" + control.HorizontalOffsetFinal.ToString()
                + ",V=" + control.VerticalOffsetFinal.ToString()
                + ",Z=" + control.ZoomFactorFinal.ToString()
                + ",P=" + control.ParallelRatioFinal.ToString()
                + "\n");
#endif

            // Notify the scroll viewer to update its inner states.
            control.SetScrollViewer(null, false, null, null);

            UpdatePage(control);
            UpdateProgress(control);
            BottomTileSetHold(false);

            if (!control.IsVertical && control.Zoom <= 100)
            {
                // Stick our view to the center of two pages.
                control.IncreasePage(0, false);
            }

            control.UpdateImages();
        }

        private void OnTwoPagesScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            CoreVirtualKeyStates ctrl_state = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control);

            if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
            {
                return;
            }

            ReaderControl control = TwoPagesReader;

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

        private void OnOnePageScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnScrollViewerSizeChanged(OnePageReader, e);
        }

        private void OnOnePageScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            OnScrollViewerViewChanged(OnePageReader, e);
        }

        private void OnTwoPagesScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnScrollViewerSizeChanged(TwoPagesReader, e);
        }

        private void OnTwoPagesScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            OnScrollViewerViewChanged(TwoPagesReader, e);
        }

        private void OnOnePageScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisScrollViewer = OnePageVerticalScrollViewer;
        }

        private void OnOnePageListViewLoaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisListView = OnePageImageListView;
        }

        private void OnTwoPagesScrollViewerLoaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisScrollViewer = TwoPagesHorizontalScrollViewer;
        }

        private void OnTwoPagesListViewLoaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisListView = TwoPagesImageListView;
        }

        // preview
        private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderFrameModel ctx = (ReaderFrameModel)e.ClickedItem;
                Shared.NavigationPageShared.PreviewMode = false;
                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsScrollViewerInitialized);
                control.SetScrollViewer(ctx.Page, true);
            });
        }
        
        // manipulating
        private void OnScrollViewerPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            (sender as UIElement).CapturePointer(e.Pointer);
            m_gesture_recognizer.ProcessDownEvent(e.GetCurrentPoint(ManipulationReference));
        }

        private void OnScrollViewerPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            m_gesture_recognizer.ProcessMoveEvents(e.GetIntermediatePoints(ManipulationReference));
        }

        private void OnScrollViewerPointerReleased(object sender, PointerRoutedEventArgs e)
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
            ReaderControl control = GetCurrentReaderControl();
            double dx = e.Delta.Translation.X;
            double dy = e.Delta.Translation.Y;

            if (!control.IsVertical && Shared.ReaderFlowDirection == FlowDirection.RightToLeft)
            {
                dx = -dx;
            }

            control.SetScrollViewer(null, false, control.HorizontalOffsetFinal - dx, control.VerticalOffsetFinal - dy);
        }

        void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            ReaderControl control = GetCurrentReaderControl();

            if (control.IsVertical || control.Zoom > 105)
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
                control.IncreasePage(-1, false);
            }
            else if (velocity < -1.0)
            {
                control.IncreasePage(1, false);
            }
        }

        // zooming
        public void ReaderSetZoom(int level)
        {
            ReaderControl control = GetCurrentReaderControl();
            double zoom = control.Zoom;
            for (int i = 0; i < level; ++i)
            {
                zoom *= 1.111111;
            }
            for (int i = 0; i > level; --i)
            {
                zoom /= 1.111111;
            }
            control.SetScrollViewer((int)zoom, false, null, null);
        }

        private void OnZoomIn()
        {
            ReaderSetZoom(1);
        }

        private void OnZoomOut()
        {
            ReaderSetZoom(-1);
        }

        private void OnZoomInBtClicked(object sender, RoutedEventArgs e)
        {
            OnZoomIn();
        }

        private void OnZoomOutBtClicked(object sender, RoutedEventArgs e)
        {
            OnZoomOut();
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
                await FavoritesDataManager.Add(m_comic.Id, m_comic.Title1, final: true);
            }
            else
            {
                await FavoritesDataManager.RemoveWithId(m_comic.Id, final: true);
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
            m_comic_record.Rating = (int)sender.Value;
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(Data.DatabaseItem.ReadRecords));
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
                    await LoadComicInformation();
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
            Shared.BottomGridPinned = false;
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
            if (!m_bottom_tile_showed || Shared.BottomGridPinned
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

        private void OnBottomGridPinnedChanged()
        {
            if (Shared.BottomGridPinned)
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
    }
}