using System;
using System.Collections.Generic;
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
    public sealed partial class SettingsPage : Page
    {
        public static SettingsPage Current;
        TabId m_tab_id;

        private bool m_auto_save;

        public SettingsPage()
        {
            Current = this;
            m_auto_save = false;
            InitializeComponent();
        }

        // tab related
        public static string GetPageUniqueString(object args)
        {
            return "settings";
        }

        private void OnTabSelected()
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
            m_tab_id.OnTabSelected = OnTabSelected;
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (m_tab_id == null)
                {
                    m_tab_id = (TabId)e.Parameter;
                }

                UpdateTabId();
                await UpdateSettings();
            });
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
