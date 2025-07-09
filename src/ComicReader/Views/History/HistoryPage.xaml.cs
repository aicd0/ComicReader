// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Lifecycle;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.AppEnvironment;
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
        Update();
    }

    private void ObserveData()
    {
        EventBus.Default.With(EventId.SidePaneUpdate).Observe(this, delegate
        {
            Update();
        });
    }

    // utilities
    private void Update()
    {
        var source = new ObservableCollection<HistoryGroupViewModel>();
        HistoryGroupViewModel? currentGroup = null;
        List<HistoryModel.ExternalItemModel> historyItems = HistoryModel.Instance.GetModel().Items;
        historyItems.Sort((x, y) => y.DateTime.CompareTo(x.DateTime));
        foreach (HistoryModel.ExternalItemModel item in historyItems)
        {
            DateTimeOffset localTime = item.DateTime.ToLocalTime();
            string key = localTime.ToString("D", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo());
            if (currentGroup != null && !currentGroup.Key.Equals(key))
            {
                source.Add(currentGroup);
                currentGroup = null;
            }
            currentGroup ??= new HistoryGroupViewModel(key);
            var itemOut = new HistoryItemViewModel
            {
                Id = item.Id,
                Time = localTime.ToString("t", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo()),
                Title = item.Title
            };
            currentGroup.Add(itemOut);
        }
        if (currentGroup != null)
        {
            source.Add(currentGroup);
        }

        HistorySource.Source = source;
        MainListView.SelectedIndex = -1;
        TbNoHistory.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task OpenItem(HistoryItemViewModel item, bool newTab)
    {
        ComicModel? comic = await ComicModel.FromId(item.Id, "HistoryLoadComic");

        if (comic == null)
        {
            DeleteItem(item);
        }
        else
        {
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            if (newTab)
            {
                GetMainPageAbility().OpenInNewTab(route);
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
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    private void DeleteItem(HistoryItemViewModel item)
    {
        HistoryModel.Instance.Remove(item.Id, false);
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
        var item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
        DeleteItem(item);
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
