// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.AppEnvironment;
using ComicReader.Common.DebugTools;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Views.Main;

using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace ComicReader;

public partial class App : Application
{
    public static MainWindow Window { get; private set; }

    public App()
    {
        UnhandledException += CrashHandler.OnUnhandledException;
        EnvironmentProvider.Instance.Initialize();
        ApplyAppTheme();
        InitializeComponent();
        StartAppCenter();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
    {
        // Read: https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#single-instancing-in-applicationonlaunched
        // If this is the first instance launched, then register it as the "main" instance.
        // If this isn't the first instance launched, then "main" will already be registered,
        // so retrieve it.
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        AppActivationArguments activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // If the instance that's executing the OnLaunched handler right now
        // isn't the "main" instance.
        if (!mainInstance.IsCurrent)
        {
            // Redirect the activation (and args) to the "main" instance, and exit.
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        await PerformInitialization();

        // Initialize MainWindow here
        Window = new MainWindow();
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

    private void ApplyAppTheme()
    {
        object appearanceSetting = ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_APPEARANCE];
        if (appearanceSetting != null)
        {
            Current.RequestedTheme = (ApplicationTheme)(int)appearanceSetting;
        }
    }

    private void StartAppCenter()
    {
        string appSecret = Properties.AppCenterSecret;
        if (appSecret.Length > 0)
        {
            AppCenter.Start(appSecret, typeof(Analytics), typeof(Crashes));
        }
    }

    private async Task PerformInitialization()
    {
        Logger.Initialize();
        await XmlDatabaseManager.Initialize();
        await SqliteDatabaseManager.Initialize(XmlDatabase.Settings.DatabaseVersion);
        ComicData.UpdateAllComics("DatabaseManager#init", lazy: true);
    }
}
