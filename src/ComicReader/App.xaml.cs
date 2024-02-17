using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.AppCenter;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using Windows.Storage;
using ComicReader.Database;
using ComicReader.Utils;
using ComicReader.Views;
using Windows.ApplicationModel.Activation;
using WinRT.Interop;

namespace ComicReader
{
    public partial class App : Application
    {
        public static MainWindow Window { get; private set; }

        public static IntPtr WindowHandle { get; private set; }

        public App()
        {
            // get and apply the appearance setting.
            object appearance_setting = ApplicationData.Current.LocalSettings.Values[Views.SettingsPage.AppearanceKey];
            if (appearance_setting != null)
            {
                Current.RequestedTheme = (ApplicationTheme)(int)appearance_setting;
            }

            InitializeComponent();

            if (Keys.AppSecret.Length > 0)
            {
                AppCenter.Start(Keys.AppSecret, typeof(Analytics), typeof(Crashes));
            }
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
        {
            // Read: https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#single-instancing-in-applicationonlaunched
            // If this is the first instance launched, then register it as the "main" instance.
            // If this isn't the first instance launched, then "main" will already be registered,
            // so retrieve it.
            var mainInstance = AppInstance.FindOrRegisterForKey("main");
            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            // If the instance that's executing the OnLaunched handler right now
            // isn't the "main" instance.
            if (!mainInstance.IsCurrent)
            {
                // Redirect the activation (and args) to the "main" instance, and exit.
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            // Initialize database here
            TaskException result = await DatabaseManager.Init();
            System.Diagnostics.Debug.Assert(result.Successful());

            // Initialize MainWindow here
            Window = new MainWindow();
            WindowHandle = WindowNative.GetWindowHandle(Window);
            Window.Activate();

            mainInstance.Activated += OnActivated;
            OnActivated(null, activatedEventArgs);
        }

        private void OnActivated(object sender, AppActivationArguments e)
        {
            if (e.Kind == ExtendedActivationKind.File)
            {
                MainPage.OnFileActivated((FileActivatedEventArgs)e.Data);
            }
        }
    }
}
