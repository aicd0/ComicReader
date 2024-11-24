// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#define DEBUG_LOG_LOAD
#if DEBUG
//#define DEBUG_LOG_MANIPULATION
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_UPDATE_IMAGE
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.SimpleImageView;
using ComicReader.Common.Threading;
using ComicReader.Database;

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
    private const int MIN_PRELOAD_FRAMES_BEFORE = 5;
    private const int MAX_PRELOAD_FRAMES_BEFORE = 10;
    private const int MIN_PRELOAD_FRAMES_AFTER = 5;
    private const int MAX_PRELOAD_FRAMES_AFTER = 10;

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

    private bool _isFirstFrameLoaded = false;
    private bool _isFirstFrameActionPerformed = false;
    private bool _isLastFrameLoaded = false;
    private bool _isLastFrameActionPerformed = false;
    private bool _isInitialFrameLoaded = false;

    private bool _tapPending = false;
    private bool _tapCancelled = false;
    private readonly UIElement _gestureReference;
    private readonly GestureHandler _gestureHandler;
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();

    private double _initialPage = 0.0;
    private List<IImageSource> _originalDataModel;
    private readonly TaskQueueDispatcher _loadInfoDispatcher = new(new TaskQueue("ReaderViewLoadInfoQueue"), "");
    private readonly TaskQueueDispatcher _loadImageDispatcher = new(new TaskQueue("ReaderViewLoadImageQueue"), "");
    private readonly ReaderFrameManager _frameManager = new();
    private readonly Dictionary<int, ImageDataModel> _dataModel = [];
    private readonly CancellationSession _dataModelSession;
    private readonly CancellationSession _loadImageSession;

    private long _metricsStartLoadTime = 0;

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
        _loadImageSession = new(_dataModelSession);
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
        _uiStateUpdatedPageArrangement = true; // we want to do a reload
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
        Reload(_originalDataModel);
    }

    //
    // Loader
    //

    private void Reload(List<IImageSource> images)
    {
        _metricsStartLoadTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        // Refresh token
        _dataModelSession.Next();
        CancellationSession.IToken token = _dataModelSession.Token;

        // Update internal states
        _dataModel.Clear();
        PageCount = images.Count;
        ResetFrames();
        SCClearFinalVal();

        // Reset loader
        int lastFrameIndex = PageToFrame(PageCount + 1, out bool _, out int _);
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
            if (index == lastFrameIndex)
            {
                _isLastFrameLoaded = false;
            }
            UpdateLoader();
        });

        // Dispatch event
        DispatchReaderStateChangeEvent(ReaderState.Loading);

        _loadInfoDispatcher.Submit(delegate
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
                        long loadTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _metricsStartLoadTime;
                        LogLoadTime("GetInfo", $"time={loadTime},index={index}");
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
        }, "ReaderLoadImageInfo");
    }

    private void ResetLoader()
    {
        _isFirstFrameLoaded = false;
        _isFirstFrameActionPerformed = false;
        _isLastFrameLoaded = false;
        _isLastFrameActionPerformed = false;
        _isInitialFrameLoaded = false;
    }

    private void UpdateLoader()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (_isFirstFrameLoaded && !_isFirstFrameActionPerformed)
        {
            _isFirstFrameActionPerformed = true;
            SetScrollViewer1(Zoom, null, true, "AdjustZooming");
            AdjustPadding();
        }

        if (_isLastFrameLoaded && !_isLastFrameActionPerformed)
        {
            _isLastFrameActionPerformed = true;
            AdjustPadding();
        }

        if (_isFirstFrameLoaded && !_isInitialFrameLoaded)
        {
            _isInitialFrameLoaded = SetScrollViewer2(null, _initialPage, true, "JumpToInitialPage");
            if (_isInitialFrameLoaded)
            {
                UpdateImages();
                DispatchReaderStateChangeEvent(ReaderState.Ready);
            }
        }
    }

    //
    // UI
    //

    private void UpdateUI()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (_uiStateUpdatedVisibility)
        {
            _uiStateUpdatedVisibility = false;
            bool isVisible = _isVisible;
            SvReader.IsEnabled = isVisible;
            SvReader.IsHitTestVisible = isVisible;
            SvReader.Opacity = isVisible ? 1 : 0;

            if (isVisible)
            {
                UpdateImages(true);
            }
            else
            {
                _loadImageSession.Next();
            }
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
        }

        if (_uiStateUpdatedPageArrangement)
        {
            _uiStateUpdatedPageArrangement = false;
            if (_originalDataModel != null)
            {
                _initialPage = CurrentPage;
                Reload(_originalDataModel);
            }
        }
    }

    private void ResetFrames()
    {
        int lastFrameIndex = PageToFrame(PageCount, out bool _, out int _);

        for (int i = FrameDataSource.Count - 1; i > lastFrameIndex; --i)
        {
            FrameDataSource.RemoveAt(i);
        }

        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel item = FrameDataSource[i];
            item.PageL = -1;
            item.PageR = -1;
            item.ImageLeftSet = false;
            item.ImageRightSet = false;
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
            FrameDataSource.Add(new ReaderFrameViewModel());
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

        item.ImageSourceLeft = _dataModel.GetValueOrDefault(item.PageL - 1)?.ImageSource;
        item.ImageSourceRight = _dataModel.GetValueOrDefault(item.PageR - 1)?.ImageSource;

        item.RebindEntireViewModel();
    }

    private bool UpdatePage(bool useFinal)
    {
        if (!_isLoaded)
        {
            return false;
        }

        double offset;
        {
            double parallelOffset = useFinal ? ParallelOffsetFinal : ParallelOffset;
            double zoomFactor = useFinal ? ZoomFactorFinal : ZoomFactor;
            offset = (parallelOffset + ViewportParallelLength * 0.5) / zoomFactor;
        }

        // Locate current frame using binary search.
        if (FrameDataSource.Count == 0)
        {
            return false;
        }

        int begin = 0;
        int end = FrameDataSource.Count - 1;
        FrameOffsetData frameOffsets = null;

        while (true)
        {
            int i = (begin + end + 1) / 2;
            FrameOffsetData offsets = FrameOffsets(i);

            if (offsets == null)
            {
                end = i - 1;
                if (begin >= end)
                {
                    frameOffsets = null;
                    break;
                }
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
                end = i - 1;
                if (begin >= end)
                {
                    frameOffsets ??= FrameOffsets(begin);
                    break;
                }
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
            // This could happen when we are rearranging pages.
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

#if DEBUG_LOG_UPDATE_PAGE
            Log("Page updated (" +
                "Page=" + PageSource.ToString() + "," +
                "UseF=" + use_final.ToString() + "," +
                "PO=" + ParallelOffset.ToString() + "," +
                "POF=" + ParallelOffsetFinal.ToString() + "," +
                "ZF=" + ZoomFactor.ToString() + ")");
#endif

        return true;
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
            UpdateLoader();
        }
        else
        {
            _dataModelSession.Next();
        }
    }

    //
    // Size Change Event Handlers
    //

    private void OnReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnSizeChanged();
    }

    private void OnReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (OnViewChanged(!e.IsIntermediate))
        {
            ReaderEventPageChanged?.Invoke(this, e.IsIntermediate);
        }
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
            _frameManager.RemoveFrame(args.ItemIndex);
            viewHolder.SetReadyStateChangeHandler(null);
            viewHolder.Bind(null);
        }
        else
        {
            int index = args.ItemIndex;
            viewHolder.SetReadyStateChangeHandler(delegate (FrameworkElement container, bool isReady)
            {
                _frameManager.PutFrame(index, container, isReady);
                Log("FrameReadyChanged", $"i={index},hash={container.GetHashCode()},ready={isReady}");
            });
            viewHolder.Bind(item);
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
            if (Math.Abs(Zoom - 100) <= 1)
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

    private int _SCCurrentPageFinal;
    private int SCCurrentPageFinal
    {
        get
        {
            FillFinalVal();
            return _SCCurrentPageFinal;
        }
        set
        {
            FillFinalVal();
            _SCCurrentPageFinal = value;
        }
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
                System.Diagnostics.Debug.Assert(false);
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

    private void LogLoadTime(string tag, string message)
    {
        Logger.I(LogTag.N("LoadTime", tag), message);
    }

    private void Log(string tag, string message)
    {
        string name = _isVertical ? "Vertical" : "Horizontal";
        Logger.I(LogTag.N($"Reader{name}", tag), message);
    }

    //
    // Classes
    //

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

    //
    // Obsolete
    //

    // Observer - Scroll Viewer
    private ZoomCoefficientResult ZoomCoefficient(int frame_idx)
    {
        if (FrameDataSource.Count == 0)
        {
            return null;
        }

        if (frame_idx < 0 || frame_idx >= FrameDataSource.Count)
        {
            System.Diagnostics.Debug.Assert(false);
            return null;
        }

        double viewport_width = ThisScrollViewer.ViewportWidth;
        double viewport_height = ThisScrollViewer.ViewportHeight;
        double frame_width = FrameDataSource[frame_idx].FrameWidth;
        double frame_height = FrameDataSource[frame_idx].FrameHeight;

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

    private float Zoom { get; set; } = 100f;
    private float ZoomFactor => ThisScrollViewer.ZoomFactor;

    private float m_ZoomFactorFinal;
    private float ZoomFactorFinal
    {
        get
        {
            FillFinalVal();
            return m_ZoomFactorFinal;
        }
        set
        {
            FillFinalVal();
            m_ZoomFactorFinal = value;
        }
    }

    private double HorizontalOffset => ThisScrollViewer.HorizontalOffset;

    private double m_HorizontalOffsetFinal;
    private double HorizontalOffsetFinal
    {
        get
        {
            FillFinalVal();
            return m_HorizontalOffsetFinal;
        }
        set
        {
            FillFinalVal();
            m_HorizontalOffsetFinal = value;
        }
    }

    private double VerticalOffset => ThisScrollViewer.VerticalOffset;

    private double m_VerticalOffsetFinal;
    private double VerticalOffsetFinal
    {
        get
        {
            FillFinalVal();
            return m_VerticalOffsetFinal;
        }
        set
        {
            FillFinalVal();
            m_VerticalOffsetFinal = value;
        }
    }

    private bool m_DisableAnimationFinal;
    private bool DisableAnimationFinal
    {
        get
        {
            FillFinalVal();
            return m_DisableAnimationFinal;
        }
        set
        {
            FillFinalVal();
            m_DisableAnimationFinal = value;
        }
    }

    private double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
    private double ParallelOffsetFinal => IsVertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
    private double ViewportParallelLength => IsVertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
    private double ViewportPerpendicularLength => IsVertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
    private double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
    private double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);
    private double FrameParallelLength(int i)
    {
        FrameworkElement container = _frameManager.GetContainer(i);
        if (container != null)
        {
            return IsVertical ? container.ActualHeight : container.ActualWidth;
        }
        return 0;
    }

    // Observer - List View
    private double m_PaddingStartFinal;
    private double PaddingStartFinal
    {
        get
        {
            FillFinalVal();
            return m_PaddingStartFinal;
        }
        set
        {
            FillFinalVal();
            m_PaddingStartFinal = value;
        }
    }

    private double m_PaddingEndFinal;
    private double PaddingEndFinal
    {
        get
        {
            FillFinalVal();
            return m_PaddingEndFinal;
        }
        set
        {
            FillFinalVal();
            m_PaddingEndFinal = value;
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
        FrameOffsetData offsets = FrameOffsets(frame);

        if (offsets == null)
        {
            return null;
        }

        double perpendicularOffset = offsets.PerpendicularCenter * ZoomFactorFinal - ViewportPerpendicularLength * 0.5;

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

        parallelOffset = parallelOffset * ZoomFactorFinal - ViewportParallelLength * 0.5;
        var result = new Tuple<double, double>(parallelOffset, perpendicularOffset);

        DebugUtils.Assert(double.IsFinite(result.Item1));
        DebugUtils.Assert(double.IsFinite(result.Item2));

        return result;
    }

    private FrameOffsetData FrameOffsets(int frame)
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

    // Modifier - Configurations
    private ScrollViewer ThisScrollViewer => SvReader;
    private ListView ThisListView => LvReader;

    // Modifier - Image Loader
    //private readonly CancellationSession _updateImageSession = new();

    private void UpdateImages(bool remove_out_of_view = true)
    {
        if (!_isVisible)
        {
            _loadImageSession.Next();
            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                ReaderFrameViewModel model = FrameDataSource[i];
                model.ImageLeftSet = false;
                model.ImageRightSet = false;
            }
            return;
        }

        if (!UpdatePage(true))
        {
            return;
        }

        int frame = PageToFrame(CurrentPageInt, out _, out _);
        int preload_window_begin = Math.Max(frame - MAX_PRELOAD_FRAMES_BEFORE, 0);
        int preload_window_end = Math.Min(frame + MAX_PRELOAD_FRAMES_AFTER, FrameDataSource.Count - 1);

        if (remove_out_of_view)
        {
            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                if (i < preload_window_begin || i > preload_window_end)
                {
                    ReaderFrameViewModel model = FrameDataSource[i];
                    model.ImageLeftSet = false;
                    model.ImageRightSet = false;
                }
            }
        }

        bool needPreload = false;
        int check_window_begin = Math.Max(frame - MIN_PRELOAD_FRAMES_BEFORE, 0);
        int check_window_end = Math.Min(frame + MIN_PRELOAD_FRAMES_AFTER, FrameDataSource.Count - 1);
        for (int i = check_window_begin; i <= check_window_end; ++i)
        {
            ReaderFrameViewModel m = FrameDataSource[i];
            if ((m.PageL > 0 && !m.ImageLeftSet) || (m.PageR > 0 && !m.ImageRightSet))
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
            var img_loader_tokens = new List<SimpleImageLoader.Token>();

            void addToLoaderQueue(int i)
            {
                if (i < 0 || i >= FrameDataSource.Count)
                {
                    return;
                }

                ReaderFrameViewModel m = FrameDataSource[i]; // Stores locally.

                if (!m.ImageLeftSet && m.PageL > 0)
                {
                    m.ImageLeftSet = true;
                    img_loader_tokens.Add(new SimpleImageLoader.Token
                    {
                        Source = m.ImageSourceLeft,
                        ImageResultHandler = new LoadImageResultHandler(m, true, this, i)
                    });
                }

                if (!m.ImageRightSet && m.PageR > 0)
                {
                    m.ImageRightSet = true;
                    img_loader_tokens.Add(new SimpleImageLoader.Token
                    {
                        Source = m.ImageSourceRight,
                        ImageResultHandler = new LoadImageResultHandler(m, false, this, i)
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

            new SimpleImageLoader.Transaction(_loadImageSession.Token, img_loader_tokens)
                .SetDispatcher(_loadImageDispatcher)
                .Commit();
        }
    }

    private class LoadImageResultHandler(ReaderFrameViewModel model, bool isLeft, ReaderView view, int index) : IImageResultHandler
    {
        private readonly long _startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        private readonly ReaderFrameViewModel _model = model;
        private readonly bool _isLeft = isLeft;

        public void OnSuccess(BitmapImage image)
        {
            long loadTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() - _startTime;
            view.LogLoadTime("LoadImage", $"time={loadTime},index={index}");
            if (_isLeft)
            {
                _model.ImageLeft = image;
            }
            else
            {
                _model.ImageRight = image;
            }
        }
    }

    // Modifier - Scrolling
    public bool MoveFrame(int increment, string reason)
    {
        if (!UpdatePage(true))
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
        if (FrameDataSource.Count == 0)
        {
            return false;
        }

        int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
        frame += increment;
        frame = Math.Min(FrameDataSource.Count - 1, frame);
        frame = Math.Max(0, frame);

        double page = FrameDataSource[frame].Page;
        float? zoom = Zoom > 101f ? 100f : (float?)null;

        return SetScrollViewer2(zoom, page, disable_animation, reason);
    }

    internal sealed class ScrollManager : BaseTransaction<bool>
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

        protected override bool CommitImpl()
        {
            if (!mReader.TryGetTarget(out ReaderView reader))
            {
                return false;
            }

            if (!reader._isLoaded)
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

    private bool SetScrollViewer1(float? zoom, double? parallel_offset, bool disable_animation, string reason)
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

    private bool SetScrollViewer2(float? zoom, double? page, bool disableAnimation, string reason)
    {
        double? horizontalOffset = null;
        double? verticalOffset = null;

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

    private bool SetScrollViewer3(
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

    private bool SetScrollViewerInternal(ScrollRequest request, string reason)
    {
        if (!_isLoaded)
        {
            return false;
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

        if (!ChangeView(context.ZoomFactor, context.HorizontalOffset, context.VerticalOffset, context.DisableAnimation))
        {
            return false;
        }

        if (request.pageToApplyZoom.HasValue)
        {
            SCCurrentPageFinal = ToDiscretePage(request.pageToApplyZoom.Value);
        }

        Zoom = context.ZoomPercentage.Value;
        return true;
    }

    private void SetScrollViewerZoom(ScrollRequest request, ScrollContext context)
    {
        // Calculate zoom coefficient prediction.
        ZoomCoefficientResult zoom_coefficient_new;
        int frame_new;
        {
            int page_new = request.pageToApplyZoom.HasValue ? (int)request.pageToApplyZoom.Value : CurrentPageInt;
            frame_new = PageToFrame(page_new, out _, out _);
            if (frame_new < 0 || frame_new >= FrameDataSource.Count)
            {
                frame_new = 0;
            }

            zoom_coefficient_new = ZoomCoefficient(frame_new);
            if (zoom_coefficient_new == null)
            {
                context.ZoomPercentage = Zoom;
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
            int frame = PageToFrame(CurrentPageInt, out _, out _);
            if (frame < 0 || frame >= FrameDataSource.Count)
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

        if (request.zoomType == ZoomType.CenterCrop)
        {
            zoom *= zoom_coefficient_new.Max() / zoom_coefficient_new.Min();
        }

        double maxZoom = Math.Max(MAX_ZOOM, 100 * zoom_coefficient_new.Max() / zoom_coefficient_new.Min());
        zoom = Math.Min(zoom, maxZoom);
        zoom = Math.Max(zoom, MIN_ZOOM);
        context.ZoomPercentage = (float)zoom;

        // A zoom factor vary less than 1% will be ignored.
        float zoom_factor_new = (float)(zoom * zoom_coefficient_new.Min());

        if (Math.Abs(zoom_factor_new / ZoomFactorFinal - 1.0f) <= 0.01f)
        {
            context.ZoomFactor = null;
            return;
        }

        context.ZoomFactor = zoom_factor_new;

        // Apply zooming.
        context.HorizontalOffset ??= HorizontalOffsetFinal;
        context.VerticalOffset ??= VerticalOffsetFinal;

        context.HorizontalOffset += ThisScrollViewer.ViewportWidth * 0.5;
        context.HorizontalOffset *= (float)context.ZoomFactor / ZoomFactorFinal;
        context.HorizontalOffset -= ThisScrollViewer.ViewportWidth * 0.5;

        context.VerticalOffset += ThisScrollViewer.ViewportHeight * 0.5;
        context.VerticalOffset *= (float)context.ZoomFactor / ZoomFactorFinal;
        context.VerticalOffset -= ThisScrollViewer.ViewportHeight * 0.5;

        context.HorizontalOffset = Math.Max(0.0, context.HorizontalOffset.Value);
        context.VerticalOffset = Math.Max(0.0, context.VerticalOffset.Value);
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

        Log("Jump", "Commit:"
            + " Z=" + ZoomFactorFinal.ToString()
            + ",H=" + HorizontalOffsetFinal.ToString()
            + ",V=" + VerticalOffsetFinal.ToString()
            + ",D=" + DisableAnimationFinal.ToString());

        ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, DisableAnimationFinal);
        return true;
    }

    private void AdjustParallelOffset(ScrollContext context)
    {
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        double zoom = context.ZoomFactor ?? ZoomFactorFinal;
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
            double space = PaddingStartFinal * zoom - parallelOffset;
            double imageCenterOffset = (PaddingStartFinal + frameParallelLength * 0.5) * zoom;
            double imageCenterToScreenCenter = imageCenterOffset - screenCenterOffset;
            movementForward = Math.Min(space, imageCenterToScreenCenter);
        }

        double? movementBackward = null;
        FrameworkElement lastContainer = _frameManager.GetContainer(FrameDataSource.Count - 1);
        if (lastContainer != null)
        {
            double frameParallelLength = _isVertical ? lastContainer.ActualHeight : lastContainer.ActualWidth;
            double extentParallelLength = ExtentParallelLength * zoom / ZoomFactor;
            double space = PaddingEndFinal * zoom - (extentParallelLength - parallelOffset - ViewportParallelLength);
            double imageCenterOffset = extentParallelLength - (PaddingEndFinal + frameParallelLength * 0.5) * zoom;
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

        m_manipulation_disabled = true;
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

        double padding_start = PaddingStartFinal;
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

        double padding_end = PaddingEndFinal;
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
    private void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / 120;

        if (_isContinuous || Zoom > 105)
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
            MoveFrame(delta, "PageTurningUsingPointerWheel");
        }

        m_manipulation_disabled = true;
        e.Handled = true;
    }

    // Events - Manipulation
    private bool m_manipulation_disabled = false;

    private void OnReaderManipulationStarted(ManipulationStartedEventArgs e)
    {
        m_manipulation_disabled = false;

#if DEBUG_LOG_MANIPULATION
        Log("Manipulation started");
#endif
    }

    private void OnReaderManipulationUpdated(ManipulationUpdatedEventArgs e)
    {
        if (m_manipulation_disabled)
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
            zoom = Zoom * scale;
        }

        ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingManipulation")
            .Zoom(zoom)
            .HorizontalOffset(HorizontalOffsetFinal - dx)
            .VerticalOffset(VerticalOffsetFinal - dy)
            .EnableAnimation()
            .Commit();
    }

    private void OnReaderManipulationCompleted(ManipulationCompletedEventArgs e)
    {
        if (_isContinuous || Zoom >= FORCE_CONTINUOUS_ZOOM_THRESHOLD)
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

    // Events - Common
    private bool OnViewChanged(bool final)
    {
        if (!_isLoaded)
        {
            return false;
        }

        if (!UpdatePage(true))
        {
            return false;
        }

        if (final)
        {
            SCClearFinalVal();

            // Notify the scroll viewer to update its inner states.
            SetScrollViewer1(null, null, false, "AdjustInnerStateAfterViewChanged");

            if (!_isContinuous && Zoom < FORCE_CONTINUOUS_ZOOM_THRESHOLD)
            {
                // Stick our view to the center of two pages.
                MoveFrameInternal(0, false, "StickToCenter");
            }

            Log("ViewChanged", $"Z={ZoomFactorFinal}"
                + $",H={HorizontalOffsetFinal}"
                + $",V={VerticalOffsetFinal}"
                + $",P={CurrentPage}");
        }

        UpdateImages(final);
        return true;
    }

    private void OnSizeChanged()
    {
        AdjustPadding();
    }

    // Internal - Final Values
    private bool m_final_value_set = false;

    private void FillFinalVal()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (m_final_value_set)
        {
            return;
        }

        m_final_value_set = true;

        _SCCurrentPageFinal = CurrentPageInt;
        m_PaddingStartFinal = IsVertical ? ThisListView.Padding.Top : ThisListView.Padding.Left;
        m_PaddingEndFinal = IsVertical ? ThisListView.Padding.Bottom : ThisListView.Padding.Right;
        m_HorizontalOffsetFinal = HorizontalOffset;
        m_VerticalOffsetFinal = VerticalOffset;
        m_ZoomFactorFinal = ZoomFactor;
        m_DisableAnimationFinal = false;
    }

    private void SCClearFinalVal()
    {
        m_final_value_set = false;
    }

    // Internal - Conversions

    private double FinalVal(double val)
    {
        return val / ZoomFactor * ZoomFactorFinal;
    }

    public enum ZoomType
    {
        CenterInside,
        CenterCrop,
    }

    public class ScrollRequest
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
