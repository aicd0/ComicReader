// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.PageBase;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.History;

internal sealed partial class HistoryPage : BasePage
{
    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();

        C0.Run(async delegate
        {
            await Update();
        });
    }

    private void ObserveData()
    {
        EventBus.Default.With(EventId.SidePaneUpdate).Observe(this, delegate
        {
            C0.Run(async delegate
            {
                await Update();
            });
        });
    }

    // utilities
    private async Task Update()
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

            current_group ??= new HistoryGroupViewModel(key);

            var item_out = new HistoryItemViewModel
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
        TbNoHistory.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task OpenItem(HistoryItemViewModel item, bool newTab)
    {
        ComicData comic = await ComicData.FromId(item.Id, "HistoryLoadComic");

        if (comic == null)
        {
            await DeleteItem(item);
        }
        else
        {
            Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            if (newTab)
            {
                MainPage.Current.OpenInNewTab(route);
            }
            else
            {
                GetMainPageAbility().OpenInCurrentTab(route);
            }

            GetNavigationPageAbility().SetIsSidePaneOpen(false);
        }
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    private async Task DeleteItem(HistoryItemViewModel item)
    {
        await HistoryDataManager.Remove(item.Id, false);
        var source = (ObservableCollection<HistoryGroupViewModel>)HistorySource.Source;

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

        TbNoHistory.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // events
    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await OpenItem(item, true);
        });
    }

    private void OnDeleteItemClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await DeleteItem(item);
        });
    }

    private void MainListViewItemClick(object sender, ItemClickEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (HistoryItemViewModel)e.ClickedItem;
            await OpenItem(item, false);
        });
    }
}
