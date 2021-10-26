//#define DEBUG_1

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
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
    public class ReaderControl
    {
        private const int max_zoom = 250;
        private const int min_zoom = 90;

        public ReaderControl(bool vertical, ScrollViewer scroll_viewer,
            ListView list_view, ReaderPageShared shared)
        {
            DisplayInformation display_info = DisplayInformation.GetForCurrentView();

            m_vertical = vertical;
            m_default_value_initialized = false;
            m_zoom = 90;
            m_page = -1;
            ThisScrollViewer = scroll_viewer;
            ThisListView = list_view;
            Shared = shared;
            LastViewportPerpendicularLength = display_info.ScreenWidthInRawPixels /
                display_info.RawPixelsPerViewPixel;
            IsAllImagesLoaded = false;
            IsScrollViewerInitialized = false;
            ImageSource = new ObservableCollection<ReaderFrameModel>();
        }

        private bool m_vertical;
        private bool m_use_absolute_value;
        private bool m_default_value_initialized;
        private double m_margin_start_final;
        private double m_margin_end_final;
        private double m_default_parallel_ratio;
        private double m_default_horizontal_offset;
        private double m_default_vertical_offset;
        private float m_default_zoom_factor;
        private bool m_default_disable_animation;
        private int m_zoom;
        private int m_page;
        private Utils.CancellationLock m_update_img_lock =
            new Utils.CancellationLock();

        public ReaderPageShared Shared;
        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;
        public double LastViewportPerpendicularLength;

        // TRUE if ScrollViewer and ListView were set, and the first container was
        // loaded
        public bool IsLayoutReady => ThisScrollViewer != null
            && ThisListView != null
            && ImageSource.Count > 0
            && ImageSource[0].Container != null;

        // TRUE if IsLayoutReady is true, and ScrollViewer were initialized
        public bool IsScrollViewerInitialized { get; set; }

        // TRUE if IsScrollViewerInitialized is true, and all containers were loaded
        public bool IsAllImagesLoaded { get; set; }

        // common properties
        public ComicItemData Comic { get; set; }
        public ObservableCollection<ReaderFrameModel> ImageSource { get; set; }
        public int Pages => Comic.ImageFiles.Count;
        public int Page => m_page;
        public int Zoom => m_zoom;
        public bool IsActive { get; set; }

        // scroll viewer
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;
        public float ZoomFactorFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_default_zoom_factor;
            }
            set
            {
                m_default_zoom_factor = value;
            }
        }
        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
        public double HorizontalOffsetFinal
        {
            get
            {
                if (m_use_absolute_value || m_vertical)
                {
                    InitializeDefaultValue();
                    return m_default_horizontal_offset;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = true;
                m_default_horizontal_offset = value;
            }
        }
        public double VerticalOffset => ThisScrollViewer.VerticalOffset;
        public double VerticalOffsetFinal
        {
            get
            {
                if (m_use_absolute_value || !m_vertical)
                {
                    InitializeDefaultValue();
                    return m_default_vertical_offset;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = true;
                m_default_vertical_offset = value;
            }
        }
        public double ParallelRatioFinal
        {
            get
            {
                if (m_use_absolute_value)
                {
                    return (ParallelOffsetFinal + ViewportParallelLength * 0.5 - MarginStartFinal)
                        / (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal);
                }
                else
                {
                    InitializeDefaultValue();
                    return m_default_parallel_ratio;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = false;
                m_default_parallel_ratio = value;
            }
        }
        public double ParallelOffset => m_vertical ? VerticalOffset : HorizontalOffset;
        public double ParallelOffsetFinal
        {
            get
            {
                if (m_use_absolute_value)
                {
                    return m_vertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
                }
                else
                {
                    return ParallelRatioFinal * (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal)
                        + MarginStartFinal - ViewportParallelLength * 0.5;
                }
            }
        }
        public double PerpendicularOffset => m_vertical ? ThisScrollViewer.HorizontalOffset : ThisScrollViewer.VerticalOffset;
        public double ViewportParallelLength => m_vertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => m_vertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => m_vertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
        public double ZoomCoefficient()
        {
            double viewport_ratio = ThisScrollViewer.ViewportWidth / ThisScrollViewer.ViewportHeight;
            double image_ratio = ImageSource[0].Container.ActualWidth / ImageSource[0].Container.ActualHeight;
            return 0.01 * (viewport_ratio > image_ratio
                ? ThisScrollViewer.ViewportHeight / ImageSource[0].Container.ActualHeight
                : ThisScrollViewer.ViewportWidth / ImageSource[0].Container.ActualWidth);
        }
        public void SetScrollViewerZoom(ref double? horizontal_offset, ref double? vertical_offset,
            ref int? zoom, out float? zoom_out)
        {
            if (ImageSource.Count == 0 || ImageSource[0].Container == null
                || ImageSource[0].Container.ActualWidth < 0.1
                || ImageSource[0].Container.ActualHeight < 0.1)
            {
                zoom = m_zoom;
                zoom_out = null;
                return;
            }

            double zoom_coefficient = ZoomCoefficient();
            bool zoom_sat = false;
            bool zoom_null = zoom == null;
            int zoom_cpy = zoom_null ? (int)(ZoomFactorFinal / zoom_coefficient) : (int)zoom;

            // an error less than 1 is acceptable
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

            if (horizontal_offset < 0)
            {
                horizontal_offset = 0;
            }

            if (vertical_offset < 0)
            {
                vertical_offset = 0;
            }
        }
        private void _SetScrollViewer(int? zoom, bool disable_animation,
            int? page, double? parallel_ratio, double? horizontal_offset, double? vertical_offset)
        {
            if (!IsLayoutReady)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("ParamIn:"
                + " Z=" + zoom.ToString()
                + ",D=" + disable_animation.ToString()
                + ",Pg=" + page.ToString()
                + ",P=" + parallel_ratio.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + "\n");
#endif
            SetScrollViewerPage(page, ref horizontal_offset, ref vertical_offset);
            SetScrollViewerZoom(ref horizontal_offset, ref vertical_offset, ref zoom, out float? zoom_out);
#if DEBUG_1
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
                ChangeView(zoom_out, (double)parallel_ratio, disable_animation);
            }
            else
            {
                ChangeView(zoom_out, horizontal_offset, vertical_offset, disable_animation);
            }

            m_zoom = (int)zoom;
            //ZoomTextBlock.Text = m_zoom.ToString() + "%";
            Shared.ContentPageShared.CanZoomIn = m_zoom < max_zoom;
            Shared.ContentPageShared.CanZoomOut = m_zoom > min_zoom;

            CorrectParallelOffset();
        }
        private void SetScrollViewerPage(int? page, ref double? horizontal_offset, ref double? vertical_offset)
        {
            if (page == null)
            {
                return;
            }

            if (page < 1)
            {
                page = 1;
            }
            else if (page > Pages)
            {
                page = Pages;
            }

            if (m_page == page)
            {
                return;
            }

            double page_parallel_offset = 0.0;

            for (int i = 0; i < page - 1; ++i)
            {
                if (ImageSource[i].Container == null)
                {
                    return;
                }
            }
            for (int i = 0; i < page - 1; ++i)
            {
                page_parallel_offset += GridParallelLength(i);
            }

            page_parallel_offset += GridParallelLength((int)page - 1) * 0.5;
            double parallel_offset = page_parallel_offset * ThisScrollViewer.ZoomFactor
                + MarginStartFinal - ViewportParallelLength * 0.5;
            SetOffset(ref horizontal_offset, ref vertical_offset, parallel_offset, null);
        }
        private bool CorrectParallelOffset()
        {
            if (ImageSource.Count == 0 || ImageSource[0].Container == null)
            {
                return false;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("CorrectOffset\n");
#endif
            double space = MarginStartFinal - ParallelOffsetFinal;
            double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;
            double image_center_offset = MarginStartFinal
                + GridParallelLength(0) * 0.5 * ZoomFactorFinal;
            double image_center_to_screen_center = image_center_offset - screen_center_offset;
            double movement_forward = Math.Min(space, image_center_to_screen_center);

            if (movement_forward > 0)
            {
                double parallel_offset = ParallelOffsetFinal + movement_forward;
                return ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }

            if (ImageSource[ImageSource.Count - 1].Container == null)
            {
                return false;
            }

            space = MarginEndFinal - (ExtentParallelLengthFinal
                - ParallelOffsetFinal - ViewportParallelLength);
            image_center_offset = ExtentParallelLengthFinal - MarginEndFinal
                - GridParallelLength(ImageSource.Count - 1) * 0.5 * ZoomFactorFinal;
            image_center_to_screen_center = screen_center_offset - image_center_offset;
            double movement_backward = Math.Min(space, image_center_to_screen_center);

            if (movement_backward > 0)
            {
                double parallel_offset = ParallelOffsetFinal - movement_backward;
                return ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }

            return false;
        }
        public void SetScrollViewer(int? zoom, bool disable_animation, double? horizontal_offset, double? vertical_offset)
        {
            _SetScrollViewer(zoom, disable_animation, null, null, horizontal_offset, vertical_offset);
        }
        public void SetScrollViewer(int? page, bool disable_animation)
        {
            _SetScrollViewer(null, disable_animation, page, null, null, null);
        }
        public void SetScrollViewer(int? zoom, bool disable_animation, double parallel_ratio)
        {
            _SetScrollViewer(zoom, disable_animation, null, parallel_ratio, null, null);
        }

        // list view
        public double MarginStartFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_margin_start_final * ZoomFactorFinal;
            }
        }
        public double MarginEndFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_margin_end_final * ZoomFactorFinal;
            }
        }
        public void SetMargin(double start, double end)
        {
            InitializeDefaultValue();
            m_margin_start_final = start;
            m_margin_end_final = end;
            if (m_vertical)
            {
                ThisListView.Margin = new Thickness(0.0, start, 0.0, end);
            }
            else
            {
                ThisListView.Margin = new Thickness(start, 0.0, end, 0.0);
            }
        }
        public void UpdateMargin()
        {
            if (!IsLayoutReady)
            {
                return;
            }

            if (ImageSource.Count == 0)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("MarginUpdated\n");
#endif
            double zoom_coefficient = ZoomCoefficient();
            double zoom_factor = min_zoom * zoom_coefficient;
            double inner_length = ViewportParallelLength / zoom_factor;
            double new_start = 0.0;

            if (ImageSource[0].Container != null)
            {
                new_start = (inner_length - GridParallelLength(0)) / 2;

                if (new_start < 0)
                {
                    new_start = 0;
                }
            }

            double new_end = 0.0;

            if (ImageSource[ImageSource.Count - 1].Container != null)
            {
                new_end = (inner_length - GridParallelLength(ImageSource.Count - 1)) / 2;

                if (new_end < 0)
                {
                    new_end = 0;
                }
            }

            SetMargin(new_start, new_end);
        }

        // grids
        public double GridParallelLength(int i) => m_vertical ? ImageSource[i].Container.ActualHeight : ImageSource[i].Container.ActualWidth;
        public double GridPerpendicularLength(int i) => m_vertical ? ImageSource[i].Container.ActualWidth : ImageSource[i].Container.ActualHeight;

        // conversions
        public void SetOffset(ref double? horizontal_offset, ref double? vertical_offset, double? parallel_offset, double? perpendicular_offset)
        {
            if (m_vertical)
            {
                if (parallel_offset != null)
                {
                    vertical_offset = parallel_offset;
                }

                if (perpendicular_offset != null)
                {
                    horizontal_offset = perpendicular_offset;
                }
            }
            else
            {
                if (parallel_offset != null)
                {
                    horizontal_offset = parallel_offset;
                }

                if (perpendicular_offset != null)
                {
                    vertical_offset = perpendicular_offset;
                }
            }
        }
        public double? ParallelVal(double? horizontal_val, double? vertical_val) => m_vertical ? vertical_val : horizontal_val;
        public double? PerpendicularVal(double? horizontal_val, double? vertical_val) => m_vertical ? horizontal_val : vertical_val;
        public double? HorizontalVal(double? parallel_val, double? perpendicular_val) => m_vertical ? perpendicular_val : parallel_val;
        public double? VerticalVal(double? parallel_val, double? perpendicular_val) => m_vertical ? parallel_val : perpendicular_val;
        private double FinalVal(double val) => val / ZoomFactor * ZoomFactorFinal;

        // events
        public void OnImageContainerSet(ReaderFrameModel ctx)
        {
            Utils.Methods.Run(async delegate
            {
                bool all_loaded = false;

                if (ImageSource.Count == Pages)
                {
                    all_loaded = true;
                    foreach (ReaderFrameModel d in ImageSource)
                    {
                        if (d.Container == null)
                        {
                            all_loaded = false;
                            break;
                        }
                    }
                }

                if (ctx.Page == 1)
                {
                    await Utils.Methods.WaitFor(() => IsLayoutReady);
#if DEBUG_1
                    System.Diagnostics.Debug.Print("FirstContainerSet\n");
#endif
                    UpdateMargin();
                    SetScrollViewer(m_zoom, true, null, null);
                    IsScrollViewerInitialized = true;
                }

                if (all_loaded)
                {
                    await Utils.Methods.WaitFor(() => IsScrollViewerInitialized);
#if DEBUG_1
                    System.Diagnostics.Debug.Print("AllLoaded\n");
#endif
                    UpdateMargin();
                    SetScrollViewer(null, true, null, null);
                    IsAllImagesLoaded = true;
                }
            });
        }

        // common functions
        public void Clear()
        {
            ImageSource.Clear();
        }
        public bool ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
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
                HorizontalOffsetFinal = (double)horizontal_offset;
            }

            if (vertical_offset != null)
            {
                VerticalOffsetFinal = (double)vertical_offset;
            }

            if (zoom_factor != null)
            {
                ZoomFactorFinal = (float)zoom_factor;
            }

            if (disable_animation)
            {
                m_default_disable_animation = true;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_default_disable_animation
                + "\n");
#endif
            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_default_disable_animation);
            return true;
        }
        public bool ChangeView(float? zoom_factor, double parallel_ratio, bool disable_animation)
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            ParallelRatioFinal = parallel_ratio;

            if (zoom_factor != null)
            {
                ZoomFactorFinal = (float)zoom_factor;
            }

            if (disable_animation)
            {
                m_default_disable_animation = true;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("Commit:"
                + " P=" + ParallelRatioFinal.ToString()
                + ",Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_default_disable_animation
                + "\n");
#endif
            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_default_disable_animation);
            return true;
        }
        public void FinalValueExpired()
        {
            m_default_value_initialized = false;
        }
        public bool UpdatePage()
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            double current_offset = (ParallelOffsetFinal - MarginStartFinal
                + ViewportParallelLength * 0.5) / ZoomFactorFinal;
            int page = 0;

            for (int i = 0; i < ImageSource.Count; ++i)
            {
                Grid grid = ImageSource[i].Container;

                if (grid == null)
                {
                    return false;
                }

                current_offset -= GridParallelLength(i);

                if (current_offset < 0.0)
                {
                    page = i + 1;
                    break;
                }
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
                await m_update_img_lock.WaitAsync();

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

                    await ComicDataManager.LoadImages(img_loader_tokens, double.PositiveInfinity, double.PositiveInfinity, m_update_img_lock);
                }
                finally
                {
                    m_update_img_lock.Release();
                }
            });
        }

        // internal
        private void InitializeDefaultValue()
        {
            if (!IsLayoutReady)
            {
                return;
            }

            if (m_default_value_initialized)
            {
                return;
            }

            m_margin_start_final = m_vertical ? ThisListView.Margin.Top : ThisListView.Margin.Left;
            m_margin_end_final = m_vertical ? ThisListView.Margin.Bottom : ThisListView.Margin.Right;

            double margin_start = m_margin_start_final * ZoomFactor;
            double margin_end = m_margin_end_final * ZoomFactor;
            m_default_parallel_ratio = (ParallelOffset + ViewportParallelLength * 0.5 - margin_start)
                / (ExtentParallelLength - margin_start - margin_end);
            m_default_horizontal_offset = HorizontalOffset;
            m_default_vertical_offset = VerticalOffset;
            m_default_zoom_factor = ZoomFactor;
            m_default_disable_animation = false;

            m_default_value_initialized = true;
        }
    }

    public class ReaderPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ContentPageShared m_ContentPageShared;
        public ContentPageShared ContentPageShared
        {
            get => m_ContentPageShared;
            set
            {
                m_ContentPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentPageShared"));
            }
        }

        private FlowDirection m_Reader_FlowDirection;
        public FlowDirection P_Reader_FlowDirection
        {
            get => m_Reader_FlowDirection;
            set
            {
                m_Reader_FlowDirection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("P_Reader_FlowDirection"));
            }
        }

        // comic basic info
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
            }
        }

        private string m_ComicPrimaryTitle;
        public string ComicPrimaryTitle
        {
            get => m_ComicPrimaryTitle;
            set
            {
                m_ComicPrimaryTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicPrimaryTitle"));
            }
        }

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

        // reader properties
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
            IsGridViewVisible = ContentPageShared.IsGridViewMode;
            IsOnePageReaderVisible = !ContentPageShared.IsGridViewMode && !ContentPageShared.IsTwoPagesMode;
            IsTwoPagesReaderVisible = !ContentPageShared.IsGridViewMode && ContentPageShared.IsTwoPagesMode;
        }

        private bool m_BottomGrid_Pinned;
        public bool P_BottomGrid_Pinned
        {
            get => m_BottomGrid_Pinned;
            set
            {
                m_BottomGrid_Pinned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("P_BottomGrid_Pinned"));
                E_BottomGrid_PinnedChanged?.Invoke();
            }
        }

        public Action E_BottomGrid_PinnedChanged;
    }

    public sealed partial class ReaderPage : Page
    {
        public static ReaderPage Current;
        public ReaderPageShared Shared { get; set; }
        private ReaderControl OnePageReader { get; set; }
        private ReaderControl TwoPagesReader { get; set; }
        private ObservableCollection<ReaderFrameModel> GridViewDataSource { get; set; }
        private ObservableCollection<TagsModel> ComicTagSource { get; set; }

        private Utils.Tab.TabManager m_tab_manager;

        private ComicItemData m_comic;
        private RecentReadItemData m_comic_record;

        private Utils.TaskQueue.TaskQueue m_load_image_queue = Utils.TaskQueue.TaskQueueManager.EmptyQueue();
        private double m_position;
        private PointerPoint m_drag_pointer;

        // BottomGrid
        private bool m_BottomGrid_showed;
        private bool m_BottomGrid_hold;
        private bool m_BottomGrid_pointer_in;
        private int m_BottomGrid_exit_requests;

        private Utils.CancellationLock m_reader_h_img_loader_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_reader_v_img_loader_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_preview_img_loader_lock = new Utils.CancellationLock();

        public ReaderPage()
        {
            Current = this;
            Shared = new ReaderPageShared();
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicPrimaryTitle = "";
            Shared.ComicDir = "";
            Shared.IsEditable = false;
            OnePageReader = new ReaderControl(true, OnePageVerticalScrollViewer, OnePageImageListView, Shared);
            TwoPagesReader = new ReaderControl(false, TwoPagesHorizontalScrollViewer, TwoPagesImageListView, Shared);
            GridViewDataSource = new ObservableCollection<ReaderFrameModel>();
            ComicTagSource = new ObservableCollection<TagsModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnUpdate = OnUpdate;

            m_comic = null;
            m_comic_record = null;
            m_position = -1.0;
            m_drag_pointer = null;

            // BottomGrid
            m_BottomGrid_showed = false;
            m_BottomGrid_hold = false;
            m_BottomGrid_pointer_in = true;
            m_BottomGrid_exit_requests = 0;
            Shared.P_BottomGrid_Pinned = false;
            Shared.E_BottomGrid_PinnedChanged = C_BottomGrid_OnPinnedChanged;
            
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

        private void OnSetShared(object shared)
        {
            Shared.ContentPageShared = (ContentPageShared)shared;
            Shared.ContentPageShared.OnFavoritesButtonClicked += OnFavoritesBtClicked;
            Shared.ContentPageShared.RootPageShared.OnExitFullscreenMode += C_BottomGrid_ForceHide;
            Shared.ContentPageShared.OnZoomInButtonClicked += OnZoomInClick;
            Shared.ContentPageShared.OnZoomOutButtonClicked += OnZoomOutClick;
            Shared.ContentPageShared.OnTwoPagesModeChanged += OnTwoPagesModeChanged;
            Shared.ContentPageShared.OnTwoPagesModeChanged += Shared.UpdateReaderVisibility;
            Shared.ContentPageShared.OnGridViewModeChanged += Shared.UpdateReaderVisibility;
            
            Shared.ContentPageShared.IsGridViewMode = false;
            Shared.ContentPageShared.IsTwoPagesMode = false;
        }

        private void OnPageEntered()
        {
            Shared.P_Reader_FlowDirection = Database.AppSettings.LeftToRight ?
                FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.Methods.Run(async delegate
            {
                Shared.ContentPageShared.RootPageShared.CurrentPageType = Utils.Tab.PageType.Reader;
                tab_id.Type = Utils.Tab.PageType.Reader;

                if (m_comic != tab_id.RequestArgs)
                {
                    ComicItemData comic = (ComicItemData)tab_id.RequestArgs;
                    tab_id.Tab.Header = comic.Title;
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

        // user-defined functions
        // load comic
        public async Task LoadComic(ComicItemData comic)
        {
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
                await HistoryDataManager.Add(m_comic.Id, m_comic.Title, true);

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
                preview_width = (double)Resources["ReaderPreviewImageWidth"];
                preview_height = (double)Resources["ReaderPreviewImageHeight"];
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
                            Width = 500.0,
                            Height = 500.0 / img.PixelWidth * img.PixelHeight,
                            OnContainerSet = OnePageReader.OnImageContainerSet,
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
                            Width = 300.0 / img.PixelHeight * img.PixelWidth,
                            Height = 300.0,
                            OnContainerSet = TwoPagesReader.OnImageContainerSet,
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
                double.PositiveInfinity, double.PositiveInfinity, m_reader_v_img_loader_lock);
            Task reader_h_loader_task = ComicDataManager.LoadImages(reader_h_img_loader_tokens,
                double.PositiveInfinity, double.PositiveInfinity, m_reader_h_img_loader_lock);
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

            Shared.ContentPageShared.IsInLib = !m_comic.IsExternal;
            Shared.ComicTitle1 = m_comic.Title;
            Shared.ComicTitle2 = m_comic.Title2;
            Shared.ComicPrimaryTitle = Shared.ComicTitle2.Length == 0 ? Shared.ComicTitle1 : Shared.ComicTitle2;
            Shared.ComicDir = m_comic.Directory;
            Shared.IsEditable = !(m_comic.IsExternal && m_comic.InfoFile == null);
            Shared.Progress = "";
            LoadComicTag();

            if (!m_comic.IsExternal)
            {
                Shared.ContentPageShared.IsFavorite = await FavoritesDataManager.FromId(m_comic.Id) != null;
                Shared.Rating = m_comic_record.Rating;
            }
        }

        private void LoadComicTag()
        {
            if (m_comic == null)
            {
                return;
            }

            ComicTagSource.Clear();
            for (int i = 0; i < m_comic.Tags.Count; ++i)
            {
                TagData tags = m_comic.Tags[i];
                TagsModel tags_model = new TagsModel(tags.Name);

                foreach (string tag in tags.Tags)
                {
                    TagModel tag_model = new TagModel
                    {
                        Tag = tag,
                        E_Clicked = E_InfoPane_TagClicked
                    };
                    tags_model.Tags.Add(tag_model);
                }

                ComicTagSource.Add(tags_model);
            }
        }

        // buttons
        public void ExpandInfoPane()
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        }

        private void OnInformationBtClicked(object sender, RoutedEventArgs e)
        {
            ExpandInfoPane();
        }

        private void OnInformationPaneVersionMenuItemSelected(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                MenuFlyoutItem item = (MenuFlyoutItem)sender;
                if (item.Text != m_comic.Id)
                {
                    ComicItemData comic = await ComicDataManager.FromId(item.Text);
                    RootPage.Current.LoadTab(null, Utils.Tab.PageType.Reader, comic);
                }
            });
        }

        private void OnSplitViewEditBtClicked(object sender, RoutedEventArgs e)
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

        private void OnFavoritesBtClicked()
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(!Shared.ContentPageShared.IsFavorite);
            });
        }

        public async Task SetIsFavorite(bool is_favorite)
        {
            if (Shared.ContentPageShared.IsFavorite == is_favorite)
            {
                return;
            }

            Shared.ContentPageShared.IsFavorite = is_favorite;

            if (is_favorite)
            {
                await FavoritesDataManager.Add(m_comic.Id, m_comic.Title, final: true);
            }
            else
            {
                await FavoritesDataManager.RemoveWithId(m_comic.Id, final: true);
            }
        }

        private void FavoriteBt_Checked(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(true);
            });
        }

        private void FavoriteBt_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(false);
            });
        }

        private ReaderControl GetCurrentReaderControl()
        {
            if (Shared.ContentPageShared.IsTwoPagesMode)
            {
                return TwoPagesReader;
            }
            else
            {
                return OnePageReader;
            }
        }

        private void ReaderControlSizeChanged(ReaderControl control, SizeChangedEventArgs e)
        {
            if (!control.IsAllImagesLoaded)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("SizeChanged\n");
#endif
            control.UpdateMargin();
            int zoom = (int)(control.Zoom / control.ViewportPerpendicularLength * control.LastViewportPerpendicularLength);

            if (zoom > 100)
            {
                zoom = 100;
            }

            control.SetScrollViewer(zoom, true, m_position);
            control.LastViewportPerpendicularLength = control.ViewportPerpendicularLength;
        }

        private void ReaderControlViewChanged(ReaderControl control, ScrollViewerViewChangedEventArgs e)
        {
            if (!control.IsScrollViewerInitialized || e.IsIntermediate)
            {
                return;
            }

            control.FinalValueExpired();

            if (control.IsAllImagesLoaded)
            {
                m_position = control.ParallelRatioFinal;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("ViewChanged:"
                + " H=" + control.HorizontalOffsetFinal.ToString()
                + ",V=" + control.VerticalOffsetFinal.ToString()
                + ",Z=" + control.ZoomFactorFinal.ToString()
                + ",P=" + control.ParallelRatioFinal.ToString()
                + "\n");
#endif
            control.SetScrollViewer(null, false, null, null);
            UpdatePage(control);
            UpdateProgress(control);
            C_BottomGrid_SetHold(false);
            control.UpdateImages();
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

        // reader controls
        public void C_Reader_SetZoom(int level)
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

        /*private void Zoom_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(null);
            int delta = pt.Properties.MouseWheelDelta;
            C_Reader_SetZoom(delta / 120);
            e.Handled = true;
        }*/

        public void OnTwoPagesModeChanged()
        {
            Utils.Methods.Run(async delegate
            {
                int last_zoom = 0;

                if (Shared.ContentPageShared.IsTwoPagesMode)
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

                if (m_position < 0)
                {
                    return;
                }

                double position = m_position;

                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsAllImagesLoaded);

                control.UpdateMargin();
                control.SetScrollViewer(Math.Min(last_zoom, 100), true, position);
            });
        }

        /*private void Page_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (OnePageVerticalScrollViewer == null)
            {
                return;
            }

            if (m_page <= 0)
            {
                return;
            }

            PointerPoint pt = e.GetCurrentPoint(null);
            int delta = pt.Properties.MouseWheelDelta;
            int page_backward = delta / 120;

            ReaderControl control = GetCurrentReaderControl();
            int new_page = m_page - page_backward;
            int? zoom = null;

            if (m_zoom > 100)
            {
                zoom = 100;
            }

            SetScrollViewer(control, new_page, false);
            SetScrollViewer(control, zoom, false, null, null);
            OverwritePage(new_page);
        }*/

        // reader events
        private void OnePageVerticalScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ReaderControlViewChanged(OnePageReader, e);
        }

        private void OnePageVerticalScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReaderControlSizeChanged(OnePageReader, e);
        }

        private void TwoPagesHorizontalScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReaderControlSizeChanged(TwoPagesReader, e);
        }

        private void TwoPagesHorizontalScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ReaderControlViewChanged(TwoPagesReader, e);
        }

        // other logics
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

        private void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            m_comic_record.Rating = (int)sender.Value;
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(Data.DatabaseItem.ReadRecords));
        }

        private void OnePageVerticalScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisScrollViewer = OnePageVerticalScrollViewer;
        }

        private void OnePageImageListView_Loaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisListView = OnePageImageListView;
        }

        private void TwoPagesHorizontalScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisScrollViewer = TwoPagesHorizontalScrollViewer;
        }

        private void TwoPagesImageListView_Loaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisListView = TwoPagesImageListView;
        }

        // panning
        private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(PanningReference);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }

            m_drag_pointer = pt;
            e.Handled = true;
        }

        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (m_drag_pointer == null)
            {
                return;
            }

            PointerPoint pt = e.GetCurrentPoint(PanningReference);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                m_drag_pointer = null;
                return;
            }

            double dx = m_drag_pointer.Position.X - pt.Position.X;
            double dy = m_drag_pointer.Position.Y - pt.Position.Y;

            if (Shared.P_Reader_FlowDirection == FlowDirection.RightToLeft)
            {
                dx = -dx;
            }

            ReaderControl control = GetCurrentReaderControl();
            control.SetScrollViewer(null, false, control.HorizontalOffsetFinal + dx, control.VerticalOffsetFinal + dy);
            m_drag_pointer = pt;
            e.Handled = true;
        }

        // BottomGrid
        private void C_BottomGrid_Show()
        {
            if (m_BottomGrid_showed || Shared.ContentPageShared.RootPageShared.IsFullscreenN)
            {
                return;
            }

            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 1.0);
            BottomGridStoryboard.Begin();
            m_BottomGrid_showed = true;
        }

        private void C_BottomGrid_Hide()
        {
            if (!m_BottomGrid_showed || Shared.P_BottomGrid_Pinned
                || m_BottomGrid_hold || m_BottomGrid_pointer_in)
            {
                return;
            }

            C_BottomGrid_ForceHide();
        }

        private void C_BottomGrid_ForceHide()
        {
            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 0.0);
            BottomGridStoryboard.Begin();
            m_BottomGrid_showed = false;
            m_BottomGrid_hold = false;
        }

        private void C_BottomGrid_SetHold(bool val)
        {
            m_BottomGrid_hold = val;

            if (m_BottomGrid_hold)
            {
                C_BottomGrid_Show();
            }
            else
            {
                C_BottomGrid_Hide();
            }
        }

        private void C_BottomGrid_OnPinnedChanged()
        {
            if (Shared.P_BottomGrid_Pinned)
            {
                C_BottomGrid_Show();
            }
        }

        private void E_BottomGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            m_BottomGrid_pointer_in = true;
            C_BottomGrid_Show();
        }

        private void E_BottomGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!m_BottomGrid_showed || m_BottomGrid_hold)
            {
                return;
            }

            m_BottomGrid_pointer_in = false;

            _ = Task.Run(() =>
            {
                _ = Interlocked.Increment(ref m_BottomGrid_exit_requests);
                Task.Delay(2000).Wait();
                int r = Interlocked.Decrement(ref m_BottomGrid_exit_requests);

                if (!m_BottomGrid_showed || m_BottomGrid_pointer_in || r != 0)
                {
                    return;
                }

                _ = Utils.Methods.Sync(delegate
                {
                    C_BottomGrid_Hide();
                });
            });
        }

        private void E_Reader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            C_BottomGrid_SetHold(!m_BottomGrid_showed);
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderFrameModel ctx = (ReaderFrameModel)e.ClickedItem;
                Shared.ContentPageShared.IsGridViewMode = false;
                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsScrollViewerInitialized);
                control.SetScrollViewer(ctx.Page, true);
            });
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            OnFavoritesBtClicked();
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            OnZoomInClick();
        }

        private void OnZoomInClick()
        {
            C_Reader_SetZoom(1);
        }

        private void OnZoomOutClick()
        {
            C_Reader_SetZoom(-1);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            OnZoomOutClick();
        }

        private void E_InfoPane_TagClicked(object sender, RoutedEventArgs e)
        {
            TagModel ctx = (TagModel)((Button)sender).DataContext;
            RootPage.Current.LoadTab(null, Utils.Tab.PageType.Search, "<tag:" + ctx.Tag + ">");
        }

        //private void DebugButton_Click(object sender, RoutedEventArgs e)
        //{
        //    GC.Collect();
        //}
    }
}