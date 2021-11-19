using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ComicReader.Data;

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

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnRegister = OnRegister;
            m_tab_manager.OnUnregister = OnUnregister;
            m_tab_manager.OnPageEntered = OnPageEntered;
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
            Shared.NavigationPageShared = (NavigationPageShared)shared;
        }

        private void OnUnregister() { }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await Update();
            });
        }

        // utilities
        public async Task Update()
        {
            var source = new ObservableCollection<HistoryItemGroupModel>();
            HistoryItemGroupModel current_group = null;
            await DatabaseManager.WaitLock();

            foreach (HistoryItemData item in Database.History.Items)
            {
                string key = item.DateTime.ToString("D");

                if (current_group != null && !current_group.Key.Equals(key))
                {
                    source.Add(current_group);
                    current_group = null;
                }

                if (current_group == null)
                {
                    current_group = new HistoryItemGroupModel(key);
                }

                HistoryItemModel item_out = new HistoryItemModel
                {
                    Id = item.Id,
                    Time = item.DateTime.ToString("g"),
                    Title = item.Title
                };

                current_group.Add(item_out);
            }

            DatabaseManager.ReleaseLock();

            if (current_group != null)
            {
                source.Add(current_group);
            }

            HistorySource.Source = source;
            MainListView.SelectedIndex = -1;
        }

        private async Task OpenItem(HistoryItemModel item, bool new_tab)
        {
            ComicItemData comic = await ComicDataManager.FromId(item.Id);

            if (comic == null)
            {
                await DeleteItem(item);
            }
            else
            {
                MainPage.Current.LoadTab(new_tab ? null : m_tab_manager.TabId, Utils.Tab.PageType.Reader, comic);
                Shared.NavigationPageShared.PaneOpen = false;
            }
        }

        private async Task DeleteItem(HistoryItemModel item)
        {
            await HistoryDataManager.Remove(item.Id, true);
            ObservableCollection<HistoryItemGroupModel> source = (ObservableCollection<HistoryItemGroupModel>)HistorySource.Source;

            for (int i = 0; i < source.Count; ++i)
            {
                HistoryItemGroupModel group = source[i];

                for (int j = 0; j < group.Count; ++j)
                {
                    HistoryItemModel item2 = group[j];

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
        }

        // events
        private void OpenInNewTabClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)((MenuFlyoutItem)sender).DataContext;
                await OpenItem(item, true);
            });
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void MainListViewItemClick(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)e.ClickedItem;
                await OpenItem(item, false);
            });
        }
    }
}
