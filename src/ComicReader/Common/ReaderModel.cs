#define DEBUG_LOG_LOAD
#if DEBUG
//#define DEBUG_LOG_JUMP
//#define DEBUG_LOG_VIEW_CHANGE
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_UPDATE_IMAGE
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Common
{
    public class ReaderModel
    {
        // Constants
        public static readonly float MaxZoom = 250f;
        public static readonly float MinZoom = 90f;
        public static readonly float ForceContinuousZoomThreshold = 105f;

        // Structs
        private class FrameOffsetData
        {
            public double ParallelBegin;
            public double ParallelCenter;
            public double ParallelEnd;
            public double PerpendicularCenter;
        };

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

        // Constructor
        public ReaderModel(Views.ReaderPageShared shared, bool is_vertical)
        {
            m_shared = shared;
            IsVertical = is_vertical;
        }

        // Observer - Common
        public bool IsCurrentReader => m_shared.ReaderSettings.IsVertical == IsVertical;
        public bool IsVertical
        {
            get; private set;
        }
        public bool IsHorizontal => !IsVertical;
        public bool IsLastPage => PageToFrame(Page, out _, out _) >= Frames.Count - 1;
        public bool IsContinuous => IsVertical ?
            m_shared.ReaderSettings.IsVerticalContinuous :
            m_shared.ReaderSettings.IsHorizontalContinuous;
        public PageArrangementType PageArrangement => IsVertical ?
            m_shared.ReaderSettings.VerticalPageArrangement :
            m_shared.ReaderSettings.HorizontalPageArrangement;

        // Observer - Data source
        public ObservableCollection<ReaderFrameViewModel> Frames { get; private set; } = new ObservableCollection<ReaderFrameViewModel>();

        // Observer - Pages
        private readonly Utils.CancellationLock m_UpdatePageLock = new Utils.CancellationLock();

        public int PageCount => Comic.ImageCount;
        private int ToDiscretePage(double page_continuous) => (int)Math.Round(page_continuous);

        public double PageSource { get; private set; } = 0.0;
        public int Page => ToDiscretePage(PageSource);

        private int m_PageFinal;
        public int PageFinal
        {
            get
            {
                FillFinalVal();
                return m_PageFinal;
            }
            private set
            {
                FillFinalVal();
                m_PageFinal = value;
            }
        }

        private async Task<bool> UpdatePage(bool use_final)
        {
            if (!LoadedFramework)
            {
                return false;
            }

            await m_UpdatePageLock.WaitAsync();
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
                    if (m_UpdatePageLock.CancellationRequested)
                    {
                        return false;
                    }

                    int i = (begin + end + 1) / 2;
                    ReaderFrameViewModel item = Frames[i];
                    FrameOffsetData offsets = FrameOffsets(i);

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
                FrameOffsetData frame_offsets = FrameOffsets(begin);

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

                PageSource = page;

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
                m_UpdatePageLock.Release();
            }
        }

        // Observer - Scroll Viewer
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

        public float Zoom { get; private set; } = 100f;
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;

        private float m_ZoomFactorFinal;
        public float ZoomFactorFinal
        {
            get
            {
                FillFinalVal();
                return m_ZoomFactorFinal;
            }
            private set
            {
                FillFinalVal();
                m_ZoomFactorFinal = value;
            }
        }

        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;

        private double m_HorizontalOffsetFinal;
        public double HorizontalOffsetFinal
        {
            get
            {
                FillFinalVal();
                return m_HorizontalOffsetFinal;
            }
            private set
            {
                FillFinalVal();
                m_HorizontalOffsetFinal = value;
            }
        }

        public double VerticalOffset => ThisScrollViewer.VerticalOffset;

        private double m_VerticalOffsetFinal;
        public double VerticalOffsetFinal
        {
            get
            {
                FillFinalVal();
                return m_VerticalOffsetFinal;
            }
            private set
            {
                FillFinalVal();
                m_VerticalOffsetFinal = value;
            }
        }

        private bool m_DisableAnimationFinal;
        public bool DisableAnimationFinal
        {
            get
            {
                FillFinalVal();
                return m_DisableAnimationFinal;
            }
            private set
            {
                FillFinalVal();
                m_DisableAnimationFinal = value;
            }
        }

        public double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
        public double ParallelOffsetFinal => IsVertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
        public double ViewportParallelLength => IsVertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => IsVertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
        private double FrameParallelLength(int i) => IsVertical ? Frames[i].Height : Frames[i].Width;

        // Observer - List View
        private double m_PaddingStartFinal;
        public double PaddingStartFinal
        {
            get
            {
                FillFinalVal();
                return m_PaddingStartFinal;
            }
            private set
            {
                FillFinalVal();
                m_PaddingStartFinal = value;
            }
        }

        private double m_PaddingEndFinal;
        public double PaddingEndFinal
        {
            get
            {
                FillFinalVal();
                return m_PaddingEndFinal;
            }
            private set
            {
                FillFinalVal();
                m_PaddingEndFinal = value;
            }
        }

        private Tuple<double, double> PageOffset(double page)
        {
            int page_int = (int)page;
            page_int = Math.Max(page_int, 1);
            page_int = Math.Min(page_int, PageCount + 1);

            int frame = PageToFrame(page_int, out _, out int neighbor);
            FrameOffsetData offsets = FrameOffsets(frame);

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

        private FrameOffsetData FrameOffsets(int frame)
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

            return new FrameOffsetData
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

        // Modifier - Configurations
        public ComicData Comic { get; set; } = null;
        public double InitialPage { get; set; } = 0.0;
        public ScrollViewer ThisScrollViewer
        {
            get; set;
        }
        public ListView ThisListView
        {
            get; set;
        }
        public Action OnLoaded
        {
            get; set;
        }

        // Modifier - Loader
        private readonly Utils.CancellationLock m_LoaderLock = new Utils.CancellationLock();
        private readonly Utils.CancellationLock m_UpdateImageLock = new Utils.CancellationLock();

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

        /// <summary>
        /// Add or update Frames with index and image aspect ratio. Index has to be<br/>
        /// continuous. As a new item is added to Frames, the reader will start loading<br/>
        /// it automatically. Make sure to set everything up before the initial call to this<br/>
        /// function.
        /// </summary>
        public async Task LoadFrame(int image_index)
        {
            await m_LoaderLock.WaitAsync();
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
                m_LoaderLock.Release();
            }
        }

        public async Task Finalize()
        {
            await m_LoaderLock.WaitAsync();
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
                m_LoaderLock.Release();
            }
        }

        public async Task<bool> UpdateImages(LockContext db, bool use_final)
        {
            if (!await UpdatePage(use_final))
            {
                return false;
            }

            await UpdateImagesInternal(db);
            return true;
        }

        private async Task UpdateImagesInternal(LockContext db, bool remove_out_of_view = true)
        {
#if DEBUG_LOG_UPDATE_IMAGE
            Log("Updating images (page " + Page.ToString() + ")");
#endif

            await m_UpdateImageLock.WaitAsync();
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

                var img_loader_tokens = new List<Utils.ImageLoader.Token>();
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
                        img_loader_tokens.Add(new Utils.ImageLoader.Token
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
                        img_loader_tokens.Add(new Utils.ImageLoader.Token
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

                await new Utils.ImageLoader.Builder(db, img_loader_tokens, m_UpdateImageLock).Commit();

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
                m_UpdateImageLock.Release();
            }
        }

        // Modifier - Manipulation
        public async Task<bool> MoveFrame(int increment)
        {
            if (!await UpdatePage(true))
            {
                return false;
            }

            MoveFrameInternal(increment, !XmlDatabase.Settings.TransitionAnimation);
            return true;
        }

        /// <summary>
        /// We assume that Page is already up-to-date at this moment.
        /// </summary>
        /// <param name="increment"></param>
        /// <param name="disable_animation"></param>
        private bool MoveFrameInternal(int increment, bool disable_animation)
        {
            if (Frames.Count == 0)
            {
                return false;
            }

            int frame = PageToFrame(PageFinal, out _, out _);
            frame += increment;
            frame = Math.Min(Frames.Count - 1, frame);
            frame = Math.Max(0, frame);

            double page = Frames[frame].Page;
            float? zoom = Zoom > 101f ? 100f : (float?)null;

            return SetScrollViewer2(zoom, page, disable_animation);
        }

        public sealed class ScrollManager : Utils.BuilderBase<bool>
        {
            private ReaderModel m_reader;
            private float? m_zoom = null;
            private double? m_parallel_offset = null;
            private double? m_horizontal_offset = null;
            private double? m_vertical_offset = null;
            private double? m_page = null;
            private bool m_disable_animation = true;

            private ScrollManager(ReaderModel reader)
            {
                m_reader = reader;
            }

            public static ScrollManager BeginTransaction(ReaderModel reader)
            {
                return new ScrollManager(reader);
            }

            protected override bool CommitImpl()
            {
                if (!m_reader.Loaded)
                {
                    return false;
                }

                bool result;

                if (m_parallel_offset.HasValue)
                {
                    result = m_reader.SetScrollViewer1(m_zoom, m_parallel_offset, m_disable_animation);
                }
                else if (m_page.HasValue)
                {
                    result = m_reader.SetScrollViewer2(m_zoom, m_page, m_disable_animation);
                }
                else
                {
                    result = m_reader.SetScrollViewer3(m_zoom, m_horizontal_offset, m_vertical_offset, m_disable_animation);
                }

                return result;
            }

            public ScrollManager Zoom(float? zoom)
            {
                m_zoom = zoom;
                return this;
            }

            public ScrollManager ParallelOffset(double? parallel_offset)
            {
                m_parallel_offset = parallel_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager HorizontalOffset(double? horizontal_offset)
            {
                m_horizontal_offset = horizontal_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager VerticalOffset(double? vertical_offset)
            {
                m_vertical_offset = vertical_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager Page(double? page)
            {
                m_page = page;
                OnSetOffset();
                return this;
            }

            public ScrollManager EnableAnimation()
            {
                m_disable_animation = false;
                return this;
            }

            private void OnSetOffset()
            {
                int checksum = 0;

                if (m_page.HasValue)
                {
                    checksum++;
                }

                if (m_parallel_offset.HasValue)
                {
                    checksum++;
                }

                if (m_horizontal_offset.HasValue || m_vertical_offset.HasValue)
                {
                    checksum++;
                }

                if (checksum > 1)
                {
                    throw new Exception("Cannot set offset twice.");
                }
            }
        }

        private bool SetScrollViewer1(float? zoom, double? parallel_offset, bool disable_animation)
        {
            double? horizontal_offset = IsHorizontal ? parallel_offset : null;
            double? vertical_offset = IsVertical ? parallel_offset : null;

            return SetScrollViewerInternal(new SetScrollViewerContext
            {
                Zoom = zoom,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        private bool SetScrollViewer2(float? zoom, double? page, bool disable_animation)
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

            return SetScrollViewerInternal(new SetScrollViewerContext
            {
                Zoom = zoom,
                PagePrediction = page,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        private bool SetScrollViewer3(float? zoom, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            return SetScrollViewerInternal(new SetScrollViewerContext
            {
                Zoom = zoom,
                HorizontalOffset = horizontal_offset,
                VerticalOffset = vertical_offset,
                DisableAnimation = disable_animation,
            });
        }

        private bool SetScrollViewerInternal(SetScrollViewerContext ctx)
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

            if (ctx.PagePrediction.HasValue)
            {
                PageFinal = ToDiscretePage(ctx.PagePrediction.Value);
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

        // Modifier - Events
        private readonly Utils.CancellationLock m_ContainerLoadedLock = new Utils.CancellationLock();
        private readonly Utils.CancellationLock m_PageRearrangeLock = new Utils.CancellationLock();

        public async Task OnContainerLoaded(ReaderFrameViewModel ctx)
        {
            LockContext db = new LockContext();

            if (ctx == null || ctx.Container == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            await m_ContainerLoadedLock.WaitAsync();
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
                    LoadedInitialPage = SetScrollViewer2(null, InitialPage, true);

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
                        await UpdateImagesInternal(db);
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
                m_ContainerLoadedLock.Release();
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
                await UpdateImagesInternal(db);
                return false;
            }

            if (!await UpdatePage(false))
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
                    MoveFrameInternal(0, false);
                }

#if DEBUG_LOG_VIEW_CHANGE
                Log("ViewChanged:"
                    + " Z=" + ZoomFactorFinal.ToString()
                    + ",H=" + HorizontalOffsetFinal.ToString()
                    + ",V=" + VerticalOffsetFinal.ToString()
                    + ",P=" + PageSource.ToString());
#endif
            }

            await UpdateImagesInternal(db, final);
            return true;
        }

        public void OnSizeChanged()
        {
            AdjustPadding();
        }

        public void OnPageRearrangeEventSealed()
        {
            Utils.C0.Run(async delegate
            {
                var db = new LockContext();

                await m_PageRearrangeLock.WaitAsync();
                try
                {
                    // Set reader status to Loading.
                    m_shared.ReaderStatus = Views.ReaderStatusEnum.Loading;

                    // Save previous states.
                    double page = PageSource;
                    float zoom = Math.Min(Zoom, 100f);

                    // Update Frames.
                    ResetFrames();

                    for (int i = 0; i < PageCount; ++i)
                    {
                        if (m_PageRearrangeLock.CancellationRequested)
                        {
                            return;
                        }

                        await LoadFrame(i);
                    }

                    await Finalize();

                    // Jump to previous page.
                    // Do NOT disable animation here or else TransformToVisual (which will be
                    // called later in OnViewChanged) will give erroneous results.
                    // Still don't know why. Been stuck here for 4h.
                    SetScrollViewer2(zoom, page, false);

                    // Update images.
                    await UpdateImages(db, true);

                    // Recover reader status.
                    m_shared.ReaderStatus = Views.ReaderStatusEnum.Working;
                }
                finally
                {
                    m_PageRearrangeLock.Release();
                }
            });
        }

        // Internal - Variables
        private readonly Views.ReaderPageShared m_shared;

        // Internal - Loader States
        private bool LoadedFramework => ThisScrollViewer != null && ThisListView != null;
        private bool LoadedFirstPage { get; set; } = false;
        private bool LoadedLastPage { get; set; } = false;
        private bool LoadedInitialPage { get; set; } = false;
        private bool LoadedImages { get; set; } = false;
        public bool Loaded { get; private set; } = false;

        // Internal - Final Values
        private bool m_final_value_set = false;

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

            m_final_value_set = true;

            m_PageFinal = Page;
            m_PaddingStartFinal = IsVertical ? ThisListView.Padding.Top : ThisListView.Padding.Left;
            m_PaddingEndFinal = IsVertical ? ThisListView.Padding.Bottom : ThisListView.Padding.Right;
            m_HorizontalOffsetFinal = HorizontalOffset;
            m_VerticalOffsetFinal = VerticalOffset;
            m_ZoomFactorFinal = ZoomFactor;
            m_DisableAnimationFinal = false;
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
                case PageArrangementType.Single:
                    left_side = true;
                    neighbor = -1;
                    return page - 1;
                case PageArrangementType.DualCover:
                    left_side = page == 1 || page % 2 == 0;
                    neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page + 1 : page - 1) : -1;
                    return page / 2;
                case PageArrangementType.DualCoverMirror:
                    left_side = page == PageCount || page % 2 == 1;
                    neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page - 1 : page + 1) : -1;
                    return page / 2;
                case PageArrangementType.DualNoCover:
                    left_side = page % 2 == 1;
                    neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page + 1 : page - 1) : -1;
                    return (page - 1) / 2;
                case PageArrangementType.DualNoCoverMirror:
                    left_side = page == PageCount || page % 2 == 0;
                    neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page - 1 : page + 1) : -1;
                    return (page - 1) / 2;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    goto case PageArrangementType.Single;
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
}
