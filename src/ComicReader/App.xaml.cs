using ComicReader.Database;
using ComicReader.Views;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace ComicReader
{
    using TaskResult = Utils.TaskResult;

    sealed partial class App : Application
    {
        private bool m_window_setup = false;

        public App()
        {
            // get and apply the appearance setting.
            object appearance_setting = ApplicationData.Current.LocalSettings.Values[Views.SettingsPage.AppearanceKey];
            if (appearance_setting != null)
            {
                Current.RequestedTheme = (ApplicationTheme)(int)appearance_setting;
            }

            InitializeComponent();

            Suspending += OnSuspending;
            CoreApplication.EnablePrelaunch(true);

            if (Keys.AppSecret.Length > 0)
            {
                AppCenter.Start(Keys.AppSecret, typeof(Analytics), typeof(Crashes));
            }
        }

        private async Task Startup(bool prelaunch_activated)
        {
            // Initialize the database if it has not been initialized.
            TaskResult result = await DatabaseManager.Init();
            System.Diagnostics.Debug.Assert(result.Successful);

            // Perform usual startup.
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(Views.MainPage));
            }

            if (prelaunch_activated)
            {
                return;
            }

            if (!m_window_setup)
            {
                m_window_setup = true;
                float minWindowWidth = (float)(double)Resources["AppMinWindowWidth"];
                float minWindowHeight = (float)(double)Resources["AppMinWindowHeight"];
                Size minWindowSize = SizeHelper.FromDimensions(minWindowWidth, minWindowHeight);
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                ApplicationView appView = ApplicationView.GetForCurrentView();

                if (!localSettings.Values.ContainsKey("VeryFirstLaunch"))
                {
                    localSettings.Values.Add("VeryFirstLaunch", false);
                }
                else
                {
                    ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
                }

                appView.SetPreferredMinSize(minWindowSize);
                // appView->TryResizeView(SizeHelper::FromDimensions(320, 700));
            }

            if (!Window.Current.Visible)
            {
                Window.Current.Activate();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Utils.C0.Run(async delegate
            {
                await Startup(args.PrelaunchActivated);
            });
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            base.OnFileActivated(args);
            Utils.C0.Run(async delegate
            {
                await MainPage.OnFileActivated(args);
                await Startup(false);
            });
        }
    }
}
