using Microsoft.Data.Sqlite;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
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

        private bool m_ReaderRightToLeft;
        public bool ReaderRightToLeft
        {
            get => m_ReaderRightToLeft;
            set
            {
                m_ReaderRightToLeft = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReaderRightToLeft"));
                OnSettingsChanged?.Invoke();
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

        // navigation
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
        }

        private void OnTabUnregister() { }

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
            m_tab_manager.TabId.Tab.Header = "Settings";
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

            // From database.
            await XmlDatabaseManager.WaitLock();

            Shared.ReaderRightToLeft = XmlDatabase.Settings.RightToLeft;
            Shared.HistorySaveBrowsingHistory = XmlDatabase.Settings.SaveHistory;

            XmlDatabaseManager.ReleaseLock();

            // Comic count.
            SqliteCommand command = SqliteDatabaseManager.NewCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + SqliteDatabaseManager.ComicTable;

            await ComicDataManager.WaitLock(db);
            long comic_count = (long)command.ExecuteScalar();
            ComicDataManager.ReleaseLock(db);

            StatisticsTextBlock.Text = "Total collections: " +
                comic_count.ToString("#,#0", CultureInfo.InvariantCulture);
            
            m_updating = false;
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

            XmlDatabase.Settings.RightToLeft = Shared.ReaderRightToLeft;
            XmlDatabase.Settings.SaveHistory = Shared.HistorySaveBrowsingHistory;

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
    }
}
