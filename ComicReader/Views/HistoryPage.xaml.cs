using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public class HistoryPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ContentPageShared m_ContentPageShared;
        public ContentPageShared ContentPageShared
        {
            get => m_ContentPageShared;
            set
            {
                m_ContentPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentPageShared"));
            }
        }
    }

    public sealed partial class HistoryPage : Page
    {
        public static HistoryPage Current = null;
        private bool m_page_initialized = false;

        public HistoryPageShared Shared { get; set; }

        public HistoryPage()
        {
            Current = this;
            Shared = new HistoryPageShared();
            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                base.OnNavigatedTo(e);

                if (!m_page_initialized)
                {
                    m_page_initialized = true;
                    Shared.ContentPageShared = ((ContentPage)e.Parameter).Shared;
                }

                await UpdateHistory();
            });
        }

        // update
        public async Task UpdateHistory()
        {
            var source = new ObservableCollection<HistoryItemGroupModel>();
            HistoryItemGroupModel current_group = null;
            await DataManager.WaitLock();

            foreach (var item in Database.History.Items)
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

                var item_out = new HistoryItemModel();
                item_out.Id = item.Id;
                item_out.Time = item.DateTime.ToString("g");
                item_out.Title = item.Title;
                current_group.Add(item_out);
            }

            DataManager.ReleaseLock();

            if (current_group != null)
            {
                source.Add(current_group);
            }

            HistorySource.Source = source;
            MainListView.SelectedIndex = -1;
        }

        private async Task OpenItem(HistoryItemModel item)
        {
            ComicItemData comic = await DataManager.GetComicWithId(item.Id);

            if (comic == null)
            {
                await DeleteItem(item);
            }
            else
            {
                RootPage.Current.LoadTab(null, PageType.Reader, comic);
            }
        }

        private void OpenInNewTabClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)((MenuFlyoutItem)sender).DataContext;
                await OpenItem(item);
            });
        }

        private async Task DeleteItem(HistoryItemModel item)
        {
            await DataManager.RemoveFromHistory(item.Id, true);
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

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void MainListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItemModel item = (HistoryItemModel)e.ClickedItem;
                await OpenItem(item);
            });
        }
    }
}
