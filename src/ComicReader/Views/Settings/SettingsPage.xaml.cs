// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.AppEnvironment;
using ComicReader.Common.DebugTools;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Views.Main;

using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Storage;

namespace ComicReader.Views.Settings;

public enum AppearanceSetting
{
    Light,
    Dark,
    UseSystemSetting,
    None
}

public class SettingsPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private AppearanceSetting _initialAppearance = AppearanceSetting.None;

    public bool Updating { get; set; } = false;

    public void Initialize()
    {
        InitializeAppearance();
    }

    private List<Tuple<string, int>> _encodings = [];
    public List<Tuple<string, int>> Encodings
    {
        get => _encodings;
        set
        {
            _encodings = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Encodings)));
        }
    }

    private int _defaultArchiveCodePageIndex = 0;
    public int DefaultArchiveCodePageIndex
    {
        get => _defaultArchiveCodePageIndex;
        set
        {
            _defaultArchiveCodePageIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DefaultArchiveCodePageIndex)));

            if (!Updating)
            {
                int selectedIndex = value;
                if (selectedIndex >= 0 && selectedIndex < Encodings.Count)
                {
                    AppData.DefaultArchiveCodePage = Encodings[selectedIndex].Item2;
                }
            }
        }
    }

    private bool _transitionAnimation = true;
    public bool TransitionAnimation
    {
        get => _transitionAnimation;
        set
        {
            _transitionAnimation = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransitionAnimation)));

            if (!Updating)
            {
                AppData.TransitionAnimation = value;
            }
        }
    }

    private bool _antiAliasingEnabled = true;
    public bool AntiAliasingEnabled
    {
        get => _antiAliasingEnabled;
        set
        {
            _antiAliasingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AntiAliasingEnabled)));

            if (!Updating)
            {
                AppData.AntiAliasingEnabled = value;
            }
        }
    }

    private bool _isClearHistoryEnabled = false;
    public bool IsClearHistoryEnabled
    {
        get => _isClearHistoryEnabled;
        set
        {
            _isClearHistoryEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsClearHistoryEnabled)));
        }
    }

    private bool _historySaveBrowsingHistory = false;
    public bool HistorySaveBrowsingHistory
    {
        get => _historySaveBrowsingHistory;
        set
        {
            _historySaveBrowsingHistory = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HistorySaveBrowsingHistory)));

            if (!Updating)
            {
                AppData.SaveBrowsingHistory = value;
            }
        }
    }

    private bool _appearanceLightChecked = false;
    public bool AppearanceLightChecked
    {
        get => _appearanceLightChecked;
        set
        {
            _appearanceLightChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceLightChecked)));

            if (!Updating && value)
            {
                SaveAppearance(AppearanceSetting.Light);
            }
        }
    }

    private bool _appearanceDarkChecked = false;
    public bool AppearanceDarkChecked
    {
        get => _appearanceDarkChecked;
        set
        {
            _appearanceDarkChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceDarkChecked)));

            if (!Updating && value)
            {
                SaveAppearance(AppearanceSetting.Dark);
            }
        }
    }

    private bool _appearanceUseSystemSettingChecked = false;
    public bool AppearanceUseSystemSettingChecked
    {
        get => _appearanceUseSystemSettingChecked;
        set
        {
            _appearanceUseSystemSettingChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceUseSystemSettingChecked)));

            if (!Updating && value)
            {
                SaveAppearance(AppearanceSetting.UseSystemSetting);
            }
        }
    }

    private bool _appearanceChanged;
    public bool AppearanceChanged
    {
        get => _appearanceChanged;
        set
        {
            _appearanceChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceChanged)));
        }
    }

    private bool _advancedDebugMode;
    public bool AdvancedDebugMode
    {
        get => _advancedDebugMode;
        set
        {
            _advancedDebugMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdvancedDebugMode)));

            if (!Updating)
            {
                DebugUtils.DebugMode = value;
            }
        }
    }

    private bool _isRescanning = true;
    public bool IsRescanning
    {
        get => _isRescanning;
        set
        {
            _isRescanning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRescanning)));
        }
    }

    private bool _isClearingCache = false;
    public bool IsClearingCache
    {
        get => _isClearingCache;
        set
        {
            _isClearingCache = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsClearingCache)));
        }
    }

    private string _cacheSize = StringResourceProvider.GetResourceString("Calculating");
    public string CacheSize
    {
        get => StringResourceProvider.GetResourceString("ClearCacheDetail").Replace("$size", _cacheSize);
        set
        {
            _cacheSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CacheSize)));
        }
    }

    private void InitializeAppearance()
    {
        object appearanceSetting = ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_APPEARANCE];
        AppearanceSetting appearance;
        if (appearanceSetting == null)
        {
            appearance = AppearanceSetting.UseSystemSetting;
        }
        else
        {
            var appTheme = (ApplicationTheme)(int)appearanceSetting;
            appearance = appTheme switch
            {
                ApplicationTheme.Light => AppearanceSetting.Light,
                ApplicationTheme.Dark => AppearanceSetting.Dark,
                _ => AppearanceSetting.UseSystemSetting,
            };
        }

        _initialAppearance = appearance;
        AppearanceLightChecked = appearance == AppearanceSetting.Light;
        AppearanceDarkChecked = appearance == AppearanceSetting.Dark;
        AppearanceUseSystemSettingChecked = appearance == AppearanceSetting.UseSystemSetting;
    }

    private void SaveAppearance(AppearanceSetting appearance)
    {
        AppearanceChanged = appearance != _initialAppearance;

        string appearanceKey = GlobalConstants.LOCAL_SETTINGS_KEY_APPEARANCE;
        switch (appearance)
        {
            case AppearanceSetting.Light:
            case AppearanceSetting.Dark:
                ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_APPEARANCE] = (int)appearance;
                break;
            case AppearanceSetting.UseSystemSetting:
                ApplicationData.Current.LocalSettings.Values.Remove(appearanceKey);
                break;
        }
    }
}

internal sealed partial class SettingsPage : BasePage
{
    private const string TAG = "SettingsPage";

    private SettingsPageViewModel ViewModel { get; } = new SettingsPageViewModel();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle(StringResourceProvider.GetResourceString("Settings"));
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Setting });
    }

    protected override void OnResume()
    {
        base.OnResume();
        ComicData.OnUpdated += OnComicDataUpdated;

        C0.Run(async delegate
        {
            await Update();
        });
    }

    protected override void OnPause()
    {
        base.OnPause();
        ComicData.OnUpdated -= OnComicDataUpdated;
    }

    //
    // Events
    //

    private void OnDebugModeToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Updating)
        {
            return;
        }

        C0.Run(async delegate
        {
            if (TsDebugMode.IsOn)
            {
                var dialog = new ContentDialog
                {
                    Title = StringResourceProvider.GetResourceString("Warning"),
                    Content = StringResourceProvider.GetResourceString("DebugModeWarning"),
                    PrimaryButtonText = StringResourceProvider.GetResourceString("Proceed"),
                    CloseButtonText = StringResourceProvider.GetResourceString("Cancel"),
                    XamlRoot = XamlRoot
                };
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.None)
                {
                    ViewModel.AdvancedDebugMode = false;
                    return;
                }
            }

            ViewModel.AdvancedDebugMode = TsDebugMode.IsOn;
        });
    }

    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var dialog = new ChooseLocationsDialog();
            await C0.ShowDialogAsync(dialog, XamlRoot);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            await HistoryDataManager.Clear(true);
            ViewModel.IsClearHistoryEnabled = false;
        });
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var uri = new Uri(@"https://github.com/aicd0/ComicReader/issues/new/choose");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        });
    }

    private void OnRescanFilesClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsRescanning = true;
        ComicData.UpdateAllComics("OnRescanFilesClicked", lazy: false);
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsClearingCache = true;
        TaskDispatcher.DefaultQueue.Submit("ClearCache", delegate
        {
            ClearCache();
            string size = GetCacheSize();
            _ = MainThreadUtils.RunInMainThread(() =>
            {
                ViewModel.IsClearingCache = false;
                ViewModel.CacheSize = size;
            });
        });
    }

    //
    // Data Update
    //

    private async Task Update()
    {
        ViewModel.Updating = true;

        await XmlDatabaseManager.WaitLock();
        ViewModel.IsClearHistoryEnabled = XmlDatabase.History.Items.Count > 0;
        XmlDatabaseManager.ReleaseLock();

        ViewModel.TransitionAnimation = AppData.TransitionAnimation;
        ViewModel.HistorySaveBrowsingHistory = AppData.SaveBrowsingHistory;
        ViewModel.AntiAliasingEnabled = AppData.AntiAliasingEnabled;
        ViewModel.AdvancedDebugMode = DebugUtils.DebugMode;

        ViewModel.Initialize();
        UpdateCodePages();
        UpdateCacheSize();
        UpdateRescanStatus();
        UpdateStatistis();
        UpdateFeedback();
        UpdateAbout();
        UpdateDebugInformation();

        ViewModel.Updating = false;
    }

    private void OnComicDataUpdated()
    {
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            UpdateRescanStatus();
            UpdateStatistis();
        });
    }

    private void UpdateCodePages()
    {
        C0.Run(async delegate
        {
            ReadOnlyDictionary<int, Encoding> supportedEncodings = await AppInfoProvider.GetSupportedEncodings();
            var encodings = new List<Tuple<string, int>>
            {
                new(StringResourceProvider.GetResourceString("Default"), -1)
            };

            int defaultCodePage = AppData.DefaultArchiveCodePage;
            int selectedIndex = 0;
            foreach (Encoding info in supportedEncodings.Values)
            {
                string title = info.EncodingName + " [" + info.CodePage.ToString() + "]";
                encodings.Add(new Tuple<string, int>(title, info.CodePage));
                if (defaultCodePage == info.CodePage)
                {
                    selectedIndex = encodings.Count - 1;
                }
            }
            ViewModel.Encodings = encodings;

            if (!supportedEncodings.ContainsKey(defaultCodePage))
            {
                AppData.DefaultArchiveCodePage = -1;
                selectedIndex = 0;
            }

            ViewModel.DefaultArchiveCodePageIndex = selectedIndex;
        });
    }

    private void UpdateStatistis()
    {
        C0.Run(async delegate
        {
            long comicCount = 0;
            await ComicData.CommandBlock2(async delegate (SqliteCommand command)
            {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command.CommandText = "SELECT COUNT(*) FROM " + ComicTable.Instance.GetTableName();
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                comicCount = (long)await command.ExecuteScalarAsync();
            }, "SettingUpdateStatistics");
            string total_comic_string = StringResourceProvider.GetResourceString("TotalComics");
            StatisticsTextBlock.Text = total_comic_string +
                comicCount.ToString("#,#0", CultureInfo.InvariantCulture);
        });
    }

    private void UpdateRescanStatus()
    {
        ViewModel.IsRescanning = ComicData.IsRescanning;
    }

    private void UpdateFeedback()
    {
        string appName = StringResourceProvider.GetResourceString("AppDisplayName");
        string contributionBeforeLink = StringResourceProvider.GetResourceString("ContributionRunBeforeLink");
        contributionBeforeLink = contributionBeforeLink.Replace("$appname", appName);
        ContributionRunBeforeLink.Text = contributionBeforeLink;
        ContributionRunAfterLink.Text = StringResourceProvider.GetResourceString("ContributionRunAfterLink");
    }

    private void UpdateAbout()
    {
        string appName = StringResourceProvider.GetResourceString("AppDisplayName");
        AboutBuildVersionControl.Text = appName + " " + EnvironmentProvider.Instance.GetVersionName();

        string author = "aicd0";
        string aboutCopyright = StringResourceProvider.GetResourceString("AboutCopyright");
        aboutCopyright = aboutCopyright.Replace("$author", author);
        AboutCopyrightControl.Text = aboutCopyright;
    }

    private void UpdateDebugInformation()
    {
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        TbDebugInformation.Text = sb.ToString();
    }

    private void UpdateCacheSize()
    {
        TaskDispatcher.DefaultQueue.Submit("CalculateCacheSize", delegate
        {
            string size = GetCacheSize();
            _ = MainThreadUtils.RunInMainThread(() =>
            {
                ViewModel.CacheSize = size;
            });
        });
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private static void ClearCache()
    {
        var cacheDir = new DirectoryInfo(ApplicationData.Current.LocalCacheFolder.Path);

        foreach (FileInfo file in cacheDir.GetFiles())
        {
            try
            {
                file.Delete();
            }
            catch (IOException e)
            {
                Logger.E(TAG, "ClearCache", e);
            }
        }

        foreach (DirectoryInfo dir in cacheDir.GetDirectories())
        {
            if (dir.Name == "Local")
            {
                continue;
            }

            try
            {
                dir.Delete(true);
            }
            catch (IOException e)
            {
                Logger.E(TAG, "ClearCache", e);
            }
        }
    }

    private static string GetCacheSize()
    {
        var d = new DirectoryInfo(ApplicationData.Current.LocalCacheFolder.Path);
        long size = GetCacheSize(d);
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format("{0:0.##} {1}", size, sizes[order]);
    }

    public static long GetCacheSize(DirectoryInfo directory)
    {
        long size = 0;

        {
            FileInfo[] files;
            try
            {
                files = directory.GetFiles();
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetCacheSize", e);
                files = [];
            }

            foreach (FileInfo file in files)
            {
                try
                {
                    size += file.Length;
                }
                catch (Exception e)
                {
                    Logger.E(TAG, "GetCacheSize", e);
                }
            }
        }

        {
            DirectoryInfo[] subDirectories;
            try
            {
                subDirectories = directory.GetDirectories();
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetCacheSize", e);
                subDirectories = [];
            }

            foreach (DirectoryInfo subDirectory in subDirectories)
            {
                if (subDirectory.Name == "Local")
                {
                    continue;
                }

                size += FileUtils.GetDirectorySize(subDirectory, ignoreErrors: true);
            }
        }

        return size;
    }
}
