using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Data;

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
                if (m_Appearance == value)
                {
                    return;
                }

                m_Appearance = value;
                AppearanceLightChecked = m_Appearance == AppearanceSetting.Light;
                AppearanceDarkChecked = m_Appearance == AppearanceSetting.Dark;
                AppearanceUseSystemSettingChecked = m_Appearance == AppearanceSetting.UseSystemSetting;
                AppearanceChanged = m_Appearance != CurrentAppearance;
                OnSettingsChanged?.Invoke();
            }
        }

        private bool m_AppearanceLightChecked = false;
        public bool AppearanceLightChecked
        {
            get => m_AppearanceLightChecked;
            set
            {
                if (value == m_AppearanceLightChecked)
                {
                    return;
                }

                m_AppearanceLightChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.Light;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceLightChecked"));
            }
        }

        private bool m_AppearanceDarkChecked = false;
        public bool AppearanceDarkChecked
        {
            get => m_AppearanceDarkChecked;
            set
            {
                if (value == m_AppearanceDarkChecked)
                {
                    return;
                }

                m_AppearanceDarkChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.Dark;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceDarkChecked"));
            }
        }

        private bool m_AppearanceUseSystemSettingChecked = false;
        public bool AppearanceUseSystemSettingChecked
        {
            get => m_AppearanceUseSystemSettingChecked;
            set
            {
                if (value == m_AppearanceUseSystemSettingChecked)
                {
                    return;
                }

                m_AppearanceUseSystemSettingChecked = value;

                if (value)
                {
                    Appearance = AppearanceSetting.UseSystemSetting;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AppearanceUseSystemSettingChecked"));
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

        // Set the initial value of m_updating to TRUE to avoid copying values
        // from controls (See Save()) while the page is launching.
        private bool m_updating = true;

        public SettingsPage()
        {
            Shared = new SettingsPageShared();
            Shared.OnSettingsChanged = OnSettingsChanged;

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnRegister = OnRegister;
            m_tab_manager.OnUnregister = OnUnregister;
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnUpdate = OnUpdate;
            Unloaded += m_tab_manager.OnUnloaded;

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

        private void OnRegister(object shared)
        {
            Shared.MainPageShared = (MainPageShared)shared;
        }

        private void OnUnregister() { }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await Update();
            });
        }

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            m_tab_manager.TabId.Tab.Header = "Settings";
            m_tab_manager.TabId.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Setting };
        }

        public static string PageUniqueString(object _) => "settings";

        // utilities
        private async Task Update()
        {
            // from local settings
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

            // from database
            await DatabaseManager.WaitLock();
            m_updating = true;

            Shared.ReaderRightToLeft = Database.AppSettings.RightToLeft;
            Shared.HistorySaveBrowsingHistory = Database.AppSettings.SaveHistory;
            StatisticsTextBlock.Text = "Total collections: " + Database.Comic.Items.Count.ToString("#,#0", CultureInfo.InvariantCulture);

            m_updating = false;
            DatabaseManager.ReleaseLock();
        }

        private async Task Save()
        {
            if (m_updating)
            {
                return;
            }

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
            await DatabaseManager.WaitLock();

            Database.AppSettings.RightToLeft = Shared.ReaderRightToLeft;
            Database.AppSettings.SaveHistory = Shared.HistorySaveBrowsingHistory;

            DatabaseManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.AppSettings));
        }

        private void OnSettingsChanged()
        {
            Utils.Methods.Run(async delegate
            {
                await Save();
            });
        }

        // events
        private void ChooseLocationsClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ChooseLocationsDialog dialog = new ChooseLocationsDialog();
                _ = await dialog.ShowAsync().AsTask();
            });
        }
    }
}
