using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
    }

    public sealed partial class SettingsPage : Page
    {
        public static SettingsPage Current;
        public SettingsPageShared Shared { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;
        private bool m_updating = false;

        public SettingsPage()
        {
            Current = this;
            Shared = new SettingsPageShared();
            Shared.OnSettingsChanged = OnSettingsChanged;

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnUpdate = OnUpdate;

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
                await Update();
            });
        }

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            m_tab_manager.TabId.Tab.Header = "Settings";
            m_tab_manager.TabId.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Setting };
            Shared.RootPageShared.CurrentPageType = Utils.Tab.PageType.Settings;
        }

        public static string PageUniqueString(object _) => "settings";

        // utilities
        private async Task Update()
        {
            m_updating = false;
            await DatabaseManager.WaitLock();

            Shared.ReaderLeftToRight = Database.AppSettings.LeftToRight;
            Shared.HistorySaveBrowsingHistory = Database.AppSettings.SaveHistory;
            StatisticsTextBlock.Text = "Total collections: " + Database.Comics.Items.Count.ToString("#,#0", CultureInfo.InvariantCulture);

            DatabaseManager.ReleaseLock();
            m_updating = true;
        }

        private async Task Save()
        {
            if (!m_updating)
            {
                return;
            }

            await DatabaseManager.WaitLock();
            Database.AppSettings.LeftToRight = Shared.ReaderLeftToRight;
            Database.AppSettings.SaveHistory = Shared.HistorySaveBrowsingHistory;

            DatabaseManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.Settings));
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
