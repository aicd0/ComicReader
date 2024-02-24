using ComicReader.Common.Constants;
using ComicReader.Native;
using ComicReader.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Text.Json;
using Windows.Storage;

namespace ComicReader
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TrySetAcrylicBackdrop();
        }

        private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
        {
            TryRecoverWindowStates();
            PageFrame.Navigate(typeof(MainPage));
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

                NativeMethods.SetWindowPlacement(App.WindowHandle, ref windowPlacement);
            }
        }

        private void TrySetAcrylicBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                var desktopAcrylicBackdrop = new DesktopAcrylicBackdrop();
                SystemBackdrop = desktopAcrylicBackdrop;
            }
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            var placement = new NativeModels.WindowPlacement();
            NativeMethods.GetWindowPlacement(App.WindowHandle, out placement);
            string serialized = JsonSerializer.Serialize(placement);
            ApplicationData.Current.LocalSettings.Values[LocalSettings.WindowStates] = serialized;
        }
    }
}
