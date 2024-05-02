using ComicReader.Database;
using ComicReader.Router;
using ComicReader.Utils;
using ComicReader.Views.Base;
using ComicReader.Views.Main;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
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

    private List<Tuple<string, int>> m_Encodings = new List<Tuple<string, int>>();
    public List<Tuple<string, int>> Encodings
    {
        get => m_Encodings;
        set
        {
            m_Encodings = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Encodings"));
        }
    }

    private int m_DefaultArchiveCodePage = -2;
    public int DefaultArchiveCodePage
    {
        get => m_DefaultArchiveCodePage;
        set
        {
            if (m_DefaultArchiveCodePage != value)
            {
                m_DefaultArchiveCodePage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DefaultArchiveCodePage"));
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
}

internal class SettingPageBase : BasePage<EmptyViewModel>;

sealed internal partial class SettingsPage : SettingPageBase
{
    public const string AppearanceKey = "Appearance";
    public SettingsPageShared Shared { get; set; }

    // Initialize m_updating to TRUE to avoid copying values from
    // controls (See Save()) while this page is still launching.
    private bool m_updating = true;

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

        Utils.C0.Run(async delegate
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
        m_updating = true;

        // Appearance.
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

        // Supported code pages.
        if (Shared.Encodings.Count == 0)
        {
            var encodings = new List<Tuple<string, int>>
            {
                new Tuple<string, int>(Utils.StringResourceProvider.GetResourceString("Default"), -1)
            };

            foreach (Encoding info in Common.AppInfoProvider.SupportedEncodings.Values)
            {
                string title = info.EncodingName + " [" + info.CodePage.ToString() + "]";
                encodings.Add(new Tuple<string, int>(title, info.CodePage));
            }

            Shared.Encodings = encodings;
        }

        if (!Common.AppInfoProvider.SupportedEncodings.ContainsKey(XmlDatabase.Settings.DefaultArchiveCodePage))
        {
            XmlDatabase.Settings.DefaultArchiveCodePage = -1;
        }

        // From Xml.
        await XmlDatabaseManager.WaitLock();

        Shared.DefaultArchiveCodePage = XmlDatabase.Settings.DefaultArchiveCodePage;
        Shared.TransitionAnimation = XmlDatabase.Settings.TransitionAnimation;
        Shared.IsClearHistoryEnabled = XmlDatabase.History.Items.Count > 0;
        Shared.HistorySaveBrowsingHistory = XmlDatabase.Settings.SaveHistory;
        Shared.AdvancedDebugMode = XmlDatabase.Settings.DebugMode;

        XmlDatabaseManager.ReleaseLock();
        m_updating = false;

        // Rescan status.
        UpdateRescanStatus();

        // Statistics.
        await UpdateStatistis();

        // Feedback.
        string app_name = Utils.StringResourceProvider.GetResourceString("AppDisplayName");
        string contribution_before_link = Utils.StringResourceProvider.GetResourceString("ContributionRunBeforeLink");
        contribution_before_link = contribution_before_link.Replace("$appname", app_name);
        ContributionRunBeforeLink.Text = contribution_before_link;
        ContributionRunAfterLink.Text = Utils.StringResourceProvider.GetResourceString("ContributionRunAfterLink");

        // About.
        PackageVersion version = Package.Current.Id.Version;
        AboutBuildVersionControl.Text = app_name + " " + version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;

        string author = "aicd0";
        string about_copyright = Utils.StringResourceProvider.GetResourceString("AboutCopyright");
        about_copyright = about_copyright.Replace("$author", author);
        AboutCopyrightControl.Text = about_copyright;
    }

    private void OnComicDataUpdated()
    {
        Threading.RunInMainThreadAsync(async delegate
        {
            UpdateRescanStatus();
            await UpdateStatistis();
        }).Wait();
    }

    private async Task UpdateStatistis()
    {
        long comicCount = 0;
        await ComicData.CommandBlock2(async delegate (SqliteCommand command)
        {
            command.CommandText = "SELECT COUNT(*) FROM " + SqliteDatabaseManager.ComicTable;
            comicCount = (long)await command.ExecuteScalarAsync();
        }, "SettingUpdateStatistics");
        string total_comic_string = Utils.StringResourceProvider.GetResourceString("TotalComics");
        StatisticsTextBlock.Text = total_comic_string +
            comicCount.ToString("#,#0", CultureInfo.InvariantCulture);
    }

    private void UpdateRescanStatus()
    {
        Shared.IsRescanning = ComicData.IsRescanning;
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

        XmlDatabase.Settings.DefaultArchiveCodePage = Shared.DefaultArchiveCodePage;
        XmlDatabase.Settings.TransitionAnimation = Shared.TransitionAnimation;
        XmlDatabase.Settings.SaveHistory = Shared.HistorySaveBrowsingHistory;
        XmlDatabase.Settings.DebugMode = Shared.AdvancedDebugMode;

        XmlDatabaseManager.ReleaseLock();
        Utils.TaskQueue.DefaultQueue.Enqueue(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
    }

    private void OnSettingsChanged()
    {
        if (m_updating)
        {
            return;
        }

        Utils.C0.Run(async delegate
        {
            await Save();
        });
    }

    // events
    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            var dialog = new ChooseLocationsDialog();
            await C0.ShowDialogAsync(dialog, XamlRoot);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            await HistoryDataManager.Clear(true);
            Shared.IsClearHistoryEnabled = false;
        });
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        Utils.C0.Run(async delegate
        {
            var uri = new Uri(@"https://github.com/aicd0/ComicReader/issues/new/choose");
            bool success = await Windows.System.Launcher.LaunchUriAsync(uri);

            if (!success)
            {
                // ...
            }
        });
    }

    private void OnRescanFilesClicked(object sender, RoutedEventArgs e)
    {
        Shared.IsRescanning = true;
        Utils.TaskQueue.DefaultQueue.Enqueue(ComicData.UpdateSealed(lazy_load: false));
    }
}
