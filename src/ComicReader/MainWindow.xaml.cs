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

public sealed partial class MainWindow : Window
{
    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }

    private MainPage _mainPage;

    public MainWindow()
    {
        InitializeComponent();
        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        Title = StringResourceProvider.GetResourceString("AppDisplayName");
        ExtendsContentIntoTitleBar = true;
        TrySetAcrylicBackdrop();
    }

    public void OnFileActivated(FileActivatedEventArgs args)
    {
        _mainPage.OnFileActivated(args);
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        var placement = new NativeModels.WindowPlacement();
        NativeMethods.GetWindowPlacement(WindowHandle, out placement);
        string serialized = JsonSerializer.Serialize(placement);
        ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_WINDOW_STATES] = serialized;
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        TryRecoverWindowStates();

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN)
            .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
        AppRouter.OpenInFrame(PageFrame, route);
        _mainPage = (MainPage)PageFrame.Content;
    }

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
        if (windowStates is string)
        {
            NativeModels.WindowPlacement windowPlacement;
            try
            {
                windowPlacement = JsonSerializer.Deserialize<NativeModels.WindowPlacement>((string)windowStates);
            }
            catch (Exception)
            {
                return;
            }

            NativeMethods.SetWindowPlacement(WindowHandle, ref windowPlacement);
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        App.WindowManager.UnregisterWindow(WindowId);
    }
}
