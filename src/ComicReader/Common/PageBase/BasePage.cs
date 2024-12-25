// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Common.PageBase;

internal abstract class BasePage : Page
{
    private PageCommunicator _communicator;
    private PointerPoint _lastPointerPoint;

    private bool _isStarted = false;
    private bool _isResumed = false;
    private bool _isLoaded = false;
    private bool _requireStop = false;

    private readonly PageStopEventHandler _pageStopHandler;

    public BasePage()
    {
        _pageStopHandler = delegate
        {
            _requireStop = true;
            TryStop();
        };

        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), true);
    }

    protected sealed override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        switch (e.NavigationMode)
        {
            case NavigationMode.New:
            case NavigationMode.Back:
            case NavigationMode.Forward:
                TryStart(e.Parameter);
                TryResume();
                break;
            case NavigationMode.Refresh:
                break;
        }
    }

    protected sealed override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        switch (e.NavigationMode)
        {
            case NavigationMode.New:
            case NavigationMode.Back:
            case NavigationMode.Forward:
                TryPause();
                TryStop();
                break;
            case NavigationMode.Refresh:
                break;
        }
    }

    protected virtual void OnStart(PageBundle bundle)
    {
    }

    protected virtual void OnResume()
    {
    }

    protected virtual void OnPause()
    {
    }

    protected virtual void OnStop()
    {
    }

    protected T GetAbility<T>() where T : class
    {
        return _communicator.GetAbility<T>();
    }

    protected bool CanHandleTapped()
    {
        if (_lastPointerPoint == null)
        {
            return true;
        }

        if (_lastPointerPoint.Properties.IsXButton1Pressed || _lastPointerPoint.Properties.IsXButton2Pressed)
        {
            return false;
        }

        return true;
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        if (!(sender as Page).IsLoaded)
        {
            return;
        }

        _isLoaded = true;
        TryResume();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if ((sender as Page).IsLoaded)
        {
            return;
        }

        _isLoaded = false;
        TryPause();
    }

    private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _lastPointerPoint = e.GetCurrentPoint((UIElement)sender);
    }

    private void TryStart(object p)
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        if (p is NavigationBundle bundle)
        {
            _communicator = bundle.Communicator;
            _communicator.GetAbility<ICommonPageAbility>()?.RegisterPageStopHandler(_pageStopHandler);
            OnStart(bundle.Bundle);
        }
        else
        {
            OnStart(null);
        }
    }

    private void TryResume()
    {
        if (!_isStarted || !_isLoaded || _isResumed)
        {
            return;
        }

        _isResumed = true;
        OnResume();
    }

    private void TryPause()
    {
        if (!_isResumed)
        {
            return;
        }

        _isResumed = false;
        OnPause();

        if (_requireStop)
        {
            TryStop();
        }
    }

    private void TryStop()
    {
        if (_isResumed || !_isStarted)
        {
            return;
        }

        _isStarted = false;
        _communicator.GetAbility<ICommonPageAbility>()?.UnregisterPageStopHandler(_pageStopHandler);
        OnStop();
    }
}
