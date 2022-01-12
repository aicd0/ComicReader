using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Views
{
    public class HistoryPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private NavigationPageShared m_NavigationPageShared;
        public NavigationPageShared NavigationPageShared
        {
            get => m_NavigationPageShared;
            set
            {
                m_NavigationPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NavigationPageShared"));
            }
        }

        private bool m_IsEmpty = false;
        public bool IsEmpty
        {
            get => m_IsEmpty;
            set
            {
                m_IsEmpty = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEmpty"));
            }
        }
    }

    public sealed partial class HistoryPage : Page
    {
        public static HistoryPage Current = null;
        public HistoryPageShared Shared { get; set; }

        private Utils.Tab.TabManager m_tab_manager;

        public HistoryPage()
        {
            Current = this;
            Shared = new HistoryPageShared();

            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabUpdate = OnTabUpdate
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
            Shared.NavigationPageShared = (NavigationPageShared)shared;
        }

        private void OnTabUnregister() { }

        private void OnTabUpdate()
        {
            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        // utilities
        public async Task Update()
        {
            var source = new ObservableCollection<HistoryGroupViewModel>();
            HistoryGroupViewModel current_group = null;
            await XmlDatabaseManager.WaitLock();

            foreach (HistoryItemData item in XmlDatabase.History.Items)
            {
                string key = item.DateTime.ToString("D");

                if (current_group != null && !current_group.Key.Equals(key))
                {
                    source.Add(current_group);
                    current_group = null;
                }

                if (current_group == null)
                {
                    current_group = new HistoryGroupViewModel(key);
                }

                HistoryItemViewModel item_out = new HistoryItemViewModel
                {
                    Id = item.Id,
                    Time = item.DateTime.ToString("t"),
                    Title = item.Title
                };

                current_group.Add(item_out);
            }

            XmlDatabaseManager.ReleaseLock();

            if (current_group != null)
            {
                source.Add(current_group);
            }

            HistorySource.Source = source;
            MainListView.SelectedIndex = -1;
            Shared.IsEmpty = source.Count == 0;
        }

        private async Task OpenItem(LockContext db, HistoryItemViewModel item, bool new_tab)
        {
            ComicData comic = await ComicDataManager.FromId(db, item.Id);

            if (comic == null)
            {
                await DeleteItem(item);
            }
            else
            {
                MainPage.Current.LoadTab(new_tab ? null : m_tab_manager.TabId, Utils.Tab.PageType.Reader, comic);
                Shared.NavigationPageShared.IsSidePaneOpen = false;
            }
        }

        private async Task DeleteItem(HistoryItemViewModel item)
        {
            await HistoryDataManager.Remove(item.Id, true);
            ObservableCollection<HistoryGroupViewModel> source = (ObservableCollection<HistoryGroupViewModel>)HistorySource.Source;

            for (int i = 0; i < source.Count; ++i)
            {
                HistoryGroupViewModel group = source[i];

                for (int j = 0; j < group.Count; ++j)
                {
                    HistoryItemViewModel item2 = group[j];

                    if (item2.Id == item.Id)
                    {
                        group.RemoveAt(j);
                        --j;
                    }
                }

                if (group.Count == 0)
                {
                    source.RemoveAt(i);
                    --i;
                }
            }

            Shared.IsEmpty = source.Count == 0;
        }

        // events
        private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                HistoryItemViewModel item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await OpenItem(db, item, true);
            });
        }

        private void OnDeleteItemClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                HistoryItemViewModel item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void MainListViewItemClick(object sender, ItemClickEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                HistoryItemViewModel item = (HistoryItemViewModel)e.ClickedItem;
                await OpenItem(db, item, false);
            });
        }
    }
}
