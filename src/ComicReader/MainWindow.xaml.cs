using ComicReader.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System;
using WinRT;
using Windows.Storage;
using ComicReader.Common.Constants;
using System.Text.Json;

namespace ComicReader
{
    public sealed partial class MainWindow : Window
    {
        DesktopAcrylicController m_acrylicController;
        SystemBackdropConfiguration m_configurationSource;

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
                WINDOWPLACEMENT windowPlacement;
                try
                {
                    windowPlacement = JsonSerializer.Deserialize<WINDOWPLACEMENT>((string)windowStates);
                }
                catch (Exception)
                {
                    return;
                }
                SetWindowPlacement(App.WindowHandle, ref windowPlacement);
            }
        }

        private void TrySetAcrylicBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                // Hooking up the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += OnWindowActivated;
                this.Closed += OnWindowClosed;
                ((FrameworkElement)this.Content).ActualThemeChanged += OnWindowThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_acrylicController = new DesktopAcrylicController();
                m_acrylicController.Kind = DesktopAcrylicKind.Base;

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_acrylicController.SetSystemBackdropConfiguration(m_configurationSource);
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_acrylicController != null)
            {
                m_acrylicController.Dispose();
                m_acrylicController = null;
            }
            this.Activated -= OnWindowActivated;
            m_configurationSource = null;
        }

        private void OnWindowThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
            }
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            GetWindowPlacement(App.WindowHandle, out placement);
            var serialized = JsonSerializer.Serialize(placement);
            ApplicationData.Current.LocalSettings.Values[LocalSettings.WindowStates] = serialized;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length { get; set; }
            public int flags { get; set; }
            public ShowWindowCommands showCmd { get; set; }
            public System.Drawing.Point ptMinPosition { get; set; }
            public System.Drawing.Point ptMaxPosition { get; set; }
            public System.Drawing.Rectangle rcNormalPosition { get; set; }
        }

        internal enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }
    }
}
