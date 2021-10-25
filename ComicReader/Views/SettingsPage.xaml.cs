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
using muxc = Microsoft.UI.Xaml.Controls;
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

        public Action E_SettingsChanged;

        private bool m_Reader_LeftToRight;
        public bool P_Reader_LeftToRight
        {
            get => m_Reader_LeftToRight;
            set
            {
                m_Reader_LeftToRight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("P_Reader_LeftToRight"));
                E_SettingsChanged?.Invoke();
            }
        }

        private bool m_Privacy_SaveBrowsingHistory;
        public bool P_Privacy_SaveBrowsingHistory
        {
            get => m_Privacy_SaveBrowsingHistory;
            set
            {
                m_Privacy_SaveBrowsingHistory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("P_Privacy_SaveBrowsingHistory"));
                E_SettingsChanged?.Invoke();
            }
        }
    }

    public sealed partial class SettingsPage : Page
    {
        public static SettingsPage Current;
        public SettingsPageShared Shared { get; set; }

        private TabManager m_tab_manager;
        private bool m_save_enabled;

        public SettingsPage()
        {
            Current = this;
            Shared = new SettingsPageShared();
            Shared.E_SettingsChanged += E_SettingsChanged;

            m_tab_manager = new TabManager();
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnUpdate = OnUpdate;
            m_save_enabled = false;

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

        private void OnSetShared(object shared)
        {
            Shared.RootPageShared = (RootPageShared)shared;
        }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await C_UpdateContent();
            });
        }

        private void OnUpdate(TabIdentifier tab_id)
        {
            m_tab_manager.TabId.Tab.Header = "Settings";
            m_tab_manager.TabId.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Setting };
            Shared.RootPageShared.CurrentPageType = PageType.Settings;
        }

        public static string GetPageUniqueString(object args)
        {
            return "settings";
        }

        // user-defined functions
        // update
        private async Task C_UpdateContent()
        {
            m_save_enabled = false;
            await DataManager.WaitLock();

            Shared.P_Reader_LeftToRight = Database.AppSettings.LeftToRight;
            Shared.P_Privacy_SaveBrowsingHistory = Database.AppSettings.SaveHistory;
            StatisticsTextBlock.Text = "Total collections: " + Database.Comics.Items.Count.ToString("#,#0", CultureInfo.InvariantCulture);

            DataManager.ReleaseLock();
            m_save_enabled = true;
        }

        // save
        private async Task C_SaveSettings()
        {
            if (!m_save_enabled)
            {
                return;
            }

            await DataManager.WaitLock();

            Database.AppSettings.LeftToRight = Shared.P_Reader_LeftToRight;
            Database.AppSettings.SaveHistory = Shared.P_Privacy_SaveBrowsingHistory;

            DataManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Settings));
        }

        private void E_SettingsChanged()
        {
            Utils.Methods.Run(async delegate
            {
                await C_SaveSettings();
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
