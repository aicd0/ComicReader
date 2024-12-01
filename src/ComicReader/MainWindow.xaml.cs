// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;

using ComicReader.Common;
using ComicReader.Common.Native;
using ComicReader.Views.Main;

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Storage;

using WinRT.Interop;

namespace ComicReader;

public sealed partial class MainWindow : Window
{
    public IntPtr WindowHandle { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        WindowHandle = WindowNative.GetWindowHandle(this);

        Title = StringResourceProvider.GetResourceString("AppDisplayName");
        TrySetAcrylicBackdrop();
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        var placement = new NativeModels.WindowPlacement();
        NativeMethods.GetWindowPlacement(WindowHandle, out placement);
        string serialized = JsonSerializer.Serialize(placement);
        ApplicationData.Current.LocalSettings.Values[LocalSettings.WindowStates] = serialized;
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        TryRecoverWindowStates();
        PageFrame.Navigate(typeof(MainPage));
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
        object windowStates = ApplicationData.Current.LocalSettings.Values[LocalSettings.WindowStates];
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
}
