// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#define DEBUG_LOG_LOAD
#if DEBUG
//#define DEBUG_LOG_JUMP
//#define DEBUG_LOG_MANIPULATION
//#define DEBUG_LOG_VIEW_CHANGE
//#define DEBUG_LOG_UPDATE_PAGE
//#define DEBUG_LOG_UPDATE_IMAGE
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.SimpleImageView;
using ComicReader.Common.Structs;
using ComicReader.Database;
using ComicReader.Utils;

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
    private const float MIN_ZOOM = 90F;
    private const float FORCE_CONTINUOUS_ZOOM_THRESHOLD = 105F;
    private const int MIN_PRELOAD_FRAMES_BEFORE = 5;
    private const int MAX_PRELOAD_FRAMES_BEFORE = 10;
    private const int MIN_PRELOAD_FRAMES_AFTER = 5;
    private const int MAX_PRELOAD_FRAMES_AFTER = 10;

    //
    // Variables
    //

    private bool _isLoaded;
    private bool _isVertical = true;
    private bool _isContinuous = true;
    private bool _isVisible = true;
    private bool _isLeftToRight = true;
    private PageArrangementType _pageArrangement = PageArrangementType.Single;
    private bool _uiStateUpdatedVisibility = true;
    private bool _uiStateUpdatedOrientation = true;
    private bool _uiStateUpdatedContinuous = true;
    private bool _uiStateUpdatedFlowDirection = true;

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

    private double _initialPosition = 0.0;
    private readonly ReaderFrameManager _frameManager = new();
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
        _loadImageSession = new(_dataModelSession);
    }

    //
    // Public Interfaces
    //

    public delegate void ReaderEventTappedEventHandler(ReaderView sender);
    public event ReaderEventTappedEventHandler ReaderEventTapped;

    public delegate void ReaderEventPageChangedEventHandler(ReaderView sender, bool isIntermediate);
    public event ReaderEventPageChangedEventHandler ReaderEventPageChanged;

    public delegate void ReaderEventInitalPageLoadedHandler();
    private event ReaderEventInitalPageLoadedHandler ReaderEventInitialPageLoaded;

    public int PageCount { get; private set; } = 0;
    public int CurrentPage => GetCurrentPage();
    public double CurrentPosition => PageSource;
    public bool IsLastPage => PageToFrame(CurrentPage, out _, out _) >= FrameDataSource.Count - 1;
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
        _pageArrangement = type;
    }

    public void SetInitialPosition(double position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        _initialPosition = position;
    }

    public void SetInitialPageLoadedHandler(ReaderEventInitalPageLoadedHandler handler)
    {
        ReaderEventInitialPageLoaded = handler;
    }

    public void StartLoadingImages(List<IImageSource> images)
    {
        _dataModelSession.Next();
        CancellationSession.IToken token = _dataModelSession.Token;
        _dataModel.Clear();
        var imagesCopy = new List<IImageSource>(images);
        PageCount = imagesCopy.Count;

        ResetLoader();
        _frameManager.ResetReadyIndex();
        int lastFrameIndex = PageToFrame(imagesCopy.Count + 1, out bool _, out int _);
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
                _isFirstFrameLoaded = false;
            }
            UpdateLoader();
        });

        for (int i = 0; i < imagesCopy.Count; i++)
        {
            int index = i;
            IImageSource image = imagesCopy[i];
            TaskQueue.DefaultQueue.Enqueue("ReaderLoadImageInfo", delegate
            {
                ImageInfoManager.ImageInfo imageInfo = ImageInfoManager.GetImageInfo(image);
                _ = Threading.RunInMainThread(delegate
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    double aspectRatio = 0.0;
                    if (imageInfo != null)
                    {
                        aspectRatio = (double)imageInfo.Width / imageInfo.Height;
                    }
                    SetImageData(index, aspectRatio, image);
                });
                return TaskException.Success;
            });
        }
    }

    //
    // Loader
    //

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
            SetScrollViewer1(Zoom, null, true, "AdjustZooming");
            AdjustPadding();
            _isFirstFrameActionPerformed = true;
        }

        if (_isLastFrameLoaded && !_isLastFrameActionPerformed)
        {
            AdjustPadding();
            _isLastFrameActionPerformed = true;
        }

        if (_isFirstFrameLoaded && !_isInitialFrameLoaded)
        {
            _isInitialFrameLoaded = SetScrollViewer2(null, _initialPosition, true, "JumpToInitialPage");
            if (_isInitialFrameLoaded)
            {
                UpdateImagesInternal();
                ReaderEventInitialPageLoaded?.Invoke();
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
        }

        if (_uiStateUpdatedOrientation)
        {
            _uiStateUpdatedOrientation = false;
            bool isVertical = _isVertical;
            SvReader.VerticalScrollBarVisibility = isVertical ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            SvReader.VerticalScrollMode = isVertical ? ScrollMode.Enabled : ScrollMode.Disabled;
            GReader.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            GReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            LvReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Stretch;
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
    }

    private void SetImageData(int index, double aspectRatio, IImageSource source)
    {
        var model = new ImageDataModel
        {
            AspectRatio = aspectRatio,
        };
        _dataModel[index] = model;

        int frameIndex = PageToFrame(index + 1, out bool leftSide, out int neighbor);
        if (frameIndex < 0)
        {
            Logger.F(TAG, "");
            return;
        }

        int page = index + 1;
        bool dual = neighbor != -1;

        if (neighbor != -1)
        {
            int neighborIndex = neighbor - 1;
            if (_dataModel.TryGetValue(neighborIndex, out ImageDataModel neighborModel))
            {
                aspectRatio += neighborModel.AspectRatio;
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

        item.ImageSource = source;
        item.FrameWidth = frameWidth;
        item.FrameHeight = frameHeight;
        item.FrameMargin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);

        if (leftSide)
        {
            if (item.PageL != page)
            {
                item.PageL = page;
                item.ImageLeft = null;
            }

            if (!dual)
            {
                item.PageR = -1;
                item.ImageRight = null;
            }
        }
        else
        {
            if (item.PageR != page)
            {
                item.PageR = page;
                item.ImageRight = null;
            }

            if (!dual)
            {
                item.PageL = -1;
                item.ImageLeft = null;
            }
        }
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
            StopLoadingImage();
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
                Log($"FrameReadyChanged(i={index},hash={container.GetHashCode()},ready={isReady})");
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
            Utils.C0.Run(async delegate
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
                    .Zoom(100, Common.Structs.ZoomType.CenterCrop)
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
    // Utilities
    //

    private int PageToFrame(int page, out bool left_side, out int neighbor)
    {
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

    private void Log(string message)
    {
        string name = _isVertical ? "Vertical" : "Horizontal";
        Logger.I($"Reader{name}", message);
    }

    //
    // Classes
    //

    private class ImageDataModel
    {
        public double AspectRatio { get; set; }
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

    //
    // Obsolete
    //

    public void SetViewModel(ReaderPageViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    //
    // Methods
    //

    public int GetCurrentPage()
    {
        int page = Page;
        if (page <= 0)
        {
            page = (int)Math.Round(_initialPosition);
        }
        return page;
    }

    // Observer - Common
    public bool IsCurrentReader => _viewModel.ReaderSettingsLiveData.GetValue().IsVertical == IsVertical;
    public bool IsHorizontal => !IsVertical;
    public bool IsLeftToRight => _viewModel.ReaderSettingsLiveData.GetValue().IsLeftToRight;
    public bool IsContinuous => IsVertical ?
        _viewModel.ReaderSettingsLiveData.GetValue().IsVerticalContinuous :
        _viewModel.ReaderSettingsLiveData.GetValue().IsHorizontalContinuous;
    public PageArrangementType PageArrangement => IsVertical ?
        _viewModel.ReaderSettingsLiveData.GetValue().VerticalPageArrangement :
        _viewModel.ReaderSettingsLiveData.GetValue().HorizontalPageArrangement;

    // Observer - Pages
    private readonly Utils.CancellationLock m_UpdatePageLock = new();

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

    private bool UpdatePage(bool use_final)
    {
        if (!_isLoaded)
        {
            return false;
        }

        double offset;
        {
            double parallel_offset = use_final ? ParallelOffsetFinal : ParallelOffset;
            double zoom_factor = use_final ? ZoomFactorFinal : ZoomFactor;
            offset = (parallel_offset + ViewportParallelLength * 0.5) / zoom_factor;
        }

        // Locate current frame using binary search.
        if (FrameDataSource.Count == 0)
        {
            return false;
        }

        int begin = 0;
        int end = FrameDataSource.Count - 1;

        while (begin < end)
        {
            int i = (begin + end + 1) / 2;
            ReaderFrameViewModel item = FrameDataSource[i];
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

        ReaderFrameViewModel frame = FrameDataSource[begin];
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
    }

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
        FrameworkElement container = _frameManager.GetContainer(i);
        if (container != null)
        {
            return IsVertical ? container.ActualHeight : container.ActualWidth;
        }
        return 0;
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
    public ScrollViewer ThisScrollViewer => SvReader;
    public ListView ThisListView => LvReader;

    // Modifier - General Loader
    private readonly Utils.CancellationLock m_LoaderLock = new();

    public void Reset()
    {
        ResetFrames();
        SyncFinalVal();
    }

    public void StopLoadingImage()
    {
        //_updateImageSession.Next();
    }

    private void ResetFrames()
    {
        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel item = FrameDataSource[i];
            item.PageL = -1;
            item.PageR = -1;
            item.ImageLeft = null;
            item.ImageRight = null;
        }
    }

    // Modifier - Image Loader
    //private readonly CancellationSession _updateImageSession = new();
    private readonly TaskQueue _updateImageQueue = new("ReaderUpdateImage");

    public bool UpdateImages(bool use_final)
    {
        if (!UpdatePage(use_final))
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
            _loadImageSession.Next();
            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                ReaderFrameViewModel model = FrameDataSource[i];
                model.ImageLeftSet = false;
                model.ImageRightSet = false;
            }
            return;
        }

        int frame = PageToFrame(Page, out _, out _);
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
                        Model = new SimpleImageView.Model
                        {
                            Source = m.ImageSource,
                            Dispatcher = new TaskQueueDispatcher(_updateImageQueue, "ReaderLoadImage"),
                        },
                        ImageResultHandler = new LoadImageResultHandler(this, i, m, true)
                    });
                }

                if (!m.ImageRightSet && m.PageR > 0)
                {
                    m.ImageRightSet = true;
                    img_loader_tokens.Add(new SimpleImageLoader.Token
                    {
                        Model = new SimpleImageView.Model
                        {
                            Source = m.ImageSource,
                            Dispatcher = new TaskQueueDispatcher(_updateImageQueue, "ReaderLoadImage"),
                        },
                        ImageResultHandler = new LoadImageResultHandler(this, i, m, false)
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

            new SimpleImageLoader.Transaction(_loadImageSession.Token, img_loader_tokens).SetQueue(_updateImageQueue).Commit();
        }
    }

    private class LoadImageResultHandler(ReaderView view, int i, ReaderFrameViewModel model, bool isLeft) : IImageResultHandler
    {
        private readonly ReaderView _view = view;
        private readonly int _index = i;
        private readonly ReaderFrameViewModel _model = model;
        private readonly bool _isLeft = isLeft;

        public void OnSuccess(BitmapImage image)
        {
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

        int frame = PageToFrame(PageFinal, out _, out _);
        frame += increment;
        frame = Math.Min(FrameDataSource.Count - 1, frame);
        frame = Math.Max(0, frame);

        double page = FrameDataSource[frame].Page;
        float? zoom = Zoom > 101f ? 100f : (float?)null;

        return SetScrollViewer2(zoom, page, disable_animation, reason);
    }

    internal sealed class ScrollManager : Utils.BaseTransaction<bool>
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
        if (!_isLoaded)
        {
            return false;
        }

#if DEBUG_LOG_JUMP
        Log("ParamIn:"
            + $" Reason={reason}"
            + $",Z={ctx.zoom}"
            + $",H={ctx.horizontalOffset}"
            + $",V={ctx.verticalOffset}"
            + $",D={ctx.disableAnimation}");
#endif

        SetScrollViewerZoom(ctx, out float? zoom_out);

#if DEBUG_LOG_JUMP
        Log("ParamOut:"
            + $" Z={zoom_out}"
            + $",H={ctx.horizontalOffset}"
            + $",V={ctx.verticalOffset}"
            + $",D={ctx.disableAnimation}"
            + $",ZN={ctx.zoom}");
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
            if (frame_new < 0 || frame_new >= FrameDataSource.Count)
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

        if (ctx.zoomType == ZoomType.CenterCrop)
        {
            zoom *= zoom_coefficient_new.Max() / zoom_coefficient_new.Min();
        }

        double maxZoom = Math.Max(MAX_ZOOM, 100 * zoom_coefficient_new.Max() / zoom_coefficient_new.Min());
        zoom = Math.Min(zoom, maxZoom);
        zoom = Math.Max(zoom, MIN_ZOOM);
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

#if DEBUG_LOG_JUMP
        Log("Commit:"
            + " Z=" + ZoomFactorFinal.ToString()
            + ",H=" + HorizontalOffsetFinal.ToString()
            + ",V=" + VerticalOffsetFinal.ToString()
            + ",D=" + DisableAnimationFinal.ToString());
#endif

        ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, DisableAnimationFinal);
        return true;
    }

    private void AdjustParallelOffset()
    {
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        double? movement_forward = null;
        double? movement_backward = null;
        double screen_center_offset = ViewportParallelLength * 0.5 + ParallelOffsetFinal;

        if (_frameManager.GetContainer(0) != null)
        {
            double space = PaddingStartFinal * ZoomFactorFinal - ParallelOffsetFinal;
            double image_center_offset = (PaddingStartFinal + FrameParallelLength(0) * 0.5) * ZoomFactorFinal;
            double image_center_to_screen_center = image_center_offset - screen_center_offset;
            movement_forward = Math.Min(space, image_center_to_screen_center);
        }

        if (_frameManager.GetContainer(FrameDataSource.Count - 1) != null)
        {
            double space = PaddingEndFinal * ZoomFactorFinal - (ExtentParallelLengthFinal
                - ParallelOffsetFinal - ViewportParallelLength);
            double image_center_offset = ExtentParallelLengthFinal - (PaddingEndFinal
                + FrameParallelLength(FrameDataSource.Count - 1) * 0.5) * ZoomFactorFinal;
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
    public void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
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
            MoveFrame(delta, "PageTurningUsingPointerWheel");
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

    public void OnReaderManipulationCompleted(ManipulationCompletedEventArgs e)
    {
        if (IsContinuous || Zoom >= FORCE_CONTINUOUS_ZOOM_THRESHOLD)
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
    public bool OnViewChanged(bool final)
    {
        if (!_isLoaded)
        {
            return false;
        }

        if (!IsCurrentReader)
        {
            // Clear images.
            UpdateImagesInternal();
            return false;
        }

        if (!UpdatePage(false))
        {
            return false;
        }

        if (final)
        {
            SyncFinalVal();

            // Notify the scroll viewer to update its inner states.
            SetScrollViewer1(null, null, false, "AdjustInnerStateAfterViewChanged");

            if (!IsContinuous && Zoom < FORCE_CONTINUOUS_ZOOM_THRESHOLD)
            {
                // Stick our view to the center of two pages.
                MoveFrameInternal(0, false, "StickToCenter");
            }

#if DEBUG_LOG_VIEW_CHANGE
            Log("ViewChanged:"
                + $" Z={ZoomFactorFinal}"
                + $",H={HorizontalOffsetFinal}"
                + $",V={VerticalOffsetFinal}"
                + $",P={PageSource}");
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
        // Set reader status to Loading.
        _viewModel.ReaderStatusLiveData.Emit(ReaderPageViewModel.ReaderStatusEnum.Loading);

        // Save previous states.
        double page = PageSource;
        float zoom = Math.Min(Zoom, 100f);

        // Update Frames.
        ResetFrames();

        DoFinalize();

        // Jump to previous page.
        // Do NOT disable animation here or else TransformToVisual (which will be
        // called later in OnViewChanged) will give erroneous results.
        // Still don't know why. Been stuck here for 4h.
        SetScrollViewer2(zoom, page, false, "JumpToPreviousPageAfterRearrange");

        // Update images.
        UpdateImages(true);

        // Recover reader status.
        _viewModel.ReaderStatusLiveData.Emit(ReaderPageViewModel.ReaderStatusEnum.Working);
    }

    public void DoFinalize()
    {
        for (int i = FrameDataSource.Count - 1; i >= 0; --i)
        {
            ReaderFrameViewModel frame = FrameDataSource[i];

            if (frame.PageL == -1 && frame.PageR == -1)
            {
                FrameDataSource.RemoveAt(i);
            }
            else
            {
                break;
            }
        }

        AdjustPadding();
    }

    // Internal - Variables
    private ReaderPageViewModel _viewModel;

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
}
