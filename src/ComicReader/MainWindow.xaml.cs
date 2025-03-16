// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;

using ComicReader.Common;
using ComicReader.Common.Native;
using ComicReader.Helpers.Navigation;
using ComicReader.Views.Main;

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.ApplicationModel.Activation;
using Windows.Storage;

using WinRT.Interop;

namespace ComicReader;

// Do NOT write heavy logic in this class, as WinUI 3 SDK could be buggy.
public sealed partial class MainWindow : Window
{
    //
    // Member variables
    //

    // IMPORTANT: Any resources or views that are created or stored directly in this class
    // must be cleaned up in the OnWindowClosed method, or there will be memory leaks.
    // See http://github.com/microsoft/microsoft-ui-xaml/issues/7282 for more details.

    private MainPage _mainPage;
    private string _url;

    //
    // Constructors
    //

    public MainWindow(string url)
    {
        _url = url;

        InitializeComponent();
        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        Title = StringResourceProvider.GetResourceString("AppDisplayName");
        ExtendsContentIntoTitleBar = true;
        TrySetAcrylicBackdrop();
    }

    //
    // Public Interfaces
    //

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }

    public void OnFileActivated(FileActivatedEventArgs args)
    {
        _mainPage?.OnFileActivated(args);
    }

    //
    // Event Handlers
    //

    // IMPORTANT: Handles event registration and unregistration in the code-behind file.
    // Do NOT use XAML for this purpose.

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        if (_mainPage == null)
        {
            return;
        }

        var placement = new NativeModels.WindowPlacement();
        NativeMethods.GetWindowPlacement(WindowHandle, out placement);
        string serialized = JsonSerializer.Serialize(placement);
        ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_WINDOW_STATES] = serialized;
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        TryRecoverWindowStates();

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN)
            .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString())
            .WithParam(RouterConstants.ARG_URL, _url);
        AppRouter.OpenInFrame(PageFrame, route);
        _mainPage = (MainPage)PageFrame.Content;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _mainPage.CloseAllTabs();
        PageFrame.Loaded -= OnPageFrameLoaded;
        App.WindowManager.UnregisterWindow(WindowId);

        _mainPage = null;
        _url = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    //
    // Private Helpers
    //

    private void TrySetAcrylicBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            var desktopAcrylicBackdrop = new DesktopAcrylicBackdrop();
            SystemBackdrop = desktopAcrylicBackdrop;
        }
    }

    private void TryRecoverWindowStates()
    {
        object windowStates = ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_WINDOW_STATES];
        if (windowStates is not string windowStatesJson)
        {
            return;
        }

        NativeModels.WindowPlacement windowPlacement;
        try
        {
            windowPlacement = JsonSerializer.Deserialize<NativeModels.WindowPlacement>(windowStatesJson);
        }
        catch (Exception)
        {
            return;
        }

        NativeMethods.SetWindowPlacement(WindowHandle, ref windowPlacement);
    }
}
