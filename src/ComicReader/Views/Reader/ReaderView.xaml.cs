// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.DesignData;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.System;
using Windows.UI.Core;

namespace ComicReader.Views.Reader;

internal partial class ReaderView : UserControl
{
    private bool _isLoaded;
    private bool _isVertical = true;
    private bool _isContinuous = true;
    private bool _isVisible = true;
    private bool _isLeftToRight = true;
    private bool _uiStateUpdatedVisibility = true;
    private bool _uiStateUpdatedOrientation = true;
    private bool _uiStateUpdatedContinuous = true;
    private bool _uiStateUpdatedFlowDirection = true;

    private bool _tapPending = false;
    private bool _tapCancelled = false;
    private readonly UIElement _gestureReference;
    private readonly GestureHandler _gestureHandler;
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();

    public delegate void ReaderEventTappedEventHandler(ReaderView sender);
    public event ReaderEventTappedEventHandler ReaderEventTapped;

    public delegate void ReaderEventPageChangedEventHandler(ReaderView sender, bool isIntermediate);
    public event ReaderEventPageChangedEventHandler ReaderEventPageChanged;

    public ReaderViewController Controller { get; set; }
    private ReaderViewController ControllerInternal => Controller;

    public int CurrentPage => ControllerInternal?.GetCurrentPage() ?? 0;
    public int PageCount => ControllerInternal?.PageCount ?? 0;

    public ReaderView()
    {
        InitializeComponent();

        Loaded += OnLoadedOrUnloaded;
        Unloaded += OnLoadedOrUnloaded;

        _gestureReference = this;
        _gestureHandler = new(this);
        _gestureRecognizer.SetHandler(_gestureHandler);
    }

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

    private void OnLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        bool isLoaded = IsLoaded;
        if (_isLoaded == isLoaded)
        {
            return;
        }
        _isLoaded = isLoaded;

        if (isLoaded)
        {
            UpdateUI();
        }
        else
        {
            ControllerInternal.StopLoadingImage();
        }
    }

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

    private void OnReaderScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        ControllerInternal.ThisScrollViewer = sender as ScrollViewer;
    }

    private void OnReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ControllerInternal.OnSizeChanged();
    }

    private void OnReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            if (await ControllerInternal.OnViewChanged(!e.IsIntermediate))
            {
                ReaderEventPageChanged?.Invoke(this, e.IsIntermediate);
            }
        });
    }

    private void OnReaderContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderFrameViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderFrame;
        if (viewHolder == null)
        {
            return;
        }
        viewHolder.Bind(item);
    }

    private void OnReaderListViewLoaded(object sender, RoutedEventArgs e)
    {
        ControllerInternal.ThisListView = sender as ListView;
    }

    private void OnReaderKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ReaderViewController reader = ControllerInternal;

        if (reader == null)
        {
            return;
        }

        Utils.C0.Run(async delegate
        {
            bool handled = true;

            switch (e.Key)
            {
                case VirtualKey.Right:
                    if (!_isVertical && !_isLeftToRight)
                    {
                        await reader.MoveFrame(-1, "JumpToPreviousPageUsingRightKey");
                    }
                    else
                    {
                        await reader.MoveFrame(1, "JumpToNextPageUsingRightKey");
                    }

                    break;

                case VirtualKey.Left:
                    if (!_isVertical && !_isLeftToRight)
                    {
                        await reader.MoveFrame(1, "JumpToNextPageUsingLeftKey");
                    }
                    else
                    {
                        await reader.MoveFrame(-1, "JumpToPreviousPageUsingLeftKey");
                    }

                    break;

                case VirtualKey.Up:
                    await reader.MoveFrame(-1, "JumpToPerviousPageUsingUpKey");
                    break;

                case VirtualKey.Down:
                    await reader.MoveFrame(1, "JumpToNextPageUsingDownKey");
                    break;

                case VirtualKey.PageUp:
                    await reader.MoveFrame(-1, "JumpToPerviousPageUsingPgUpKey");
                    break;

                case VirtualKey.PageDown:
                    await reader.MoveFrame(1, "JumpToNextPageUsingPgDownKey");
                    break;

                case VirtualKey.Home:
                    ReaderViewController.ScrollManager.BeginTransaction(reader, "JumpToFirstPageUsingHomeKey")
                        .Page(1)
                        .Commit();
                    break;

                case VirtualKey.End:
                    ReaderViewController.ScrollManager.BeginTransaction(reader, "JumpToLastPageUsingEndKey")
                        .Page(reader.PageCount)
                        .Commit();
                    break;

                case VirtualKey.Space:
                    await reader.MoveFrame(1, "JumpToNextPageUsingSpaceKey");
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
        Utils.C0.Run(async delegate
        {
            // Ctrl key down indicates the user is zooming the page. In that case we shouldn't handle the event.
            CoreVirtualKeyStates ctrl_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

            if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
            {
                return;
            }

            ReaderViewController reader = ControllerInternal;

            if (reader == null)
            {
                return;
            }

            await reader.OnReaderScrollViewerPointerWheelChanged(e);
        });
    }

    private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
    {
        ControllerInternal?.OnReaderManipulationStarted(e);
    }

    private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
    {
        ControllerInternal?.OnReaderManipulationUpdated(e);
    }

    private void OnReaderManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        ReaderViewController reader = ControllerInternal;
        if (reader == null)
        {
            return;
        }

        Utils.C0.Run(async delegate
        {
            await reader.OnReaderManipulationCompleted(e);
        });
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
            ReaderViewController reader = ControllerInternal;
            if (reader == null)
            {
                return;
            }

            if (Math.Abs(reader.Zoom - 100) <= 1)
            {
                ReaderViewController.ScrollManager.BeginTransaction(reader, "FitScreenUsingCenterCrop")
                    .Zoom(100, Common.Structs.ZoomType.CenterCrop)
                    .EnableAnimation()
                    .Commit();
            }
            else
            {
                ReaderViewController.ScrollManager.BeginTransaction(reader, "FitScreenUsingCenterInside")
                    .Zoom(100)
                    .EnableAnimation()
                    .Commit();
            }
        }
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
}
