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
        private bool m_PageInitialized;
        private bool m_SaveEnabled;
        private TabId m_Tab;

        public SettingsPageShared Shared { get; set; }

        public SettingsPage()
        {
            Current = this;
            m_SaveEnabled = false;
            Shared = new SettingsPageShared();
            Shared.E_SettingsChanged += E_SettingsChanged;
            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!m_PageInitialized)
            {
                m_PageInitialized = true;
                NavigationParams p = (NavigationParams)e.Parameter;
                m_Tab = p.TabId;
                m_Tab.OnTabSelected += C_PageEntered;
                Shared.RootPageShared = (RootPageShared)p.Shared;
            }

            C_UpdateTab();
            C_PageEntered();
            Shared.RootPageShared.CurrentPageType = PageType.Settings;
        }

        public static string C_PageUniqueString(object args)
        {
            return "settings";
        }

        private void C_PageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await C_UpdateContent();
            });
        }

        private void C_UpdateTab()
        {
            m_Tab.Tab.Header = "Settings";
            m_Tab.Tab.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Setting };
            m_Tab.UniqueString = C_PageUniqueString(null);
            m_Tab.Type = PageType.Settings;
        }

        // user-defined functions
        // update
        private async Task C_UpdateContent()
        {
            m_SaveEnabled = false;
            await DataManager.WaitLock();

            Shared.P_Reader_LeftToRight = Database.AppSettings.LeftToRight;
            Shared.P_Privacy_SaveBrowsingHistory = Database.AppSettings.SaveHistory;
            StatisticsTextBlock.Text = "Total collections: " + Database.Comics.Count.ToString("#,#0", CultureInfo.InvariantCulture);

            DataManager.ReleaseLock();
            m_SaveEnabled = true;
        }

        // save
        private async Task C_SaveSettings()
        {
            if (!m_SaveEnabled)
            {
                return;
            }

            await DataManager.WaitLock();

            Database.AppSettings.LeftToRight = Shared.P_Reader_LeftToRight;
            Database.AppSettings.SaveHistory = Shared.P_Privacy_SaveBrowsingHistory;

            DataManager.ReleaseLock();
            Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Settings));
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
