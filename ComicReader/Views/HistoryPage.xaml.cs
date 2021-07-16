using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public sealed partial class HistoryPage : Page
    {
        public static HistoryPage Current = null;

        public HistoryPage()
        {
            Current = this;
            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Utils.Methods.Run(async delegate
            {
                await UpdateHistory();
            });
        }

        public async Task UpdateHistory()
        {
            var source = new ObservableCollection<HistoryItemGroup>();
            HistoryItemGroup current_group = null;
            await DataManager.WaitLock();

            foreach (var item in Database.History)
            {
                string key = item.DateTime.ToString("D");

                if (current_group != null && !current_group.Key.Equals(key))
                {
                    source.Add(current_group);
                    current_group = null;
                }

                if (current_group == null)
                {
                    current_group = new HistoryItemGroup(key);
                }

                var item_out = new HistoryItem();
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
        }

        private async Task OpenItem(HistoryItem item)
        {
            ComicData comic = await DataManager.GetComicWithId(item.Id);

            if (comic == null)
            {
                await DeleteItem(item);
            }
            else
            {
                await RootPage.Current.LoadTab(null, PageType.Reader, comic);
            }
        }

        private void OpenInNewTabClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                HistoryItem item = (HistoryItem)((MenuFlyoutItem)sender).DataContext;
                await OpenItem(item);
            });
        }

        private async Task DeleteItem(HistoryItem item)
        {
            await DataManager.RemoveFromHistory(item.Id, true);
            ObservableCollection<HistoryItemGroup> source = (ObservableCollection<HistoryItemGroup>)HistorySource.Source;

            for (int i = 0; i < source.Count; ++i)
            {
                HistoryItemGroup group = source[i];

                for (int j = 0; j < group.Count; ++j)
                {
                    HistoryItem item2 = group[j];

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
                HistoryItem item = (HistoryItem)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void ItemPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                Pointer ptr = e.Pointer;

                if (ptr.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
                {
                    Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(null);

                    if (ptrPt.Properties.IsLeftButtonPressed)
                    {
                        HistoryItem item = (HistoryItem)((StackPanel)sender).DataContext;
                        await OpenItem(item);
                        e.Handled = true;
                    }
                }
            });
        }
    }
}
