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
using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;
using ComicReader.Database;
using ComicReader.Router;
using ComicReader.Views.Base;
using ComicReader.Views.Main;

using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.ApplicationModel;
using Windows.Storage;

namespace ComicReader.Views.Settings;

public enum AppearanceSetting
{
    Light,
    Dark,
    UseSystemSetting,
    None
}

public class SettingsPageShared : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public Action OnSettingsChanged;

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

    private int _defaultArchiveCodePage = -2;
    public int DefaultArchiveCodePage
    {
        get => _defaultArchiveCodePage;
        set
        {
            if (_defaultArchiveCodePage != value)
            {
                _defaultArchiveCodePage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DefaultArchiveCodePage)));
                OnSettingsChanged?.Invoke();
            }
        }
    }

    private bool m_TransitionAnimation = true;
    public bool TransitionAnimation
    {
        get => m_TransitionAnimation;
        set
        {
            if (m_TransitionAnimation != value)
            {
                m_TransitionAnimation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TransitionAnimation"));
                OnSettingsChanged?.Invoke();
            }
        }
    }

    private bool m_IsClearHistoryEnabled = false;
    public bool IsClearHistoryEnabled
    {
        get => m_IsClearHistoryEnabled;
        set
        {
            m_IsClearHistoryEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsClearHistoryEnabled"));
        }
    }

    private bool m_HistorySaveBrowsingHistory = false;
    public bool HistorySaveBrowsingHistory
    {
        get => m_HistorySaveBrowsingHistory;
        set
        {
            if (m_HistorySaveBrowsingHistory != value)
            {
                m_HistorySaveBrowsingHistory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HistorySaveBrowsingHistory"));
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public AppearanceSetting CurrentAppearance { get; set; }

    private AppearanceSetting m_Appearance = AppearanceSetting.None;
    public AppearanceSetting Appearance
    {
        get => m_Appearance;
        set
        {
            if (m_Appearance != value)
            {
                m_Appearance = value;
                AppearanceLightChecked = m_Appearance == AppearanceSetting.Light;
                AppearanceDarkChecked = m_Appearance == AppearanceSetting.Dark;
                AppearanceUseSystemSettingChecked = m_Appearance == AppearanceSetting.UseSystemSetting;
                AppearanceChanged = m_Appearance != CurrentAppearance;
                OnSettingsChanged?.Invoke();
            }
        }
    }

    private bool m_AppearanceLightChecked = false;
    public bool AppearanceLightChecked
    {
        get => m_AppearanceLightChecked;
        set
        {
            if (value != m_AppearanceLightChecked)
            {
                m_AppearanceLightChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.Light;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceLightChecked"));
            }
        }
    }

    private bool m_AppearanceDarkChecked = false;
    public bool AppearanceDarkChecked
    {
        get => m_AppearanceDarkChecked;
        set
        {
            if (value != m_AppearanceDarkChecked)
            {
                m_AppearanceDarkChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.Dark;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceDarkChecked"));
            }
        }
    }

    private bool m_AppearanceUseSystemSettingChecked = false;
    public bool AppearanceUseSystemSettingChecked
    {
        get => m_AppearanceUseSystemSettingChecked;
        set
        {
            if (value != m_AppearanceUseSystemSettingChecked)
            {
                m_AppearanceUseSystemSettingChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.UseSystemSetting;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceUseSystemSettingChecked"));
            }
        }
    }

    private bool m_AppearanceChanged;
    public bool AppearanceChanged
    {
        get => m_AppearanceChanged;
        set
        {
            m_AppearanceChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceChanged"));
        }
    }

    private bool m_AdvancedDebugMode;
    public bool AdvancedDebugMode
    {
        get => m_AdvancedDebugMode;
        set
        {
            m_AdvancedDebugMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AdvancedDebugMode"));
            OnSettingsChanged?.Invoke();
        }
    }

    private bool m_IsRescanning = true;
    public bool IsRescanning
    {
        get => m_IsRescanning;
        set
        {
            m_IsRescanning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRescanning"));
        }
    }

    private bool _isClearingCache = false;
    public bool IsClearingCache
    {
        get => _isClearingCache;
        set
        {
            _isClearingCache = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsClearingCache"));
        }
    }

    private string _cacheSize = StringResourceProvider.GetResourceString("Calculating");
    public string CacheSize
    {
        get => StringResourceProvider.GetResourceString("ClearCacheDetail").Replace("$size", _cacheSize);
        set
        {
            _cacheSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CacheSize"));
        }
    }
}

internal sealed partial class SettingsPage : BasePage
{
    public const string AppearanceKey = "Appearance";

    private const string TAG = "SettingsPage";

    public SettingsPageShared Shared { get; set; }

    // Initialize _updating to TRUE to avoid copying values from
    // controls (See Save()) while this page is still launching.
    private bool _updating = true;

    public SettingsPage()
    {
        Shared = new SettingsPageShared
        {
            OnSettingsChanged = OnSettingsChanged
        };

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

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    // utilities
    private async Task Update()
    {
        _updating = true;

        await XmlDatabaseManager.WaitLock();
        Shared.TransitionAnimation = XmlDatabase.Settings.TransitionAnimation;
        Shared.IsClearHistoryEnabled = XmlDatabase.History.Items.Count > 0;
        Shared.HistorySaveBrowsingHistory = XmlDatabase.Settings.SaveHistory;
        XmlDatabaseManager.ReleaseLock();

        Shared.AdvancedDebugMode = AppStatusPreserver.DebugMode;

        UpdateAppearance();
        _ = UpdateCodePages();
        UpdateCacheSize();
        UpdateRescanStatus();
        await UpdateStatistis();
        UpdateFeedback();
        UpdateAbout();

        _updating = false;
    }

    private void OnComicDataUpdated()
    {
        MainThreadUtils.RunInMainThreadAsync(async delegate
        {
            UpdateRescanStatus();
            await UpdateStatistis();
        }).Wait();
    }

    //
    // Data Update
    //

    private void UpdateAppearance()
    {
        object appearance_setting = ApplicationData.Current.LocalSettings.Values[AppearanceKey];
        if (appearance_setting == null)
        {
            Shared.CurrentAppearance = AppearanceSetting.UseSystemSetting;
        }
        else if ((ApplicationTheme)(int)appearance_setting == ApplicationTheme.Light)
        {
            Shared.CurrentAppearance = AppearanceSetting.Light;
        }
        else if ((ApplicationTheme)(int)appearance_setting == ApplicationTheme.Dark)
        {
            Shared.CurrentAppearance = AppearanceSetting.Dark;
        }
        Shared.Appearance = Shared.CurrentAppearance;
    }

    private async Task UpdateCodePages()
    {
        ReadOnlyDictionary<int, Encoding> supportedEncodings = await AppInfoProvider.GetSupportedEncodings();
        var encodings = new List<Tuple<string, int>>
        {
            new(StringResourceProvider.GetResourceString("Default"), -1)
        };
        foreach (Encoding info in supportedEncodings.Values)
        {
            string title = info.EncodingName + " [" + info.CodePage.ToString() + "]";
            encodings.Add(new Tuple<string, int>(title, info.CodePage));
        }
        Shared.Encodings = encodings;

        if (!supportedEncodings.ContainsKey(AppStatusPreserver.DefaultArchiveCodePage))
        {
            AppStatusPreserver.DefaultArchiveCodePage = -1;
        }

        Shared.DefaultArchiveCodePage = AppStatusPreserver.DefaultArchiveCodePage;
    }

    private async Task UpdateStatistis()
    {
        long comicCount = 0;
        await ComicData.CommandBlock2(async delegate (SqliteCommand command)
        {
            command.CommandText = "SELECT COUNT(*) FROM " + SqliteDatabaseManager.ComicTable;
            comicCount = (long)await command.ExecuteScalarAsync();
        }, "SettingUpdateStatistics");
        string total_comic_string = StringResourceProvider.GetResourceString("TotalComics");
        StatisticsTextBlock.Text = total_comic_string +
            comicCount.ToString("#,#0", CultureInfo.InvariantCulture);
    }

    private void UpdateRescanStatus()
    {
        Shared.IsRescanning = ComicData.IsRescanning;
    }

    private void UpdateFeedback()
    {
        string appName = StringResourceProvider.GetResourceString("AppDisplayName");
        string contribution_before_link = StringResourceProvider.GetResourceString("ContributionRunBeforeLink");
        contribution_before_link = contribution_before_link.Replace("$appname", appName);
        ContributionRunBeforeLink.Text = contribution_before_link;
        ContributionRunAfterLink.Text = StringResourceProvider.GetResourceString("ContributionRunAfterLink");
    }

    private void UpdateAbout()
    {
#if DEBUG
        string appName = StringResourceProvider.GetResourceString("DevAppDisplayName");
#else
        string appName = Utils.StringResourceProvider.GetResourceString("AppDisplayName");
#endif
        PackageVersion version = Package.Current.Id.Version;
        AboutBuildVersionControl.Text = appName + " " + version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;

        string author = "aicd0";
        string about_copyright = StringResourceProvider.GetResourceString("AboutCopyright");
        about_copyright = about_copyright.Replace("$author", author);
        AboutCopyrightControl.Text = about_copyright;
    }

    private async Task Save()
    {
        // To local settings.
        if (Shared.Appearance == AppearanceSetting.Light)
        {
            ApplicationData.Current.LocalSettings.Values[AppearanceKey] = (int)ApplicationTheme.Light;
        }
        else if (Shared.Appearance == AppearanceSetting.Dark)
        {
            ApplicationData.Current.LocalSettings.Values[AppearanceKey] = (int)ApplicationTheme.Dark;
        }
        else if (Shared.Appearance == AppearanceSetting.UseSystemSetting)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(AppearanceKey);
        }

        // To database.
        await XmlDatabaseManager.WaitLock();
        XmlDatabase.Settings.TransitionAnimation = Shared.TransitionAnimation;
        XmlDatabase.Settings.SaveHistory = Shared.HistorySaveBrowsingHistory;
        XmlDatabaseManager.ReleaseLock();
        TaskQueue.DefaultQueue.Enqueue("SettingsPage#Save", XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));

        AppStatusPreserver.DefaultArchiveCodePage = Shared.DefaultArchiveCodePage;
        AppStatusPreserver.DebugMode = Shared.AdvancedDebugMode;
    }

    private void OnSettingsChanged()
    {
        if (_updating)
        {
            return;
        }

        C0.Run(async delegate
        {
            await Save();
        });
    }

    // events
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
            Shared.IsClearHistoryEnabled = false;
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
        Shared.IsRescanning = true;
        ComicData.UpdateAllComics("OnRescanFilesClicked", lazy: false);
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        Shared.IsClearingCache = true;
        TaskQueue.DefaultQueue.Enqueue("ClearCache", delegate
        {
            ClearCache();
            string size = GetCacheSize();
            _ = MainThreadUtils.RunInMainThread(() =>
            {
                Shared.IsClearingCache = false;
                Shared.CacheSize = size;
            });
            return TaskException.Success;
        });
    }

    //
    // cache
    //

    private void UpdateCacheSize()
    {
        TaskQueue.DefaultQueue.Enqueue("CalculateCacheSize", delegate
        {
            string size = GetCacheSize();
            _ = MainThreadUtils.RunInMainThread(() =>
            {
                Shared.CacheSize = size;
            });
            return TaskException.Success;
        });
    }

    private static void ClearCache()
    {
        var di = new DirectoryInfo(ApplicationData.Current.LocalCacheFolder.Path);
        foreach (FileInfo file in di.GetFiles())
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
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
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
        long size = CalculateDirSize(d);
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
        // show a single decimal place, and no space.
        return string.Format("{0:0.##} {1}", size, sizes[order]);
    }

    private static long CalculateDirSize(DirectoryInfo d)
    {
        long size = 0;
        // Add file sizes.
        FileInfo[] fis = d.GetFiles();
        foreach (FileInfo fi in fis)
        {
            size += fi.Length;
        }
        // Add subdirectory sizes.
        DirectoryInfo[] dis = d.GetDirectories();
        foreach (DirectoryInfo di in dis)
        {
            size += CalculateDirSize(di);
        }
        return size;
    }
}
