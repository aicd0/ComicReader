// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Imaging;
using ComicReader.Common.Threading;
using ComicReader.Data;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace ComicReader.Views.Reader;

internal partial class ReaderView : UserControl
{
    //
    // Constants
    //

    private const string TAG = nameof(ReaderView);
    private const float MAX_ZOOM = 250F;
    private const float MIN_ZOOM = 50F;
    private const float FORCE_CONTINUOUS_ZOOM_THRESHOLD = 105F;
    private const int PRELOAD_FRAMES_BEFORE = 10;
    private const int PRELOAD_FRAMES_AFTER = 10;

    //
    // Variables
    //

    private bool _isLoaded;
    private ReaderState _state = ReaderState.Idle;
    private bool _isVertical = true;
    private bool _isContinuous = true;
    private bool _isVisible = true;
    private bool _isLeftToRight = true;
    private PageArrangementType _pageArrangement = PageArrangementType.Single;
    private bool _uiStateUpdatedVisibility = true;
    private bool _uiStateUpdatedOrientation = true;
    private bool _uiStateUpdatedContinuous = true;
    private bool _uiStateUpdatedFlowDirection = true;
    private bool _uiStateUpdatedPageArrangement = true;
    private bool _postUiStateUpdated = false;

    private bool _isFirstFrameLoaded = false;
    private bool _isFirstFrameActionPerformed = false;
    private bool _isInitialFrameLoaded = false;
    private bool _isInitialFrameActionPerformed = false;
    private bool _isLastFrameLoaded = false;
    private bool _isLastFrameActionPerformed = false;

    private bool _tapPending = false;
    private bool _tapCancelled = false;
    private bool _manipulationDisabled = false;
    private readonly UIElement _gestureReference;
    private readonly GestureHandler _gestureHandler;
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();

    private double _initialPage = 0.0;
    private List<IImageSource> _originalDataModel;
    private readonly ITaskDispatcher _loadInfoDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadInfoQueue");
    private readonly ITaskDispatcher _loadImageDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private readonly ReaderFrameManager _frameManager = new();
    private readonly ReaderImagePool _imagePool;
    private readonly Dictionary<int, ImageDataModel> _dataModel = [];
    private readonly CancellationSession _dataModelSession;
    private readonly CancellationSession _loadImageSession;

    private ObservableCollection<ReaderFrameViewModel> FrameDataSource { get; } = [];

    //
    // Constructor
    //

    public ReaderView()
    {
        InitializeComponent();

        Loaded += OnLoadedOrUnloaded;
        Unloaded += OnLoadedOrUnloaded;

        _gestureReference = this;
        _gestureHandler = new(this);
        _gestureRecognizer.SetHandler(_gestureHandler);

        _dataModelSession = new();
        _loadImageSession = new();
        _imagePool = new(_loadImageSession, _loadImageDispatcher);
    }

    //
    // Public Interfaces
    //

    public delegate void ReaderEventTappedEventHandler(ReaderView sender);
    public event ReaderEventTappedEventHandler ReaderEventTapped;

    public delegate void ReaderEventPageChangedEventHandler(ReaderView sender, bool isIntermediate);
    public event ReaderEventPageChangedEventHandler ReaderEventPageChanged;

    public delegate void ReaderEventReaderStateChangeHandler(ReaderView sender, ReaderState state);
    public event ReaderEventReaderStateChangeHandler ReaderEventReaderStateChanged;

    public int PageCount { get; private set; } = 0;
    public double CurrentPage { get; private set; } = 0.0;
    private int CurrentPageInt => ToDiscretePage(CurrentPage);
    public int CurrentPageDisplay
    {
        get
        {
            int page = CurrentPageInt;
            if (page <= 0)
            {
                page = (int)Math.Round(_initialPage);
            }
            return page;
        }
    }
    public bool IsLastPage => PageToFrame(CurrentPageDisplay, out _, out _) >= FrameDataSource.Count - 1;
    public bool IsVertical => _isVertical;

    public void SetIsVertical(bool isVertical)
    {
        if (isVertical == _isVertical)
        {
            return;
        }

        _isVertical = isVertical;
        _uiStateUpdatedOrientation = true;
        _uiStateUpdatedFlowDirection = true;
        UpdateUI();
    }

    public void SetIsContinuous(bool isContinuous)
    {
        if (isContinuous == _isContinuous)
        {
            return;
        }

        _isContinuous = isContinuous;
        _uiStateUpdatedContinuous = true;
        UpdateUI();
    }

    public void SetFlowDirection(bool isLeftToRight)
    {
        if (isLeftToRight == _isLeftToRight)
        {
            return;
        }

        _isLeftToRight = isLeftToRight;
        _uiStateUpdatedFlowDirection = true;
        UpdateUI();
    }

    public void SetVisibility(bool visible)
    {
        if (visible == _isVisible)
        {
            return;
        }

        _isVisible = visible;
        _uiStateUpdatedVisibility = true;
        UpdateUI();
    }

    public void SetPageArrangement(PageArrangementType type)
    {
        if (_pageArrangement == type)
        {
            return;
        }

        _pageArrangement = type;
        _uiStateUpdatedPageArrangement = true;
        UpdateUI();
    }

    public void SetInitialPage(double page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        _initialPage = page;
    }

    public void StartLoadingImages(List<IImageSource> images)
    {
        _originalDataModel = new List<IImageSource>(images);
        Reload(_originalDataModel, true);
    }

    //
    // Loader
    //

    private void Reload(List<IImageSource> images, bool clear)
    {
        // Refresh token
        _dataModelSession.Next();
        CancellationSession.IToken token = _dataModelSession.Token;

        // Update internal states
        _dataModel.Clear();
        PageCount = images.Count;

        int lastFrameIndex = PageToFrame(PageCount, out bool _, out int _);

        for (int i = FrameDataSource.Count - 1; i > lastFrameIndex; --i)
        {
            FrameDataSource.RemoveAt(i);
        }

        if (clear)
        {
            _loadImageSession.Next();
            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                ReaderFrameViewModel item = FrameDataSource[i];
                item.PageL = -1;
                item.PageR = -1;
                item.LeftImageHolder.SetImage(null);
                item.RightImageHolder.SetImage(null);
            }
        }

        SCClearFinalVal("Reload");

        // Reset loader
        int initialFrameIndex = PageToFrame(ToDiscretePage(_initialPage), out bool _, out int _);
        Log("Reload", $"IP={_initialPage},IF={initialFrameIndex},LP={PageCount},LF={lastFrameIndex}");
        ResetLoader();
        _frameManager.ResetReadyIndex();
        _frameManager.SetFrameReadyHandler(delegate (int index)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (index == 0)
            {
                _isFirstFrameLoaded = true;
            }
            if (index == initialFrameIndex)
            {
                _isInitialFrameLoaded = true;
            }
            if (index == lastFrameIndex)
            {
                _isLastFrameLoaded = true;
            }
            UpdateLoader($"FrameReady,i={index}");
        });

        // Dispatch event
        DispatchReaderStateChangeEvent(ReaderState.Loading);

        _loadInfoDispatcher.Submit("ReaderLoadImageInfo", delegate
        {
            void dispatchToMainThread(List<Tuple<int, double, IImageSource>> pendingList)
            {
                if (pendingList.Count == 0)
                {
                    return;
                }
                List<Tuple<int, double, IImageSource>> pendingListCopy = new(pendingList);
                pendingList.Clear();
                _ = MainThreadUtils.RunInMainThread(delegate
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    foreach (Tuple<int, double, IImageSource> item in pendingListCopy)
                    {
                        int index = item.Item1;
                        SetImageData(index, item.Item2, item.Item3);
                    }
                });
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            List<Tuple<int, double, IImageSource>> pendingList = new();

            for (int i = 0; i < images.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                IImageSource image = images[i];
                ImageInfoManager.ImageInfo imageInfo = ImageInfoManager.GetImageInfo(image);
                double aspectRatio = 0.0;
                if (imageInfo != null && imageInfo.Width > 0 && imageInfo.Height > 0)
                {
                    aspectRatio = (double)imageInfo.Width / imageInfo.Height;
                }

                pendingList.Add(new Tuple<int, double, IImageSource>(i, aspectRatio, image));

                if (stopwatch.LapSpan().TotalMilliseconds > 500)
                {
                    stopwatch.Lap();
                    dispatchToMainThread(pendingList);
                }
            }

            dispatchToMainThread(pendingList);
        });
    }

    private void ResetLoader()
    {
        Log("Load", "Reset");
        _isFirstFrameLoaded = false;
        _isFirstFrameActionPerformed = false;
        _isInitialFrameLoaded = false;
        _isInitialFrameActionPerformed = false;
        _isLastFrameLoaded = false;
        _isLastFrameActionPerformed = false;
    }

    private void UpdateLoader(string reason)
    {
        if (!_isLoaded)
        {
            return;
        }

        Log("Load", reason);

        bool needAdjustPadding = false;

        if (_isFirstFrameLoaded && !_isFirstFrameActionPerformed)
        {
            Log("Load", "FirstFrame");
            _isFirstFrameActionPerformed = true;
            needAdjustPadding = true;
        }

        if (_isLastFrameLoaded && !_isLastFrameActionPerformed)
        {
            Log("Load", "LastFrame");
            _isLastFrameActionPerformed = true;
            needAdjustPadding = true;
        }

        if (needAdjustPadding)
        {
            AdjustPadding();
        }

        bool needDispatchReadyState = false;

        if (_isInitialFrameLoaded && !_isInitialFrameActionPerformed)
        {
            Log("Load", "InitialFrame");
            _isInitialFrameActionPerformed = true;

            PostToCurrentThread(delegate
            {
                ScrollResult scrollResult = SetScrollViewer2(_zoom, _initialPage, true, "JumpToInitialPage");
                Log("Load", $"InitialFrameScroll (result={scrollResult})");
                if (scrollResult == ScrollResult.TooClose)
                {
                    UpdateImages("InitialFrameLoaded", true);
                }
            });

            needDispatchReadyState = true;
        }

        if (needDispatchReadyState)
        {
            DispatchReaderStateChangeEvent(ReaderState.Ready);
        }
    }

    //
    // UI
    //

    private void UpdateUI()
    {
        if (!_postUiStateUpdated)
        {
            _postUiStateUpdated = true;
            PostToCurrentThread(delegate
            {
                _postUiStateUpdated = false;
                UpdateUIInternal();
            });
        }
    }

    private void UpdateUIInternal()
    {
        if (!_isLoaded)
        {
            return;
        }

        bool needReload = false;

        if (_uiStateUpdatedVisibility)
        {
            _uiStateUpdatedVisibility = false;
            bool isVisible = _isVisible;
            SvReader.IsEnabled = isVisible;
            SvReader.IsHitTestVisible = isVisible;
            SvReader.Opacity = isVisible ? 1 : 0;
        }

        if (_uiStateUpdatedOrientation)
        {
            _uiStateUpdatedOrientation = false;
            bool isVertical = _isVertical;
            SvReader.VerticalScrollBarVisibility = isVertical ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            SvReader.HorizontalScrollBarVisibility = isVertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Visible;
            SvReader.VerticalScrollMode = isVertical ? ScrollMode.Enabled : ScrollMode.Disabled;
            GReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            GReader.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            LvReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            LvReader.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            LvReader.ItemContainerStyle = (Style)Resources[isVertical ? "VerticalReaderListViewItemStyle" : "HorizontalReaderListViewItemStyle"];
            LvReader.ItemTemplate = (DataTemplate)Resources[isVertical ? "VerticalReaderListViewItemTemplate" : "HorizontalReaderListViewItemTemplate"];
            LvReader.ItemsPanel = (ItemsPanelTemplate)Resources[isVertical ? "VerticalReaderListViewItemPanelTemplate" : "HorizontalReaderListViewItemPanelTemplate"];
            FrameDataSource.Clear(); // force IsModelInstanceUpdateToDate set to false
            needReload = true;
        }

        if (_uiStateUpdatedFlowDirection)
        {
            _uiStateUpdatedFlowDirection = false;
            if (!_isVertical)
            {
                SvReader.FlowDirection = _isLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }
        }

        if (_uiStateUpdatedContinuous)
        {
            _uiStateUpdatedContinuous = false;
            _gestureRecognizer.AutoProcessInertia = _isContinuous;
            if (!_isVertical)
            {
                needReload = true;
            }
        }

        if (_uiStateUpdatedPageArrangement)
        {
            _uiStateUpdatedPageArrangement = false;
            needReload = true;
        }

        if (needReload && _originalDataModel != null)
        {
            _initialPage = CurrentPage;
            Reload(_originalDataModel, false);
        }
    }

    private void SetImageData(int index, double aspectRatio, IImageSource source)
    {
        DebugUtils.Assert(index >= 0);
        DebugUtils.Assert(double.IsFinite(aspectRatio));
        DebugUtils.Assert(source != null);

        var model = new ImageDataModel
        {
            AspectRatio = aspectRatio,
            ImageSource = source,
        };
        _dataModel[index] = model;

        int frameIndex = PageToFrame(index + 1, out bool leftSide, out int neighbor);

        DebugUtils.Assert(frameIndex >= 0);
        DebugUtils.Assert(neighbor >= -1);

        int page = index + 1;
        bool dual = neighbor != -1;

        if (neighbor != -1)
        {
            int neighborIndex = neighbor - 1;
            if (_dataModel.TryGetValue(neighborIndex, out ImageDataModel neighborModel))
            {
                aspectRatio += neighborModel.AspectRatio;
            }
            else
            {
                return;
            }
        }

        double frameWidth;
        double frameHeight;
        double verticalPadding;
        double horizontalPadding;
        if (aspectRatio < 1e-3)
        {
            frameWidth = 0;
            frameHeight = 0;
            verticalPadding = 0;
            horizontalPadding = 0;
        }
        else
        {
            double defaultWidth = 500.0;
            double defaultHeight = 300.0;
            double defaultVerticalPadding = 10.0;
            double defaultHorizontalPadding = 100.0;
            if (dual)
            {
                defaultWidth *= 2;
            }

            if (_isContinuous)
            {
                defaultHorizontalPadding = 10.0;
            }

            frameWidth = _isVertical ? defaultWidth : defaultHeight * aspectRatio;
            frameHeight = _isVertical ? defaultWidth / aspectRatio : defaultHeight;
            verticalPadding = _isVertical ? defaultVerticalPadding : 0;
            horizontalPadding = _isVertical ? 0 : defaultHorizontalPadding;
        }

        while (frameIndex >= FrameDataSource.Count)
        {
            _frameManager.MarkModelInstanceOutOfDate(frameIndex, "DataAppended");
            FrameDataSource.Add(new ReaderFrameViewModel(_imagePool));
        }
        ReaderFrameViewModel item = FrameDataSource[frameIndex];

        DebugUtils.Assert(double.IsFinite(frameWidth));
        DebugUtils.Assert(double.IsFinite(frameHeight));
        DebugUtils.Assert(double.IsFinite(horizontalPadding));
        DebugUtils.Assert(double.IsFinite(verticalPadding));

        item.FrameWidth = frameWidth;
        item.FrameHeight = frameHeight;
        item.FrameMargin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);

        if (leftSide)
        {
            item.PageL = page;
            item.PageR = neighbor;
        }
        else
        {
            item.PageR = page;
            item.PageL = neighbor;
        }

        if (item.PageL != -1)
        {
            item.LeftImageSource = _dataModel.GetValueOrDefault(item.PageL - 1, null)?.ImageSource;
            DebugUtils.Assert(item.LeftImageSource != null);
        }
        else
        {
            item.LeftImageSource = null;
        }

        if (item.PageR != -1)
        {
            item.RightImageSource = _dataModel.GetValueOrDefault(item.PageR - 1, null)?.ImageSource;
            DebugUtils.Assert(item.RightImageSource != null);
        }
        else
        {
            item.RightImageSource = null;
        }

        item.RebindEntireViewModel();
        _frameManager.MarkModelContentUpdateToDate(frameIndex, "ViewBindByProperty");
    }

    private bool UpdatePage()
    {
        if (!_isInitialFrameLoaded)
        {
            DebugUtils.Assert(false);
            return false;
        }

        double offset;
        {
            double parallelOffset = ParallelOffset;
            double zoomFactor = ZoomFactor;
            offset = (parallelOffset + ViewportParallelLength * 0.5) / zoomFactor;
        }

        if (FrameDataSource.Count == 0)
        {
            DebugUtils.Assert(false);
            return false;
        }

        // Locate current frame using binary search
        int begin = 0;
        int end = FrameDataSource.Count - 1;
        FrameOffsetData frameOffsets = null;

        while (true)
        {
            int i = (begin + end + 1) / 2;
            FrameOffsetData offsets = FrameOffset(i);

            if (offsets == null)
            {
                if (begin >= end)
                {
                    frameOffsets = null;
                    break;
                }
                end = i - 1;
                continue;
            }

            if (offsets.ParallelBegin < offset)
            {
                begin = i;
                frameOffsets = offsets;
                if (begin >= end)
                {
                    break;
                }
            }
            else
            {
                if (begin >= end)
                {
                    frameOffsets ??= FrameOffset(begin);
                    break;
                }
                end = i - 1;
            }
        }

        if (frameOffsets == null)
        {
            return false;
        }

        ReaderFrameViewModel frame = FrameDataSource[begin];

        int pageMin;
        int pageMax;

        if (frame.PageL == -1 && frame.PageR == -1)
        {
            DebugUtils.Assert(false);
            return false;
        }

        if (frame.PageL == -1)
        {
            pageMin = pageMax = frame.PageR;
        }
        else if (frame.PageR == -1)
        {
            pageMin = pageMax = frame.PageL;
        }
        else
        {
            pageMin = Math.Min(frame.PageL, frame.PageR);
            pageMax = Math.Max(frame.PageL, frame.PageR);
        }

        double page;

        if (offset < frameOffsets.ParallelCenter)
        {
            double pageFrac = (offset - frameOffsets.ParallelBegin) / (frameOffsets.ParallelCenter - frameOffsets.ParallelBegin);
            page = pageMin - 0.5 + pageFrac * 0.5;
        }
        else
        {
            double pageFrac = (offset - frameOffsets.ParallelCenter) / (frameOffsets.ParallelEnd - frameOffsets.ParallelCenter);
            page = pageMax + pageFrac * 0.5;
        }

        CurrentPage = page;

        Log("PageUpdated",
            $"P={CurrentPage}," +
            $"PO={ParallelOffset}," +
            $"POF={SCParallelOffsetFinal}," +
            $"Z={ZoomFactor}");

        return true;
    }

    private void UpdateImages(string reason, bool final)
    {
        Log("LoadImage", $"reason={reason},final={final}");

        int frame = PageToFrame(CurrentPageInt, out _, out _);
        int preloadWindowBegin = Math.Max(frame - PRELOAD_FRAMES_BEFORE, 0);
        int preloadWindowEnd = Math.Min(frame + PRELOAD_FRAMES_AFTER, FrameDataSource.Count - 1);

        if (final)
        {
            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                ReaderFrameViewModel model = FrameDataSource[i];
                if (i < preloadWindowBegin || i > preloadWindowEnd)
                {
                    model.LeftImageHolder.SetImage(null);
                    model.RightImageHolder.SetImage(null);
                }
                else
                {
                    UpdateImageDecodeSize(model);
                }
            }
        }

        void addToLoaderQueue(int i)
        {
            if (i < 0 || i >= FrameDataSource.Count)
            {
                return;
            }

            ReaderFrameViewModel model = FrameDataSource[i];
            model.LeftImageHolder.SetImage(model.LeftImageSource);
            model.RightImageHolder.SetImage(model.RightImageSource);
        }

        int spread = Math.Max(preloadWindowEnd - frame, frame - preloadWindowBegin);
        addToLoaderQueue(frame);
        for (int i = 1; i <= spread; ++i)
        {
            if (frame + i <= preloadWindowEnd)
            {
                addToLoaderQueue(frame + i);
            }

            if (frame - i >= preloadWindowBegin)
            {
                addToLoaderQueue(frame - i);
            }
        }

        _imagePool.FlushRequests();
    }

    private void UpdateImageDecodeSize(ReaderFrameViewModel model)
    {
        if (!AppData.AntiAliasingEnabled)
        {
            return;
        }

        double frameHeight = model.FrameHeight * SCZoomFactorFinal;

        void applyDecodeSize(BitmapImage image)
        {
            if (image == null || image.PixelHeight <= 0 || image.PixelWidth <= 0)
            {
                return;
            }

            double frameWidth = frameHeight * image.PixelWidth / image.PixelHeight;
            double multiplication = 1.2 * DisplayUtils.GetRawPixelPerPixel();
            int decodeHeight = (int)Math.Round(frameHeight * multiplication);
            int decodeWidth = (int)Math.Round(frameWidth * multiplication);

            if (decodeHeight * decodeWidth >= image.PixelHeight * image.PixelWidth)
            {
                decodeHeight = image.PixelHeight;
                decodeWidth = image.PixelWidth;
            }

            if (image.DecodePixelHeight == decodeHeight && image.DecodePixelWidth == decodeWidth)
            {
                return;
            }

            image.DecodePixelWidth = decodeWidth;
            image.DecodePixelHeight = decodeHeight;
        }

        applyDecodeSize(model.ImageLeft);
        applyDecodeSize(model.ImageRight);
    }

    //
    // Load/Unload Handlers
    //

    private void OnLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void OnReaderListViewLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void OnReaderScrollViewerLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void UpdateLoadedState()
    {
        bool viewLoaded = IsLoaded;
        bool lvLoaded = LvReader != null && LvReader.IsLoaded;
        bool svLoaded = SvReader != null && SvReader.IsLoaded;
        bool isLoaded = viewLoaded && lvLoaded && svLoaded;

        if (_isLoaded == isLoaded)
        {
            return;
        }
        _isLoaded = isLoaded;

        if (isLoaded)
        {
            UpdateUI();
            UpdateLoader("Loaded");
        }
        else
        {
            _dataModelSession.Next();
            _loadImageSession.Next();
        }
    }

    //
    // Size Change Event Handlers
    //

    private void OnReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        AdjustPadding();
    }

    private void OnReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (OnViewChanged(!e.IsIntermediate))
        {
            ReaderEventPageChanged?.Invoke(this, e.IsIntermediate);
        }
    }

    private bool OnViewChanged(bool final)
    {
        if (!_isInitialFrameLoaded)
        {
            return false;
        }

        if (!UpdatePage())
        {
            return false;
        }

        if (final)
        {
            Log("ViewChanged", $"Z={ZoomFactor}"
                + $",H={HorizontalOffset}"
                + $",V={VerticalOffset}"
                + $",P={CurrentPage}");

            SCClearFinalVal("ViewChanged");

            // Notify the scroll viewer to update its inner states.
            SetScrollViewer1(null, null, false, "AdjustInnerStateAfterViewChanged");

            if (!_isContinuous && _zoom < FORCE_CONTINUOUS_ZOOM_THRESHOLD)
            {
                // Stick our view to the center of two pages.
                MoveFrameInternal(0, false, "StickToCenter");
            }
        }

        UpdateImages("ViewChanged", final);
        return true;
    }

    //
    // Content Change Event Handlers
    //

    private void OnReaderContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderFrameViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderFrame;

        if (viewHolder == null)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            _frameManager.MarkViewNotReady(args.ItemIndex, "ViewRecycled");
            viewHolder.SetReadyStateChangeHandler(null);
            viewHolder.SetImageChangeHandler(null);
            viewHolder.Bind(null);
        }
        else
        {
            int index = args.ItemIndex;

            viewHolder.SetReadyStateChangeHandler(delegate (FrameworkElement container, bool isReady, string reason)
            {
                if (isReady)
                {
                    _frameManager.MarkViewReady(index, container, "ViewReady");
                }
                else
                {
                    _frameManager.MarkViewNotReady(index, "ViewNotReady");
                }
            });
            viewHolder.SetImageChangeHandler(UpdateImageDecodeSize);

            viewHolder.Bind(item);
            _frameManager.MarkModelInstanceUpdateToDate(index, "ViewBindByContainer");
        }
    }

    //
    // Key Down Event Handlers
    //

    private void OnReaderKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled = true;
        switch (e.Key)
        {
            case VirtualKey.Right:
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrame(-1, "JumpToPreviousPageUsingRightKey");
                }
                else
                {
                    MoveFrame(1, "JumpToNextPageUsingRightKey");
                }

                break;

            case VirtualKey.Left:
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrame(1, "JumpToNextPageUsingLeftKey");
                }
                else
                {
                    MoveFrame(-1, "JumpToPreviousPageUsingLeftKey");
                }

                break;

            case VirtualKey.Up:
                MoveFrame(-1, "JumpToPerviousPageUsingUpKey");
                break;

            case VirtualKey.Down:
                MoveFrame(1, "JumpToNextPageUsingDownKey");
                break;

            case VirtualKey.PageUp:
                MoveFrame(-1, "JumpToPerviousPageUsingPgUpKey");
                break;

            case VirtualKey.PageDown:
                MoveFrame(1, "JumpToNextPageUsingPgDownKey");
                break;

            case VirtualKey.Home:
                ScrollManager.BeginTransaction(this, "JumpToFirstPageUsingHomeKey")
                    .Page(1)
                    .Commit();
                break;

            case VirtualKey.End:
                ScrollManager.BeginTransaction(this, "JumpToLastPageUsingEndKey")
                    .Page(PageCount)
                    .Commit();
                break;

            case VirtualKey.Space:
                MoveFrame(1, "JumpToNextPageUsingSpaceKey");
                break;

            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    //
    // Pointer Event Handlers
    //

    private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
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

    private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _gestureRecognizer.ProcessMoveEvents(e.GetIntermediatePoints(_gestureReference));
#if DEBUG_LOG_POINTER
        //Log("Pointer moved");
#endif
    }

    private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        (sender as UIElement).CapturePointer(e.Pointer);
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
        _gestureRecognizer.ProcessDownEvent(pointer_point);
#if DEBUG_LOG_POINTER
        Log("Pointer pressed");
#endif
    }

    private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
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

    private void OnReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Ctrl key down indicates the user is zooming the page. In that case we shouldn't handle the event.
        CoreVirtualKeyStates ctrl_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

        if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
        {
            return;
        }

        OnReaderScrollViewerPointerWheelChanged(e);
    }

    private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
    {
        OnReaderManipulationStarted(e);
    }

    private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
    {
        OnReaderManipulationUpdated(e);
    }

    private void OnReaderManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        OnReaderManipulationCompleted(e);
    }

    private void OnReaderManipulationStarted(ManipulationStartedEventArgs e)
    {
        _manipulationDisabled = false;

#if DEBUG_LOG_MANIPULATION
        Log("Manipulation started");
#endif
    }

    private void OnReaderManipulationUpdated(ManipulationUpdatedEventArgs e)
    {
        if (_manipulationDisabled)
        {
            return;
        }

        double dx = e.Delta.Translation.X;
        double dy = e.Delta.Translation.Y;
        float scale = e.Delta.Scale;

        if (!_isVertical && !_isLeftToRight)
        {
            dx = -dx;
        }

        float? zoom = null;

        if (Math.Abs(scale - 1.0f) > 0.01f)
        {
            zoom = _zoom * scale;
        }

        ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingManipulation")
            .Zoom(zoom)
            .HorizontalOffset(SCHorizontalOffsetFinal - dx)
            .VerticalOffset(SCVerticalOffsetFinal - dy)
            .EnableAnimation()
            .Commit();
    }

    private void OnReaderManipulationCompleted(ManipulationCompletedEventArgs e)
    {
        if (_isContinuous || _zoom >= FORCE_CONTINUOUS_ZOOM_THRESHOLD)
        {
            return;
        }

        double velocity = IsVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

        if (!_isVertical && !_isLeftToRight)
        {
            velocity = -velocity;
        }

        if (velocity > 1.0)
        {
            MoveFrame(-1, "MoveToLastPageUsingManipulation");
        }
        else if (velocity < -1.0)
        {
            MoveFrame(1, "MoveToNextPageUsingManipulation");
        }

#if DEBUG_LOG_MANIPULATION
        Log("Manipulation completed, V=" + velocity.ToString());
#endif
    }

    private void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / 120;

        if (_isContinuous || _zoom > 105)
        {
            // Continuous scrolling.
            ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingPointerWheel")
                .ParallelOffset(SCParallelOffsetFinal + delta * 140.0)
                .EnableAnimation()
                .Commit();
        }
        else
        {
            // Page turning.
            MoveFrame(delta, "PageTurningUsingPointerWheel");
        }

        _manipulationDisabled = true;
        e.Handled = true;
    }

    private void OnReaderTapped(object sender, TappedEventArgs e)
    {
        if (e.TapCount == 1)
        {
            if (_tapPending)
            {
                return;
            }

            _tapPending = true;
            _tapCancelled = false;
            C0.Run(async delegate
            {
                await Task.Delay(100);
                _tapPending = false;
                if (_tapCancelled)
                {
                    return;
                }

                ReaderEventTapped?.Invoke(this);
            });
        }
        else if (e.TapCount == 2)
        {
            _tapCancelled = true;
            if (Math.Abs(_zoom - 100) <= 1)
            {
                ScrollManager.BeginTransaction(this, "FitScreenUsingCenterCrop")
                    .Zoom(100, ZoomType.CenterCrop)
                    .EnableAnimation()
                    .Commit();
            }
            else
            {
                ScrollManager.BeginTransaction(this, "FitScreenUsingCenterInside")
                    .Zoom(100)
                    .EnableAnimation()
                    .Commit();
            }
        }
    }

    //
    // Scroll Controller
    //

    private float _zoom = 100f;
    private bool _finalValueSynced = false;

    private ScrollViewer ThisScrollViewer => SvReader;
    private ListView ThisListView => LvReader;
    private float ZoomFactor => ThisScrollViewer.ZoomFactor;
    private double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
    private double VerticalOffset => ThisScrollViewer.VerticalOffset;
    private double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
    private double ViewportParallelLength => IsVertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
    private double ViewportPerpendicularLength => IsVertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
    private double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;

    private int _SCCurrentPageFinal;
    private int SCCurrentPageFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCCurrentPageFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCCurrentPageFinal = value;
        }
    }

    private float _SCZoomFactorFinal;
    private float SCZoomFactorFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCZoomFactorFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCZoomFactorFinal = value;
        }
    }

    private double _SCHorizontalOffsetFinal;
    private double SCHorizontalOffsetFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCHorizontalOffsetFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCHorizontalOffsetFinal = value;
        }
    }

    private double _SCVerticalOffsetFinal;
    private double SCVerticalOffsetFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCVerticalOffsetFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCVerticalOffsetFinal = value;
        }
    }

    private bool _SCDisableAnimationFinal;
    private bool SCDisableAnimationFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCDisableAnimationFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCDisableAnimationFinal = value;
        }
    }

    private double _SCPaddingStartFinal;
    private double SCPaddingStartFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCPaddingStartFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCPaddingStartFinal = value;
        }
    }

    private double _SCPaddingEndFinal;
    private double SCPaddingEndFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCPaddingEndFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCPaddingEndFinal = value;
        }
    }

    private double SCParallelOffsetFinal => IsVertical ? SCVerticalOffsetFinal : SCHorizontalOffsetFinal;

    private void SCSyncFinalVal()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (_finalValueSynced)
        {
            return;
        }

        _finalValueSynced = true;

        _SCCurrentPageFinal = CurrentPageInt;
        _SCPaddingStartFinal = IsVertical ? ThisListView.Padding.Top : ThisListView.Padding.Left;
        _SCPaddingEndFinal = IsVertical ? ThisListView.Padding.Bottom : ThisListView.Padding.Right;
        _SCHorizontalOffsetFinal = HorizontalOffset;
        _SCVerticalOffsetFinal = VerticalOffset;
        _SCZoomFactorFinal = ZoomFactor;
        _SCDisableAnimationFinal = false;
    }

    private void SCClearFinalVal(string reason)
    {
        Log("ClearFinalValue", reason);
        _finalValueSynced = false;
    }

    public bool MoveFrame(int increment, string reason)
    {
        MoveFrameInternal(increment, !AppData.TransitionAnimation, reason);
        return true;
    }

    private void MoveFrameInternal(int increment, bool disable_animation, string reason)
    {
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
        frame += increment;
        frame = Math.Min(FrameDataSource.Count - 1, frame);
        frame = Math.Max(0, frame);

        double page = FrameDataSource[frame].Page;
        float? zoom = _zoom > 101f ? 100f : (float?)null;

        SetScrollViewer2(zoom, page, disable_animation, reason);
    }

    private ScrollResult SetScrollViewer1(float? zoom, double? parallel_offset, bool disable_animation, string reason)
    {
        double? horizontal_offset = _isVertical ? null : parallel_offset;
        double? vertical_offset = _isVertical ? parallel_offset : null;

        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            horizontalOffset = horizontal_offset,
            verticalOffset = vertical_offset,
            disableAnimation = disable_animation,
        }, reason);
    }

    private ScrollResult SetScrollViewer2(float? zoom, double? page, bool disableAnimation, string reason)
    {
        double? horizontalOffset = null;
        double? verticalOffset = null;

        if (page.HasValue)
        {
            Tuple<double, double> offsets = PageOffset(page.Value);

            if (offsets == null)
            {
                Log("Jump", $"Failed (offsets is null, p={page.Value})");
                return ScrollResult.Failed;
            }

            if (Math.Abs(offsets.Item1 - SCParallelOffsetFinal) < 1.0)
            {
                // Ignore the request if target offset is really close to the current offset,
                // otherwise we might trigger a dead loop
                Log("Jump", "Failed (too close)");
                return ScrollResult.TooClose;
            }

            ConvertOffset(ref horizontalOffset, ref verticalOffset, offsets.Item1, offsets.Item2);
        }

        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            pageToApplyZoom = page,
            horizontalOffset = horizontalOffset,
            verticalOffset = verticalOffset,
            disableAnimation = disableAnimation,
        }, reason);
    }

    private ScrollResult SetScrollViewer3(
        float? zoom,
        ZoomType zoomType,
        double? horizontal_offset,
        double? vertical_offset,
        bool disable_animation,
        string reason
    )
    {
        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            zoomType = zoomType,
            horizontalOffset = horizontal_offset,
            verticalOffset = vertical_offset,
            disableAnimation = disable_animation,
        }, reason);
    }

    private ScrollResult SetScrollViewerInternal(ScrollRequest request, string reason)
    {
        if (!_isLoaded)
        {
            Log("Jump", "Failed (not loaded)");
            return ScrollResult.Failed;
        }

        DebugUtils.Assert(float.IsFinite(request.zoom ?? 0));
        DebugUtils.Assert(!float.IsNegative(request.zoom ?? 0));
        DebugUtils.Assert(double.IsFinite(request.horizontalOffset ?? 0));
        DebugUtils.Assert(double.IsFinite(request.verticalOffset ?? 0));

        Log("Jump", "Request:"
            + $" Reason={reason}"
            + $",P={request.pageToApplyZoom}"
            + $",Z={request.zoom}"
            + $",H={request.horizontalOffset}"
            + $",V={request.verticalOffset}"
            + $",D={request.disableAnimation}");

        var context = new ScrollContext
        {
            ZoomPercentage = request.zoom,
            DisableAnimation = request.disableAnimation,
            HorizontalOffset = request.horizontalOffset,
            VerticalOffset = request.verticalOffset,
        };

        SetScrollViewerZoom(request, context);

        DebugUtils.Assert(float.IsFinite(context.ZoomPercentage ?? 0));
        DebugUtils.Assert(!float.IsNegative(context.ZoomPercentage ?? 0));
        DebugUtils.Assert(float.IsFinite(context.ZoomFactor ?? 0));
        DebugUtils.Assert(!float.IsNegative(context.ZoomFactor ?? 0));
        DebugUtils.Assert(double.IsFinite(context.HorizontalOffset ?? 0));
        DebugUtils.Assert(double.IsFinite(context.VerticalOffset ?? 0));

        if (context.HorizontalOffset.HasValue)
        {
            context.HorizontalOffset = Math.Max(0, context.HorizontalOffset.Value);
        }
        if (context.VerticalOffset.HasValue)
        {
            context.VerticalOffset = Math.Max(0, context.VerticalOffset.Value);
        }

        Log("Jump", "ParamAfterZoom:"
            + $" Z={context.ZoomPercentage}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        AdjustParallelOffset(context);

        DebugUtils.Assert(float.IsFinite(context.ZoomPercentage ?? 0));
        DebugUtils.Assert(!float.IsNegative(context.ZoomPercentage ?? 0));
        DebugUtils.Assert(float.IsFinite(context.ZoomFactor ?? 0));
        DebugUtils.Assert(!float.IsNegative(context.ZoomFactor ?? 0));
        DebugUtils.Assert(double.IsFinite(context.HorizontalOffset ?? 0));
        DebugUtils.Assert(double.IsFinite(context.VerticalOffset ?? 0));

        Log("Jump", "ParamAfterFix:"
            + $" Z={context.ZoomPercentage}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        if (context.HorizontalOffset != null || context.VerticalOffset != null || context.ZoomFactor != null)
        {
            ChangeView(context.ZoomFactor, context.HorizontalOffset, context.VerticalOffset, context.DisableAnimation);
        }

        if (request.pageToApplyZoom.HasValue)
        {
            SCCurrentPageFinal = ToDiscretePage(request.pageToApplyZoom.Value);
        }

        _zoom = context.ZoomPercentage.Value;
        return ScrollResult.Success;
    }

    private void SetScrollViewerZoom(ScrollRequest request, ScrollContext context)
    {
        // Calculate zoom coefficient prediction.
        ZoomCoefficientResult zoomCoefficientNew;
        int frameNew;
        {
            int pageNew = request.pageToApplyZoom.HasValue ? (int)request.pageToApplyZoom.Value : SCCurrentPageFinal;
            frameNew = PageToFrame(pageNew, out _, out _);

            if (frameNew < 0 || frameNew >= FrameDataSource.Count)
            {
                frameNew = 0;
            }

            zoomCoefficientNew = ZoomCoefficient(frameNew);

            Log("Jump", "Zoom#1:"
                + $" PN={pageNew}"
                + $",FN={frameNew}"
                + $",ZCN={zoomCoefficientNew}");

            if (zoomCoefficientNew == null)
            {
                context.ZoomPercentage = _zoom;
                context.ZoomFactor = null;
                return;
            }
        }

        // Calculate zoom in percentage.
        double zoom;
        if (request.zoom.HasValue)
        {
            zoom = request.zoom.Value;
        }
        else
        {
            int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
            if (frame < 0 || frame >= FrameDataSource.Count)
            {
                frame = 0;
            }

            ZoomCoefficientResult zoomCoefficient = zoomCoefficientNew;
            if (frame != frameNew)
            {
                ZoomCoefficientResult zoomCoefficientTest = ZoomCoefficient(frame);
                if (zoomCoefficientTest != null)
                {
                    zoomCoefficient = zoomCoefficientTest;
                }
            }

            zoom = (float)(SCZoomFactorFinal / zoomCoefficient.Min());
        }

        if (request.zoomType == ZoomType.CenterCrop)
        {
            zoom *= zoomCoefficientNew.Max() / zoomCoefficientNew.Min();
        }

        double maxZoom = Math.Max(MAX_ZOOM, 100 * zoomCoefficientNew.Max() / zoomCoefficientNew.Min());
        zoom = Math.Min(zoom, maxZoom);
        zoom = Math.Max(zoom, MIN_ZOOM);
        context.ZoomPercentage = (float)zoom;

        // A zoom factor vary less than 1% will be ignored.
        float zoom_factor_new = (float)(zoom * zoomCoefficientNew.Min());

        if (Math.Abs(zoom_factor_new / SCZoomFactorFinal - 1.0f) <= 0.01f)
        {
            context.ZoomFactor = null;
            return;
        }

        context.ZoomFactor = zoom_factor_new;

        // Apply zooming.
        context.HorizontalOffset ??= SCHorizontalOffsetFinal;
        context.VerticalOffset ??= SCVerticalOffsetFinal;

        context.HorizontalOffset += ThisScrollViewer.ViewportWidth * 0.5;
        context.HorizontalOffset *= (float)context.ZoomFactor / SCZoomFactorFinal;
        context.HorizontalOffset -= ThisScrollViewer.ViewportWidth * 0.5;

        context.VerticalOffset += ThisScrollViewer.ViewportHeight * 0.5;
        context.VerticalOffset *= (float)context.ZoomFactor / SCZoomFactorFinal;
        context.VerticalOffset -= ThisScrollViewer.ViewportHeight * 0.5;

        context.HorizontalOffset = Math.Max(0.0, context.HorizontalOffset.Value);
        context.VerticalOffset = Math.Max(0.0, context.VerticalOffset.Value);
    }

    private void ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
    {
        if (horizontal_offset != null)
        {
            SCHorizontalOffsetFinal = horizontal_offset.Value;
        }

        if (vertical_offset != null)
        {
            SCVerticalOffsetFinal = vertical_offset.Value;
        }

        if (zoom_factor != null)
        {
            SCZoomFactorFinal = zoom_factor.Value;
        }

        if (disable_animation)
        {
            SCDisableAnimationFinal = true;
        }

        Log("Jump", "Commit:"
            + " Z=" + SCZoomFactorFinal.ToString()
            + ",H=" + SCHorizontalOffsetFinal.ToString()
            + ",V=" + SCVerticalOffsetFinal.ToString()
            + ",D=" + SCDisableAnimationFinal.ToString());

        ThisScrollViewer.ChangeView(SCHorizontalOffsetFinal, SCVerticalOffsetFinal, SCZoomFactorFinal, SCDisableAnimationFinal);
    }

    private void AdjustParallelOffset(ScrollContext context)
    {
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        double zoom = context.ZoomFactor ?? SCZoomFactorFinal;
        double parallelOffset;
        if (_isVertical)
        {
            if (!context.VerticalOffset.HasValue)
            {
                return;
            }
            parallelOffset = context.VerticalOffset.Value;
        }
        else
        {
            if (!context.HorizontalOffset.HasValue)
            {
                return;
            }
            parallelOffset = context.HorizontalOffset.Value;
        }

        double screenCenterOffset = ViewportParallelLength * 0.5 + parallelOffset;

        double? movementForward = null;
        FrameworkElement firstContainer = _frameManager.GetContainer(0);
        if (firstContainer != null)
        {
            double frameParallelLength = _isVertical ? firstContainer.ActualHeight : firstContainer.ActualWidth;
            double space = SCPaddingStartFinal * zoom - parallelOffset;
            double imageCenterOffset = (SCPaddingStartFinal + frameParallelLength * 0.5) * zoom;
            double imageCenterToScreenCenter = imageCenterOffset - screenCenterOffset;
            movementForward = Math.Min(space, imageCenterToScreenCenter);
        }

        double? movementBackward = null;
        FrameworkElement lastContainer = _frameManager.GetContainer(FrameDataSource.Count - 1);
        if (lastContainer != null)
        {
            double frameParallelLength = _isVertical ? lastContainer.ActualHeight : lastContainer.ActualWidth;
            double extentParallelLength = ExtentParallelLength * zoom / ZoomFactor;
            double space = SCPaddingEndFinal * zoom - (extentParallelLength - parallelOffset - ViewportParallelLength);
            double imageCenterOffset = extentParallelLength - (SCPaddingEndFinal + frameParallelLength * 0.5) * zoom;
            double imageCenterToScreenCenter = screenCenterOffset - imageCenterOffset;
            movementBackward = Math.Min(space, imageCenterToScreenCenter);
        }

        double movement = 0.0;
        bool canMove = false;
        if (movementForward.HasValue && movementForward.Value > 0)
        {
            canMove = true;
            movement += movementForward.Value;
        }
        if (movementBackward.HasValue && movementBackward.Value > 0)
        {
            canMove = true;
            movement -= movementBackward.Value;
        }

        if (!canMove)
        {
            return;
        }

        if (_isVertical)
        {
            context.VerticalOffset += movement;
        }
        else
        {
            context.HorizontalOffset += movement;
        }

        _manipulationDisabled = true;
    }

    private void AdjustPadding()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (FrameDataSource.Count == 0)
        {
            return;
        }

        double padding_start = SCPaddingStartFinal;
        do
        {
            int frame_idx = 0;

            if (_frameManager.GetContainer(frame_idx) == null)
            {
                break;
            }

            ZoomCoefficientResult zoom_coefficient = ZoomCoefficient(frame_idx);
            if (zoom_coefficient == null)
            {
                break;
            }

            double zoom_factor = MIN_ZOOM * zoom_coefficient.Min();
            double inner_length = ViewportParallelLength / zoom_factor;
            padding_start = (inner_length - FrameParallelLength(frame_idx)) / 2;
            padding_start = Math.Max(0.0, padding_start);
        } while (false);

        double padding_end = SCPaddingEndFinal;
        do
        {
            int frame_idx = FrameDataSource.Count - 1;

            if (_frameManager.GetContainer(frame_idx) == null)
            {
                break;
            }

            ZoomCoefficientResult zoom_coefficient = ZoomCoefficient(frame_idx);
            if (zoom_coefficient == null)
            {
                break;
            }

            double zoom_factor = MIN_ZOOM * zoom_coefficient.Min();
            double inner_length = ViewportParallelLength / zoom_factor;
            padding_end = (inner_length - FrameParallelLength(frame_idx)) / 2;
            padding_end = Math.Max(0.0, padding_end);
        } while (false);

        SCPaddingStartFinal = padding_start;
        SCPaddingEndFinal = padding_end;

        if (IsVertical)
        {
            ThisListView.Padding = new Thickness(0.0, padding_start, 0.0, padding_end);
        }
        else
        {
            ThisListView.Padding = new Thickness(padding_start, 0.0, padding_end, 0.0);
        }
    }

    private Tuple<double, double> PageOffset(double page)
    {
        DebugUtils.Assert(double.IsFinite(page));

        int pageInt = (int)page;
        page = Math.Min(page, PageCount);
        pageInt = Math.Min(pageInt, PageCount);
        page = Math.Max(page, 1);
        pageInt = Math.Max(pageInt, 1);

        int frame = PageToFrame(pageInt, out _, out int neighbor);
        FrameOffsetData offsets = FrameOffset(frame);

        if (offsets == null)
        {
            return null;
        }

        double perpendicularOffset = offsets.PerpendicularCenter * SCZoomFactorFinal - ViewportPerpendicularLength * 0.5;

        int pageMin;
        int pageMax;

        if (neighbor == -1)
        {
            pageMin = pageMax = pageInt;
        }
        else
        {
            pageMin = Math.Min(pageInt, neighbor);
            pageMax = Math.Max(pageInt, neighbor);
        }

        double parallelOffset;

        if (pageMin <= page && page <= pageMax)
        {
            parallelOffset = offsets.ParallelCenter;
        }
        else if (page < pageMin)
        {
            double page_frac = (0.5 - pageMin + page) * 2.0;
            parallelOffset = offsets.ParallelBegin + page_frac * (offsets.ParallelCenter - offsets.ParallelBegin);
        }
        else
        {
            double page_frac = (page - pageMax) * 2.0;
            parallelOffset = offsets.ParallelCenter + page_frac * (offsets.ParallelEnd - offsets.ParallelCenter);
        }

        parallelOffset = parallelOffset * SCZoomFactorFinal - ViewportParallelLength * 0.5;
        var result = new Tuple<double, double>(parallelOffset, perpendicularOffset);

        DebugUtils.Assert(double.IsFinite(result.Item1));
        DebugUtils.Assert(double.IsFinite(result.Item2));

        return result;
    }

    private FrameOffsetData FrameOffset(int frame)
    {
        FrameworkElement container = _frameManager.GetContainer(frame);
        if (container == null)
        {
            return null;
        }

        if (frame < 0 || frame >= FrameDataSource.Count)
        {
            return null;
        }
        ReaderFrameViewModel item = FrameDataSource[frame];

        GeneralTransform frame_transform = container.TransformToVisual(ThisListView);
        Point frame_position = frame_transform.TransformPoint(new Point(0.0, 0.0));

        double parallel_offset = IsVertical ? frame_position.Y : frame_position.X;
        double perpendicular_offset = IsVertical ? frame_position.X : frame_position.Y;

        bool left_to_right = _isLeftToRight;

        if (!_isVertical && !left_to_right)
        {
            parallel_offset -= item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right;
        }

        var result = new FrameOffsetData
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

        DebugUtils.Assert(double.IsFinite(result.ParallelEnd));
        DebugUtils.Assert(double.IsFinite(result.ParallelCenter));
        DebugUtils.Assert(double.IsFinite(result.ParallelBegin));
        DebugUtils.Assert(double.IsFinite(result.PerpendicularCenter));
        return result;
    }

    private ZoomCoefficientResult ZoomCoefficient(int frameIndex)
    {
        if (FrameDataSource.Count == 0)
        {
            return null;
        }

        if (frameIndex < 0 || frameIndex >= FrameDataSource.Count)
        {
            DebugUtils.Assert(false);
            return null;
        }

        double viewport_width = ThisScrollViewer.ViewportWidth;
        double viewport_height = ThisScrollViewer.ViewportHeight;
        double frame_width = FrameDataSource[frameIndex].FrameWidth;
        double frame_height = FrameDataSource[frameIndex].FrameHeight;

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

    //
    // Utilities
    //

    private void DispatchReaderStateChangeEvent(ReaderState state)
    {
        if (state == _state)
        {
            return;
        }
        _state = state;

        ReaderEventReaderStateChanged?.Invoke(this, state);
    }

    private double FrameParallelLength(int i)
    {
        FrameworkElement container = _frameManager.GetContainer(i);

        if (container != null)
        {
            return IsVertical ? container.ActualHeight : container.ActualWidth;
        }

        return 0;
    }

    private int PageToFrame(int page, out bool left_side, out int neighbor)
    {
        DebugUtils.Assert(int.IsPositive(page));

        switch (_pageArrangement)
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
                DebugUtils.Assert(false);
                goto case PageArrangementType.Single;
        }
    }

    private int ToDiscretePage(double pageContinuous)
    {
        return Math.Max(1, (int)Math.Round(pageContinuous));
    }

    private void ConvertOffset(ref double? toHorizontal, ref double? toVertical, double? fromParallel, double? fromPerpendicular)
    {
        if (_isVertical)
        {
            if (fromParallel != null)
            {
                toVertical = fromParallel;
            }

            if (fromPerpendicular != null)
            {
                toHorizontal = fromPerpendicular;
            }
        }
        else
        {
            if (fromParallel != null)
            {
                toHorizontal = fromParallel;
            }

            if (fromPerpendicular != null)
            {
                toVertical = fromPerpendicular;
            }
        }
    }

    private double? HorizontalVal(double? parallelVal, double? perpendicularVal)
    {
        return IsVertical ? perpendicularVal : parallelVal;
    }

    private double? VerticalVal(double? parallelVal, double? perpendicularVal)
    {
        return IsVertical ? parallelVal : perpendicularVal;
    }

    private void Log(string tag, string message)
    {
        Logger.I(LogTag.N($"ReaderView", tag), message);
    }

    private static void PostToCurrentThread(Action<Task> action)
    {
        var context = TaskScheduler.FromCurrentSynchronizationContext();
        _ = Task.Delay(1).ContinueWith(action, context);
    }

    //
    // Classes
    //

    internal sealed class ScrollManager : BaseTransaction<ScrollResult>
    {
        private readonly WeakReference<ReaderView> mReader;
        private float? mZoom = null;
        private ZoomType mZoomType = ZoomType.CenterInside;
        private double? mParallelOffset = null;
        private double? mHorizontalOffset = null;
        private double? mVerticalOffset = null;
        private double? mPage = null;
        private bool mDisableAnimation = true;
        private readonly string mReason;

        private ScrollManager(ReaderView reader, string reason)
        {
            mReader = new WeakReference<ReaderView>(reader);
            mReason = reason;
        }

        public static ScrollManager BeginTransaction(ReaderView reader, string reason)
        {
            return new ScrollManager(reader, reason);
        }

        protected override ScrollResult CommitImpl()
        {
            if (!mReader.TryGetTarget(out ReaderView reader))
            {
                return ScrollResult.Failed;
            }

            if (!reader._isLoaded)
            {
                return ScrollResult.Failed;
            }

            ScrollResult result;
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

        public ScrollManager CopyFrom(ReaderView view)
        {
            mPage = view.CurrentPage;
            OnSetOffset();
            return this;
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

    private class ImageDataModel
    {
        public double AspectRatio { get; set; }
        public IImageSource ImageSource { get; set; }
    }

    private class GestureHandler(ReaderView view) : ReaderGestureRecognizer.IHandler
    {
        private readonly ReaderView _view = view;

        public void ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            _view.OnReaderManipulationCompleted(sender, e);
        }

        public void ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            _view.OnReaderManipulationStarted(sender, e);
        }

        public void ManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            _view.OnReaderManipulationUpdated(sender, e);
        }

        public void Tapped(object sender, TappedEventArgs e)
        {
            _view.OnReaderTapped(sender, e);
        }
    }

    public enum ReaderState
    {
        Idle,
        Ready,
        Loading,
        Error,
    }

    public enum ScrollResult
    {
        Success = 0,
        Failed = 1,
        TooClose = 2,
    }

    public enum ZoomType
    {
        CenterInside,
        CenterCrop,
    }

    private class ScrollRequest
    {
        // Zoom
        public float? zoom = null;
        public ZoomType zoomType = ZoomType.CenterInside;
        public double? pageToApplyZoom = null;

        // Offset
        public double? horizontalOffset = null;
        public double? verticalOffset = null;

        // Animation
        public bool disableAnimation = false;
    }

    private class ScrollContext
    {
        public float? ZoomPercentage = null;
        public float? ZoomFactor = null;
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;
        public bool DisableAnimation = false;
    }
}
