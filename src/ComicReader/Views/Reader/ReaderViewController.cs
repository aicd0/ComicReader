#define DEBUG_LOG_LOAD
#if DEBUG
//#define DEBUG_LOG_JUMP
//#define DEBUG_LOG_MANIPULATION
//#define DEBUG_LOG_VIEW_CHANGE
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_UPDATE_IMAGE
#endif

using ComicReader.Common.Structs;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;
using ComicReader.Utils.Image;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ComicReader.Views.Reader
{
    internal class ReaderViewController
    {
        // Constants
        public static readonly float MaxZoom = 250f;
        public static readonly float MinZoom = 90f;
        public static readonly float ForceContinuousZoomThreshold = 105f;
        private static readonly int MinPreloadFramesBefore = 5;
        private static readonly int MaxPreloadFramesBefore = 10;
        private static readonly int MinPreloadFramesAfter = 5;
        private static readonly int MaxPreloadFramesAfter = 10;

        // Constructor
        public ReaderViewController(ReaderPageViewModel viewModel, string name, bool is_vertical)
        {
            _name = name;
            _viewModel = viewModel;
            IsVertical = is_vertical;
        }

        // Observer - Common
        public bool IsCurrentReader => _viewModel.ReaderSettingsLiveData.GetValue().IsVertical == IsVertical;
        public bool IsVertical
        {
            get; private set;
        }
        public bool IsHorizontal => !IsVertical;
        public bool IsLeftToRight => _viewModel.ReaderSettingsLiveData.GetValue().IsLeftToRight;
        public bool IsLastPage => PageToFrame(Page, out _, out _) >= DataSource.Count - 1;
        public bool IsContinuous => IsVertical ?
            _viewModel.ReaderSettingsLiveData.GetValue().IsVerticalContinuous :
            _viewModel.ReaderSettingsLiveData.GetValue().IsHorizontalContinuous;
        public PageArrangementType PageArrangement => IsVertical ?
            _viewModel.ReaderSettingsLiveData.GetValue().VerticalPageArrangement :
            _viewModel.ReaderSettingsLiveData.GetValue().HorizontalPageArrangement;

        // Observer - Data source
        public ObservableCollection<ReaderFrameViewModel> DataSource { get; private set; } = new ObservableCollection<ReaderFrameViewModel>();

        // Observer - Pages
        private readonly Utils.CancellationLock m_UpdatePageLock = new Utils.CancellationLock();

        public int PageCount => Comic?.ImageCount ?? 0;
        private int ToDiscretePage(double page_continuous)
        {
            return (int)Math.Round(page_continuous);
        }

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

            return await m_UpdatePageLock.LockAsync(delegate (CancellationLock.Token token)
            {
                double offset;
                {
                    double parallel_offset = use_final ? ParallelOffsetFinal : ParallelOffset;
                    double zoom_factor = use_final ? ZoomFactorFinal : ZoomFactor;
                    offset = (parallel_offset + ViewportParallelLength * 0.5) / zoom_factor;
                }

                // Locate current frame using binary search.
                if (DataSource.Count == 0)
                {
                    return false;
                }

                int begin = 0;
                int end = DataSource.Count - 1;

                while (begin < end)
                {
                    if (token.CancellationRequested)
                    {
                        return false;
                    }

                    int i = (begin + end + 1) / 2;
                    ReaderFrameViewModel item = DataSource[i];
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

                ReaderFrameViewModel frame = DataSource[begin];
                FrameOffsetData frame_offsets = FrameOffsets(begin);

                if (frame_offsets == null)
                {
                    return false;
                }

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
                    "Page=" + PageSource.ToString() + "," +
                    "UseF=" + use_final.ToString() + "," +
                    "PO=" + ParallelOffset.ToString() + "," +
                    "POF=" + ParallelOffsetFinal.ToString() + "," +
                    "ZF=" + ZoomFactor.ToString() + ")");
#endif

                return true;
            });
        }

        // Observer - Scroll Viewer
        private ZoomCoefficientResult ZoomCoefficient(int frame_idx)
        {
            if (DataSource.Count == 0)
            {
                return null;
            }

            if (frame_idx < 0 || frame_idx >= DataSource.Count)
            {
                System.Diagnostics.Debug.Assert(false);
                return null;
            }

            double viewport_width = ThisScrollViewer.ViewportWidth;
            double viewport_height = ThisScrollViewer.ViewportHeight;
            double frame_width = DataSource[frame_idx].FrameWidth;
            double frame_height = DataSource[frame_idx].FrameHeight;

            double minValue = Math.Min(viewport_width, viewport_height);
            minValue = Math.Min(minValue, frame_width);
            minValue = Math.Min(minValue, frame_height);
            if (minValue < 0.1)
            {
                return null;
            }

            return new ZoomCoefficientResult
            {
                FitWidth = 0.01 * viewport_width / frame_width,
                FitHeight = 0.01 * viewport_height / frame_height
            };
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
        private double FrameParallelLength(int i)
        {
            Grid container = DataSource[i].ItemContainer?.Container;
            if (container == null)
            {
                return 0;
            }

            return IsVertical ? container.ActualHeight : container.ActualWidth;
        }

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
            page_int = Math.Min(page_int, PageCount);

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
            if (frame < 0 || frame >= DataSource.Count)
            {
                return null;
            }

            ReaderFrameViewModel item = DataSource[frame];
            Grid container = item.ItemContainer?.Container;
            if (container == null)
            {
                return null;
            }

            GeneralTransform frame_transform = container.TransformToVisual(ThisListView);
            Point frame_position = frame_transform.TransformPoint(new Point(0.0, 0.0));

            double parallel_offset = IsVertical ? frame_position.Y : frame_position.X;
            double perpendicular_offset = IsVertical ? frame_position.X : frame_position.Y;

            bool left_to_right = IsLeftToRight;

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
        private ComicData _comic;
        public ComicData Comic
        {
            get => _comic;
            set
            {
                _comic = value;
                StopLoadingImage();
            }
        }
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

        // Modifier - General Loader
        private readonly Utils.CancellationLock m_LoaderLock = new Utils.CancellationLock();

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

        public void StopLoadingImage()
        {
            _updateImageSession.Next();
        }

        private void ResetFrames()
        {
            for (int i = 0; i < DataSource.Count; ++i)
            {
                ReaderFrameViewModel item = DataSource[i];
                item.Notify(cancel: true);
                item.PageL = -1;
                item.PageR = -1;
                item.ImageL.ImageSet = false;
                item.ImageR.ImageSet = false;
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
            await m_LoaderLock.LockAsync(async delegate (CancellationLock.Token token)
            {
                ComicData comic = Comic;
                if (comic == null)
                {
                    return;
                }

                if (image_index >= comic.ImageAspectRatios.Count)
                {
                    return;
                }

                int frame_idx = PageToFrame(image_index + 1, out bool left_side, out int neighbor);
                if (frame_idx < 0)
                {
                    return;
                }

                int page = image_index + 1;
                bool dual = neighbor != -1;

                double aspect_ratio = Math.Max(0, comic.ImageAspectRatios[image_index]);
                if (neighbor != -1)
                {
                    int neighbor_idx = neighbor - 1;
                    if (neighbor_idx >= 0 && neighbor_idx < comic.ImageAspectRatios.Count)
                    {
                        aspect_ratio += Math.Max(0, comic.ImageAspectRatios[neighbor_idx]);
                    }
                }

                double frame_width;
                double frame_height;
                double vertical_padding;
                double horizontal_padding;
                if (aspect_ratio < 1e-3)
                {
                    frame_width = 0;
                    frame_height = 0;
                    vertical_padding = 0;
                    horizontal_padding = 0;
                }
                else
                {
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

                    frame_width = IsVertical ? default_width : default_height * aspect_ratio;
                    frame_height = IsVertical ? default_width / aspect_ratio : default_height;
                    vertical_padding = IsVertical ? default_vertical_padding : 0;
                    horizontal_padding = IsHorizontal ? default_horizontal_padding : 0;
                }

                while (frame_idx >= DataSource.Count)
                {
                    DataSource.Add(new ReaderFrameViewModel());
                }

                ReaderFrameViewModel item = DataSource[frame_idx];

                item.FrameWidth = frame_width;
                item.FrameHeight = frame_height;
                item.FrameMargin = new Thickness(horizontal_padding, vertical_padding, horizontal_padding, vertical_padding);

                if (left_side)
                {
                    if (item.PageL != page)
                    {
                        item.PageL = page;
                        item.ImageL.ImageSet = false;
                    }

                    if (!dual)
                    {
                        item.PageR = -1;
                        item.ImageR.ImageSet = false;
                    }
                }
                else
                {
                    if (item.PageR != page)
                    {
                        item.PageR = page;
                        item.ImageR.ImageSet = false;
                    }

                    if (!dual)
                    {
                        item.PageL = -1;
                        item.ImageL.ImageSet = false;
                    }
                }

                // Wait for the frame to be ready.
                item.Reset();
                item.Notify();
                await item.WaitForReady();

                if (!item.Ready)
                {
                    return;
                }

                await OnContainerLoaded(item);
            });
        }

        public async Task Finalize()
        {
            await m_LoaderLock.LockAsync(delegate (CancellationLock.Token token)
            {
                for (int i = DataSource.Count - 1; i >= 0; --i)
                {
                    ReaderFrameViewModel frame = DataSource[i];

                    if (frame.PageL == -1 && frame.PageR == -1)
                    {
                        DataSource.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }

                AdjustPadding();
            });
        }

        // Modifier - Image Loader
        private CancellationSession _updateImageSession = new CancellationSession();
        private TaskQueue _updateImageQueue = new TaskQueue();

        public async Task<bool> UpdateImages(bool use_final)
        {
            if (!await UpdatePage(use_final))
            {
                return false;
            }

            UpdateImagesInternal();
            return true;
        }

        private void UpdateImagesInternal(bool remove_out_of_view = true)
        {
            if (!IsCurrentReader)
            {
                foreach (ReaderFrameViewModel m in DataSource)
                {
                    if (m.ImageL.ImageSet || m.ImageR.ImageSet)
                    {
                        m.ImageL.ImageSet = false;
                        m.ImageR.ImageSet = false;
                        m.ItemContainer?.CompareAndBind(m);
                    }
                }

                return;
            }

            CancellationSession.Token token = _updateImageSession.CurrentToken;
            int frame = PageToFrame(Page, out _, out _);
            int preload_window_begin = Math.Max(frame - MaxPreloadFramesBefore, 0);
            int preload_window_end = Math.Min(frame + MaxPreloadFramesAfter, DataSource.Count - 1);

            if (remove_out_of_view)
            {
                for (int i = 0; i < DataSource.Count; ++i)
                {
                    if (i < preload_window_begin || i > preload_window_end)
                    {
                        ReaderFrameViewModel m = DataSource[i];
                        if (m.ImageL.ImageSet || m.ImageR.ImageSet)
                        {
                            m.ImageL.ImageSet = false;
                            m.ImageR.ImageSet = false;
                            m.ItemContainer?.CompareAndBind(m);
                        }
                    }
                }
            }

            bool needPreload = false;
            int check_window_begin = Math.Max(frame - MinPreloadFramesBefore, 0);
            int check_window_end = Math.Min(frame + MinPreloadFramesAfter, DataSource.Count - 1);
            for (int i = check_window_begin; i <= check_window_end; ++i)
            {
                ReaderFrameViewModel m = DataSource[i];

                if ((m.PageL > 0 && !m.ImageL.ImageSet) || (m.PageR > 0 && !m.ImageR.ImageSet))
                {
                    needPreload = true;
                    break;
                }
            }

            if (needPreload)
            {
#if DEBUG_LOG_UPDATE_IMAGE
                Log("Loading images (page=" + Page.ToString() + ")");
#endif
                var img_loader_tokens = new List<ImageLoader.Token>();

                void addToLoaderQueue(int i)
                {
                    if (i < 0 || i >= DataSource.Count)
                    {
                        return;
                    }

                    ReaderFrameViewModel m = DataSource[i]; // Stores locally.

                    if (!m.ImageL.ImageSet && m.PageL > 0)
                    {
                        m.ImageL.ImageSet = true;
                        img_loader_tokens.Add(new ImageLoader.Token
                        {
                            SessionToken = token,
                            Index = m.PageL - 1,
                            Comic = Comic,
                            Callback = new LoadImageCallback(m, true)
                        });
                    }

                    if (!m.ImageR.ImageSet && m.PageR > 0)
                    {
                        m.ImageR.ImageSet = true;
                        img_loader_tokens.Add(new ImageLoader.Token
                        {
                            SessionToken = token,
                            Index = m.PageR - 1,
                            Comic = Comic,
                            Callback = new LoadImageCallback(m, false)
                        });
                    }
                }

                addToLoaderQueue(frame);
                int spread = Math.Max(preload_window_end - frame, frame - preload_window_begin);
                for (int i = 1; i <= spread; ++i)
                {
                    if (frame + i <= preload_window_end)
                    {
                        addToLoaderQueue(frame + i);
                    }

                    if (frame - i >= preload_window_begin)
                    {
                        addToLoaderQueue(frame - i);
                    }
                }

                new ImageLoader.Transaction(img_loader_tokens).SetQueue(_updateImageQueue).Commit();
            }
        }

        private class LoadImageCallback : ImageLoader.ICallback
        {
            private readonly ReaderFrameViewModel _viewModel;
            private readonly bool _isLeft;

            public LoadImageCallback(ReaderFrameViewModel viewModel, bool isLeft)
            {
                _viewModel = viewModel;
                _isLeft = isLeft;
            }

            public void OnSuccess(BitmapImage image)
            {
                if (_isLeft)
                {
                    _viewModel.ImageL.Image = image;
                }
                else
                {
                    _viewModel.ImageR.Image = image;
                }

                _viewModel.ItemContainer?.CompareAndBind(_viewModel);
#if DEBUG_LOG_UPDATE_IMAGE
                Log("Page " + m.PageR.ToString() + " loaded");
#endif
            }
        }

        // Modifier - Scrolling
        public async Task<bool> MoveFrame(int increment, string reason)
        {
            if (!await UpdatePage(true))
            {
                return false;
            }

            MoveFrameInternal(increment, !XmlDatabase.Settings.TransitionAnimation, reason);
            return true;
        }

        /// <summary>
        /// We assume that Page is already up-to-date at this moment.
        /// </summary>
        /// <param name="increment"></param>
        /// <param name="disable_animation"></param>
        private bool MoveFrameInternal(int increment, bool disable_animation, string reason)
        {
            if (DataSource.Count == 0)
            {
                return false;
            }

            int frame = PageToFrame(PageFinal, out _, out _);
            frame += increment;
            frame = Math.Min(DataSource.Count - 1, frame);
            frame = Math.Max(0, frame);

            double page = DataSource[frame].Page;
            float? zoom = Zoom > 101f ? 100f : (float?)null;

            return SetScrollViewer2(zoom, page, disable_animation, reason);
        }

        sealed internal class ScrollManager : Utils.BaseTransaction<bool>
        {
            private readonly WeakReference<ReaderViewController> mReader;
            private float? mZoom = null;
            private ZoomType mZoomType = ZoomType.CenterInside;
            private double? mParallelOffset = null;
            private double? mHorizontalOffset = null;
            private double? mVerticalOffset = null;
            private double? mPage = null;
            private bool mDisableAnimation = true;
            private string mReason;

            private ScrollManager(ReaderViewController reader, string reason)
            {
                mReader = new WeakReference<ReaderViewController>(reader);
                mReason = reason;
            }

            public static ScrollManager BeginTransaction(ReaderViewController reader, string reason)
            {
                return new ScrollManager(reader, reason);
            }

            protected override bool CommitImpl()
            {
                if (!mReader.TryGetTarget(out ReaderViewController reader))
                {
                    return false;
                }

                if (!reader.Loaded)
                {
                    return false;
                }

                bool result;
                if (mParallelOffset.HasValue)
                {
                    result = reader.SetScrollViewer1(mZoom, mParallelOffset, mDisableAnimation, mReason);
                }
                else if (mPage.HasValue)
                {
                    result = reader.SetScrollViewer2(mZoom, mPage, mDisableAnimation, mReason);
                }
                else
                {
                    result = reader.SetScrollViewer3(mZoom, mZoomType, mHorizontalOffset, mVerticalOffset, mDisableAnimation, mReason);
                }

                return result;
            }

            public ScrollManager Zoom(float? zoom, ZoomType zoomType = ZoomType.CenterInside)
            {
                mZoom = zoom;
                mZoomType = zoomType;
                return this;
            }

            public ScrollManager ParallelOffset(double? parallel_offset)
            {
                mParallelOffset = parallel_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager HorizontalOffset(double? horizontal_offset)
            {
                mHorizontalOffset = horizontal_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager VerticalOffset(double? vertical_offset)
            {
                mVerticalOffset = vertical_offset;
                OnSetOffset();
                return this;
            }

            public ScrollManager Page(double? page)
            {
                mPage = page;
                OnSetOffset();
                return this;
            }

            public ScrollManager EnableAnimation()
            {
                mDisableAnimation = false;
                return this;
            }

            private void OnSetOffset()
            {
                int checksum = 0;

                if (mPage.HasValue)
                {
                    checksum++;
                }

                if (mParallelOffset.HasValue)
                {
                    checksum++;
                }

                if (mHorizontalOffset.HasValue || mVerticalOffset.HasValue)
                {
                    checksum++;
                }

                if (checksum > 1)
                {
                    throw new Exception("Cannot set offset twice.");
                }
            }
        }

        private bool SetScrollViewer1(float? zoom, double? parallel_offset, bool disable_animation, string reason)
        {
            double? horizontal_offset = IsHorizontal ? parallel_offset : null;
            double? vertical_offset = IsVertical ? parallel_offset : null;

            return SetScrollViewerInternal(new SetScrollViewerContext
            {
                zoom = zoom,
                horizontalOffset = horizontal_offset,
                verticalOffset = vertical_offset,
                disableAnimation = disable_animation,
            }, reason);
        }

        private bool SetScrollViewer2(float? zoom, double? page, bool disable_animation, string reason)
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
                zoom = zoom,
                pageToApplyZoom = page,
                horizontalOffset = horizontal_offset,
                verticalOffset = vertical_offset,
                disableAnimation = disable_animation,
            }, reason);
        }

        private bool SetScrollViewer3(
            float? zoom,
            ZoomType zoomType,
            double? horizontal_offset,
            double? vertical_offset,
            bool disable_animation,
            string reason
        )
        {
            return SetScrollViewerInternal(new SetScrollViewerContext
            {
                zoom = zoom,
                zoomType = zoomType,
                horizontalOffset = horizontal_offset,
                verticalOffset = vertical_offset,
                disableAnimation = disable_animation,
            }, reason);
        }

        private bool SetScrollViewerInternal(SetScrollViewerContext ctx, string reason)
        {
            if (!LoadedFramework)
            {
                return false;
            }

#if DEBUG_LOG_JUMP
            Log("ParamIn: "
                + "Reason=" + reason + ","
                + "Z=" + ctx.zoom.ToString() + ","
                + "H=" + ctx.horizontalOffset.ToString() + ","
                + "V=" + ctx.verticalOffset.ToString() + ","
                + "D=" + ctx.disableAnimation.ToString());
#endif

            SetScrollViewerZoom(ctx, out float? zoom_out);

#if DEBUG_LOG_JUMP
            Log("ParamSetZoom: "
                + "Z=" + ctx.zoom.ToString() + ","
                + "Zo=" + zoom_out.ToString() + ","
                + "H=" + ctx.horizontalOffset.ToString() + ","
                + "V=" + ctx.verticalOffset.ToString());
#endif

            if (!ChangeView(zoom_out, ctx.horizontalOffset, ctx.verticalOffset, ctx.disableAnimation))
            {
                return false;
            }

            if (ctx.pageToApplyZoom.HasValue)
            {
                PageFinal = ToDiscretePage(ctx.pageToApplyZoom.Value);
            }

            Zoom = ctx.zoom.Value;
            AdjustParallelOffset();
            return true;
        }

        private void SetScrollViewerZoom(SetScrollViewerContext ctx, out float? zoom_factor)
        {
            // Calculate zoom coefficient prediction.
            ZoomCoefficientResult zoom_coefficient_new;
            int frame_new;
            {
                int page_new = ctx.pageToApplyZoom.HasValue ? (int)ctx.pageToApplyZoom.Value : Page;
                frame_new = PageToFrame(page_new, out _, out _);
                if (frame_new < 0 || frame_new >= DataSource.Count)
                {
                    frame_new = 0;
                }

                zoom_coefficient_new = ZoomCoefficient(frame_new);
                if (zoom_coefficient_new == null)
                {
                    ctx.zoom = Zoom;
                    zoom_factor = null;
                    return;
                }
            }

            // Calculate zoom in percentage.
            double zoom;
            if (ctx.zoom.HasValue)
            {
                zoom = ctx.zoom.Value;
            }
            else
            {
                int frame = PageToFrame(Page, out _, out _);
                if (frame < 0 || frame >= DataSource.Count)
                {
                    frame = 0;
                }

                ZoomCoefficientResult zoom_coefficient = zoom_coefficient_new;
                if (frame != frame_new)
                {
                    ZoomCoefficientResult zoom_coefficient_test = ZoomCoefficient(frame);
                    if (zoom_coefficient_test != null)
                    {
                        zoom_coefficient = zoom_coefficient_test;
                    }
                }

                zoom = (float)(ZoomFactorFinal / zoom_coefficient.Min());
            }

            if (ctx.zoomType == ZoomType.CenterCrop)
            {
                zoom *= zoom_coefficient_new.Max() / zoom_coefficient_new.Min();
            }

            double maxZoom = Math.Max(MaxZoom, 100 * zoom_coefficient_new.Max() / zoom_coefficient_new.Min());
            zoom = Math.Min(zoom, maxZoom);
            zoom = Math.Max(zoom, MinZoom);
            ctx.zoom = (float)zoom;

            // A zoom factor vary less than 1% will be ignored.
            float zoom_factor_new = (float)(zoom * zoom_coefficient_new.Min());

            if (Math.Abs(zoom_factor_new / ZoomFactorFinal - 1.0f) <= 0.01f)
            {
                zoom_factor = null;
                return;
            }

            zoom_factor = zoom_factor_new;

            // Apply zooming.
            ctx.horizontalOffset ??= HorizontalOffsetFinal;
            ctx.verticalOffset ??= VerticalOffsetFinal;

            ctx.horizontalOffset += ThisScrollViewer.ViewportWidth * 0.5;
            ctx.horizontalOffset *= (float)zoom_factor / ZoomFactorFinal;
            ctx.horizontalOffset -= ThisScrollViewer.ViewportWidth * 0.5;

            ctx.verticalOffset += ThisScrollViewer.ViewportHeight * 0.5;
            ctx.verticalOffset *= (float)zoom_factor / ZoomFactorFinal;
            ctx.verticalOffset -= ThisScrollViewer.ViewportHeight * 0.5;

            ctx.horizontalOffset = Math.Max(0.0, ctx.horizontalOffset.Value);
            ctx.verticalOffset = Math.Max(0.0, ctx.verticalOffset.Value);
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

            if (DataSource[0].ItemContainer?.Container != null)
            {
                double space = PaddingStartFinal * ZoomFactorFinal - ParallelOffsetFinal;
                double image_center_offset = (PaddingStartFinal + FrameParallelLength(0) * 0.5) * ZoomFactorFinal;
                double image_center_to_screen_center = image_center_offset - screen_center_offset;
                movement_forward = Math.Min(space, image_center_to_screen_center);
            }

            if (DataSource[DataSource.Count - 1].ItemContainer?.Container != null)
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
                ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
            else if (movement_backward.HasValue && movement_backward.Value > 0)
            {
                double parallel_offset = ParallelOffsetFinal - movement_backward.Value;
                ChangeView(null, HorizontalVal(parallel_offset, null),
                    VerticalVal(parallel_offset, null), false);
            }
            else
            {
                return;
            }

            m_manipulation_disabled = true;
        }

        private void AdjustPadding()
        {
            if (!LoadedFramework)
            {
                return;
            }

            if (DataSource.Count == 0)
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

                if (DataSource[frame_idx].ItemContainer?.Container == null)
                {
                    break;
                }

                ZoomCoefficientResult zoom_coefficient = ZoomCoefficient(frame_idx);
                if (zoom_coefficient == null)
                {
                    break;
                }

                double zoom_factor = MinZoom * zoom_coefficient.Min();
                double inner_length = ViewportParallelLength / zoom_factor;
                padding_start = (inner_length - FrameParallelLength(frame_idx)) / 2;
                padding_start = Math.Max(0.0, padding_start);
            } while (false);

            double padding_end = PaddingEndFinal;
            do
            {
                int frame_idx = DataSource.Count - 1;

                if (DataSource[frame_idx].ItemContainer?.Container == null)
                {
                    break;
                }

                ZoomCoefficientResult zoom_coefficient = ZoomCoefficient(frame_idx);
                if (zoom_coefficient == null)
                {
                    break;
                }

                double zoom_factor = MinZoom * zoom_coefficient.Min();
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

        // Events - Pointer
        public async Task OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(null);
            int delta = -pt.Properties.MouseWheelDelta / 120;

            if (IsContinuous || Zoom > 105)
            {
                // Continuous scrolling.
                ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingPointerWheel")
                    .ParallelOffset(ParallelOffsetFinal + delta * 140.0)
                    .EnableAnimation()
                    .Commit();
            }
            else
            {
                // Page turning.
                await MoveFrame(delta, "PageTuringUsingPointerWheel");
            }

            m_manipulation_disabled = true;
            e.Handled = true;
        }

        // Events - Manipulation
        private bool m_manipulation_disabled = false;

        public void OnReaderManipulationStarted(ManipulationStartedEventArgs e)
        {
            m_manipulation_disabled = false;

#if DEBUG_LOG_MANIPULATION
            Log("Manipulation started");
#endif
        }

        public void OnReaderManipulationUpdated(ManipulationUpdatedEventArgs e)
        {
            if (m_manipulation_disabled)
            {
                return;
            }

            double dx = e.Delta.Translation.X;
            double dy = e.Delta.Translation.Y;
            float scale = e.Delta.Scale;

            if (IsHorizontal && !IsLeftToRight)
            {
                dx = -dx;
            }

            float? zoom = null;

            if (Math.Abs(scale - 1.0f) > 0.01f)
            {
                zoom = Zoom * scale;
            }

            ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingManipulation")
                .Zoom(zoom)
                .HorizontalOffset(HorizontalOffsetFinal - dx)
                .VerticalOffset(VerticalOffsetFinal - dy)
                .EnableAnimation()
                .Commit();
        }

        public async Task OnReaderManipulationCompleted(ManipulationCompletedEventArgs e)
        {
            if (IsContinuous || Zoom >= ForceContinuousZoomThreshold)
            {
                return;
            }

            double velocity = IsVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

            if (IsHorizontal && !IsLeftToRight)
            {
                velocity = -velocity;
            }

            if (velocity > 1.0)
            {
                await MoveFrame(-1, "MoveToLastPageUsingManipulation");
            }
            else if (velocity < -1.0)
            {
                await MoveFrame(1, "MoveToNextPageUsingManipulation");
            }

#if DEBUG_LOG_MANIPULATION
            Log("Manipulation completed, V=" + velocity.ToString());
#endif
        }

        // Events - Common
        private readonly Utils.CancellationLock m_ContainerLoadedLock = new Utils.CancellationLock();
        private readonly Utils.CancellationLock m_PageRearrangeLock = new Utils.CancellationLock();

        public async Task OnContainerLoaded(ReaderFrameViewModel ctx)
        {
            if (ctx == null || ctx.ItemContainer?.Container == null)
            {
                System.Diagnostics.Debug.Assert(false);
                return;
            }

            await m_ContainerLoadedLock.LockAsync(async delegate (CancellationLock.Token token)
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
                    await Threading.RunInMainThread(delegate
                    {
                        SetScrollViewer1(Zoom, null, true, "AdjustZooming");
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
                    await Threading.RunInMainThread(delegate
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
                    LoadedInitialPage = SetScrollViewer2(null, InitialPage, true, "JumpToInitialPage");
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
                        UpdateImagesInternal();
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
            });
        }

        public async Task<bool> OnViewChanged(bool final)
        {
            if (!Loaded)
            {
                return false;
            }

            if (!IsCurrentReader)
            {
                // Clear images.
                UpdateImagesInternal();
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
                SetScrollViewer1(null, null, false, "AdjustInnerStateAfterViewChanged");

                if (!IsContinuous && Zoom < ForceContinuousZoomThreshold)
                {
                    // Stick our view to the center of two pages.
                    MoveFrameInternal(0, false, "StickToCenter");
                }

#if DEBUG_LOG_VIEW_CHANGE
                Log("ViewChanged:"
                    + " Z=" + ZoomFactorFinal.ToString()
                    + ",H=" + HorizontalOffsetFinal.ToString()
                    + ",V=" + VerticalOffsetFinal.ToString()
                    + ",P=" + PageSource.ToString());
#endif
            }

            UpdateImagesInternal(final);
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
                await m_PageRearrangeLock.LockAsync(async delegate (CancellationLock.Token token)
                {
                    // Set reader status to Loading.
                    _viewModel.ReaderStatusLiveData.Emit(ReaderPageViewModel.ReaderStatusEnum.Loading);

                    // Save previous states.
                    double page = PageSource;
                    float zoom = Math.Min(Zoom, 100f);

                    // Update Frames.
                    ResetFrames();

                    for (int i = 0; i < PageCount; ++i)
                    {
                        if (token.CancellationRequested)
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
                    SetScrollViewer2(zoom, page, false, "JumpToPreviousPageAfterRearrange");

                    // Update images.
                    await UpdateImages(true);

                    // Recover reader status.
                    _viewModel.ReaderStatusLiveData.Emit(ReaderPageViewModel.ReaderStatusEnum.Working);
                });
            });
        }

        // Internal - Variables
        private readonly string _name;
        private readonly ReaderPageViewModel _viewModel;

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

            Utils.Debug.Log("Reader(" + _name + "): " + text + ".");
        }
    }
}
