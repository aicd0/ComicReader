// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.AppEnvironment;
using ComicReader.Common.Services;
using ComicReader.Data;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.ServiceManagement;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Sentry;

using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace ComicReader;

public partial class App : Application
{
    private const string TAG = nameof(App);

    internal static readonly WindowManager<MainWindow> WindowManager = new();

    public App()
    {
        InitializeBeforeAppCreate();
        InitializeComponent();
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

        await InitializeOnAppLaunch();

        // Initialize MainWindow here
        var window = new MainWindow("");
        window.Activate();

        mainInstance.Activated += OnActivated;
        OnActivated(null, activatedEventArgs);
    }

    private void OnActivated(object? sender, AppActivationArguments e)
    {
        if (e.Kind == ExtendedActivationKind.File)
        {
            MainWindow? window = WindowManager.GetAnyWindow();
            if (window != null)
            {
                window.OnFileActivated((FileActivatedEventArgs)e.Data);
            }
            else
            {
                Logger.F(TAG, "Failed to perform file activation, no window is found.");
            }
        }
    }

    private void InitializeBeforeAppCreate()
    {
        // Initialize crash handler
        UnhandledException += (_, e) =>
        {
            if (Properties.SentryDsn.Length > 0)
            {
                SentrySdk.CaptureException(e.Exception);
            }
            CrashHandler.OnUnhandledException(e.Exception);
        };

        // Initialize Sentry
        if (Properties.SentryDsn.Length > 0)
        {
            SentrySdk.Init(o =>
            {
                o.Dsn = Properties.SentryDsn;
                EnvironmentProvider.Instance.AppendEnvironmentTags(o.DefaultTags);
            });
        }

        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());

        // Initialize debug switches
        DebugSwitchModel.Instance.Initialize().Wait();

        // Initialize logger
        Logger.Initialize();

        // Apply app theme
        ApplyAppTheme();
    }

    private async Task InitializeOnAppLaunch()
    {
        // Initialize databases
        await XmlDatabaseManager.Initialize();
        SqlDatabaseManager.Initialize();
        await DatabaseUpgradeManager.Instance.UpgradeDatabase();

        // Update comic library
        ComicModel.UpdateAllComics("DatabaseManager#init", lazy: true);
    }

    private void ApplyAppTheme()
    {
        object appearanceSetting = ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_APPEARANCE];
        if (appearanceSetting != null)
        {
            Current.RequestedTheme = (ApplicationTheme)(int)appearanceSetting;
        }
    }
}
