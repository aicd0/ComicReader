using Microsoft.Data.Sqlite;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Database;

namespace ComicReader.Views
{
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

        private MainPageShared m_MainPageShared;
        public MainPageShared MainPageShared
        {
            get => m_MainPageShared;
            set
            {
                m_MainPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MainPageShared"));
            }
        }

        public Action OnSettingsChanged;

        private bool m_ReaderLeftToRight;
        public bool ReaderLeftToRight
        {
            get => m_ReaderLeftToRight;
            set
            {
                m_ReaderLeftToRight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReaderLeftToRight"));
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

        private bool m_HistorySaveBrowsingHistory;
        public bool HistorySaveBrowsingHistory
        {
            get => m_HistorySaveBrowsingHistory;
            set
            {
                m_HistorySaveBrowsingHistory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HistorySaveBrowsingHistory"));
                OnSettingsChanged?.Invoke();
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
    }

    public sealed partial class SettingsPage : Page
    {
        public const string AppearanceKey = "Appearance";
        public SettingsPageShared Shared { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;

        // Initialize m_updating to TRUE to avoid copying values from
        // controls (See Save()) while the page is launching.
        private bool m_updating = true;

        public SettingsPage()
        {
            Shared = new SettingsPageShared();
            Shared.OnSettingsChanged = OnSettingsChanged;

            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabUpdate = OnTabUpdate,
                OnTabStart = OnTabStart
            };

            InitializeComponent();
        }

        // Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_manager.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            m_tab_manager.OnNavigatedFrom(e);
        }

        private void OnTabRegister(object shared)
        {
            Shared.MainPageShared = (MainPageShared)shared;

            ComicDataManager.OnUpdated += OnComicDataUpdated;
        }

        private void OnTabUnregister()
        {
            ComicDataManager.OnUpdated -= OnComicDataUpdated;
        }

        private void OnTabUpdate()
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                await Update(db);
            });
        }

        private void OnTabStart(Utils.Tab.TabIdentifier tab_id)
        {
            m_tab_manager.TabId.Tab.Header = Utils.C0.TryGetResourceString("Settings");
            m_tab_manager.TabId.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Setting };
        }

        public static string PageUniqueString(object _) => "settings";

        // utilities
        private async Task Update(LockContext db)
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

            // From Xml.
            await XmlDatabaseManager.WaitLock();

            Shared.ReaderLeftToRight = XmlDatabase.Settings.LeftToRight;
            Shared.IsClearHistoryEnabled = XmlDatabase.History.Items.Count > 0;
            Shared.HistorySaveBrowsingHistory = XmlDatabase.Settings.SaveHistory;
            Shared.AdvancedDebugMode = XmlDatabase.Settings.DebugMode;

            XmlDatabaseManager.ReleaseLock();
            
            m_updating = false;

            // Rescan status.
            UpdateRescanStatus();

            // Statistics.
            await UpdateStatistis(db);

            // Feedback.
            string app_name = Utils.C0.TryGetResourceString("AppDisplayName");
            string contribution_before_link = Utils.C0.TryGetResourceString("ContributionRunBeforeLink");
            contribution_before_link = contribution_before_link.Replace("$appname", app_name);
            ContributionRunBeforeLink.Text = contribution_before_link;
            ContributionRunAfterLink.Text = Utils.C0.TryGetResourceString("ContributionRunAfterLink");

            // About.
            PackageVersion version = Package.Current.Id.Version;
            AboutBuildVersionControl.Text = app_name + " " + version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;

            string author = "aicd0";
            string about_copyright = Utils.C0.TryGetResourceString("AboutCopyright");
            about_copyright = about_copyright.Replace("$author", author);
            AboutCopyrightControl.Text = about_copyright;
        }

        private void OnComicDataUpdated(LockContext db)
        {
            // IMPORTANT: Use TaskCompletionSource to guarantee all async tasks
            // in Sync block has completed.
            TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

            Utils.C0.Sync(async delegate
            {
                UpdateRescanStatus();
                await UpdateStatistis(db);
                completion_src.SetResult(true);
            }).Wait();

            completion_src.Task.Wait();
        }

        private async Task UpdateStatistis(LockContext db)
        {
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + SqliteDatabaseManager.ComicTable;

            await ComicDataManager.WaitLock(db);
            long comic_count = (long)command.ExecuteScalar();
            ComicDataManager.ReleaseLock(db);

            string total_comic_string = Utils.C0.TryGetResourceString("TotalComics");
            StatisticsTextBlock.Text = total_comic_string +
                comic_count.ToString("#,#0", CultureInfo.InvariantCulture);
        }

        private void UpdateRescanStatus()
        {
            Shared.IsRescanning = ComicDataManager.IsRescanning;
        }

        private async Task Save()
        {
            // to local settings
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

            // to database
            await XmlDatabaseManager.WaitLock();

            XmlDatabase.Settings.LeftToRight = Shared.ReaderLeftToRight;
            XmlDatabase.Settings.SaveHistory = Shared.HistorySaveBrowsingHistory;
            XmlDatabase.Settings.DebugMode = Shared.AdvancedDebugMode;

            XmlDatabaseManager.ReleaseLock();
            Utils.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
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
                ChooseLocationsDialog dialog = new ChooseLocationsDialog();
                _ = await dialog.ShowAsync().AsTask();
            });
        }

        private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await HistoryDataManager.Clear();
                Shared.IsClearHistoryEnabled = false;
            });
        }

        private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                Uri uri = new Uri(@"https://github.com/aicd0/ComicReader/issues/new");
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
            Utils.TaskQueueManager.AppendTask(
                ComicDataManager.UpdateSealed(lazy_load: false), "", Utils.TaskQueueManager.EmptyQueue());
        }
    }
}
