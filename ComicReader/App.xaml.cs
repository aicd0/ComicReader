using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

using ComicReader.Data;

namespace ComicReader
{
    sealed partial class App : Application
    {
        bool m_window_setup = false;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            CoreApplication.EnablePrelaunch(true);
        }

        private void Startup(bool prelaunch_activated, ApplicationExecutionState state)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(Views.RootPage));
                Utils.BackgroundTasks.AppendTask(DataManager.LoadDatabaseSealed(), "Loading...");
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

                ApplicationView appView = ApplicationView.GetForCurrentView();
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

                if (!localSettings.Values.ContainsKey("VeryFirstLaunch"))
                {
                    localSettings.Values.Add("VeryFirstLaunch", false);
                }
                else
                {
                    ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
                }

                appView.SetPreferredMinSize(minWindowSize);
                //appView->TryResizeView(SizeHelper::FromDimensions(320, 700));
            }

            if (!Window.Current.Visible)
            {
                Window.Current.Activate();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Startup(args.PrelaunchActivated, args.PreviousExecutionState);
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            base.OnFileActivated(args);
            Startup(false, args.PreviousExecutionState);
            Utils.BackgroundTasks.AppendTask(Views.RootPage.Current.OnFileActivatedSealed(args));
        }
    }
}
