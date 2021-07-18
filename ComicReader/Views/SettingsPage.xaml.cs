using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using ComicReader.Data;

namespace ComicReader.Views
{
    public class SettingsPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private RootPageShared m_RootPageShared;
        public RootPageShared RootPageShared
        {
            get => m_RootPageShared;
            set
            {
                m_RootPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RootPageShared"));
            }
        }
    }

    public sealed partial class SettingsPage : Page
    {
        public static SettingsPage Current;
        private bool m_page_initialized;
        private TabId m_tab_id;

        private bool m_auto_save;

        public SettingsPageShared Shared { get; set; }

        public SettingsPage()
        {
            Current = this;
            m_auto_save = false;
            Shared = new SettingsPageShared();
            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!m_page_initialized)
            {
                m_page_initialized = true;
                NavigationParams p = (NavigationParams)e.Parameter;
                m_tab_id = p.TabId;
                m_tab_id.OnTabSelected += OnPageEntered;
                Shared.RootPageShared = (RootPageShared)p.Shared;
            }

            UpdateTabId();
            OnPageEntered();
            Shared.RootPageShared.CurrentPageType = PageType.Settings;
        }

        public static string GetPageUniqueString(object args)
        {
            return "settings";
        }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await UpdateSettings();
            });
        }

        private void UpdateTabId()
        {
            m_tab_id.Tab.Header = "Settings";
            m_tab_id.Tab.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Setting };
            m_tab_id.UniqueString = GetPageUniqueString(null);
            m_tab_id.Type = PageType.Settings;
        }

        // user-defined functions
        // update
        private async Task UpdateSettings()
        {
            m_auto_save = false;
            await DataManager.WaitLock();

            SaveBrowsingHistoryToggleSwitch.IsOn = Database.AppSettings.SaveHistory;
            StatisticsTextBlock.Text = "Total collections: " + Database.Comics.Count.ToString("#,#0", CultureInfo.InvariantCulture);

            DataManager.ReleaseLock();
            m_auto_save = true;
        }

        // save
        private async Task SaveSettings()
        {
            bool save_history = SaveBrowsingHistoryToggleSwitch.IsOn;
            await DataManager.WaitLock();
            Database.AppSettings.SaveHistory = save_history;
            DataManager.ReleaseLock();
            Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Settings));
        }

        // save browsing history toggle
        void OnSaveBrowsingHistoryToggleSwitchToggled(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (m_auto_save)
                {
                    await SaveSettings();
                }
            });
        }

        // choose locations button
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
