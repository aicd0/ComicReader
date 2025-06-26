// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Services;
using ComicReader.Data;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.ServiceManagement;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Globalization;

using Windows.ApplicationModel.Activation;

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
        // Register crash handler
        UnhandledException += (_, e) =>
        {
            SentryManager.CaptureException(e.Exception);
            CrashHandler.OnUnhandledException(e.Exception);
        };

        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());

        // Initialize environment information
        EnvironmentProvider.Instance.Initialize(Properties.AdditionalDebugInformation);

        // Initialize Sentry
        SentryManager.Initialize(Properties.SentryDsn, EnvironmentProvider.GetEnvironmentTags());

        // Initialize app language
        InitializeAppLanguage();

        // Initialize app theme
        InitializeAppTheme();
    }

    private void InitializeAppTheme()
    {
        AppSettingsModel.AppearanceSetting themeSetting = AppSettingsModel.Instance.GetModel().Theme;
        switch (themeSetting)
        {
            case AppSettingsModel.AppearanceSetting.Light:
                Current.RequestedTheme = ApplicationTheme.Light;
                break;
            case AppSettingsModel.AppearanceSetting.Dark:
                Current.RequestedTheme = ApplicationTheme.Dark;
                break;
            default:
                break;
        }
    }

    private void InitializeAppLanguage()
    {
        if (EnvironmentProvider.IsPortable())
        {
            string languageTag = AppSettingsModel.Instance.GetModel().Language;
            if (string.IsNullOrEmpty(languageTag))
            {
                languageTag = EnvironmentProvider.GetCurrentSystemLanguage();
            }
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        }
    }

    private async Task InitializeOnAppLaunch()
    {
        // Initialize debug switches
        DebugSwitchModel.Instance.Initialize();

        // Initialize logger
        Logger.Initialize();

        // Initialize databases
        await XmlDatabaseManager.Initialize();
        SqlDatabaseManager.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabase();

        // Update comic library
        ComicModel.UpdateAllComics("DatabaseManager#init", lazy: true);
    }
}
