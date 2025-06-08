// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.PageBase;
using ComicReader.Data;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.UserControls;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using LiteDB;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Search;

internal sealed partial class SearchPage : BasePage
{
    private SearchPageViewModel ViewModel { get; set; } = new SearchPageViewModel();

    private readonly ComicItemViewModel.IItemHandler _comicItemHandler;
    private List<Match> _matches = new();
    private int _matchIndex = 0;
    private readonly CancellationLock _searchLock = new();
    private string _keyword = "";

    public SearchPage()
    {
        InitializeComponent();

        _comicItemHandler = new ComicItemHandler(this);
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");
        ViewModel.OnCommandBarSelectAllToggleChanged = OnCommandBarSelectAllToggleChanged;
        ViewModel.IsSelectMode = false;
        ViewModel.ComicItemSelectionMode = ListViewSelectionMode.None;

        C0.Run(async delegate
        {
            await StartSearch();
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        GetNavigationPageAbility().SetSearchBox(_keyword);
        ScrollViewer scrollViewer = SearchResultGridView.ChildrenBreadthFirst().OfType<ScrollViewer>().First();
        scrollViewer.ViewChanged += OnScrollViewerViewChanged;
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    // Unsorted
    private async Task StartSearch()
    {
        await _searchLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            SetSelectMode(false);
            string keyword = _keyword;

            // Extract filters and keywords from string.
            var filter = Filter.Parse(keyword, out List<string> remaining);
            var keywords = new List<string>();

            foreach (string text in remaining)
            {
                keywords = keywords.Concat(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
            }

            if (!filter.ContainsFilter("hidden"))
            {
                _ = filter.AddFilter("~hidden");
            }

            string title_text;
            string tab_title;
            string filter_brief = filter.DescriptionBrief();
            string filter_details = filter.DescriptionDetailed();

            if (keywords.Count != 0)
            {
                string keyword_combined = StringUtils.Join(" ", keywords);
                title_text = "\"" + keyword_combined + "\"";
                tab_title = StringResourceProvider.GetResourceString("SearchResultsOf");
                tab_title = tab_title.Replace("$keyword", keyword_combined);
            }
            else if (filter_brief.Length != 0)
            {
                title_text = filter_brief;
                tab_title = filter_brief;
                filter_details = "";
            }
            else
            {
                title_text = StringResourceProvider.GetResourceString("AllMatchedResults");
                tab_title = StringResourceProvider.GetResourceString("SearchResults");
            }

            // update tab header
            GetMainPageAbility().SetTitle(tab_title);
            GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Find });

            // start searching
            ViewModel.IsLoading = true;
            ViewModel.UpdateUI();
            await SearchMain(keywords, filter);
            _matchIndex = 0;
            ViewModel.IsLoading = false;

            // update UI
            ViewModel.Title = title_text;
            ViewModel.FilterDetails = filter_details;

            string no_results = StringResourceProvider.GetResourceString("NoResults");
            no_results = no_results.Replace("$keyword", keyword);

            ViewModel.NoResultText = no_results;
            ViewModel.SearchResults.Clear();
        });

        await LoadMoreResults(100);
    }

    private class Match
    {
        public long Id;
        public int Similarity = 0;
        public string SortTitle = "";
    }

    private async Task SearchMain(List<string> keywords, Filter filter)
    {
        for (int i = 0; i < keywords.Count; ++i)
        {
            keywords[i] = keywords[i].ToLower();
        }

        var keyword_matched = new List<Match>();
        List<long> filter_matched = null;

        await ComicData.EnqueueCommand(delegate
        {
            var command = new SelectCommand<ComicTable>(ComicTable.Instance);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            IReaderToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            IReaderToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
            using SelectCommand<ComicTable>.IReader reader = command.Execute(SqlDatabaseManager.MainDatabase);

            while (reader.Read())
            {
                // Calculate similarity.
                int similarity = 0;
                string title1 = title1Token.GetValue();
                string title2 = title2Token.GetValue();

                if (keywords.Count != 0)
                {
                    string match_text = title1 + " " + title2;
                    similarity = StringUtils.QuickMatch(keywords, match_text);

                    if (similarity < 1)
                    {
                        continue;
                    }
                }

                // Save results.
                keyword_matched.Add(new Match
                {
                    Id = idToken.GetValue(),
                    Similarity = similarity,
                    SortTitle = title1 + " " + title2
                });
            }

            var all = new List<long>(keyword_matched.Count);

            foreach (Match match in keyword_matched)
            {
                all.Add(match.Id);
            }

            filter_matched = filter.Match(all);
        }, "SearchComics");

        // Intersect two.
        _matches = C3<Match, long, long>.Intersect(keyword_matched, filter_matched,
            (Match x) => x.Id, (long x) => x,
            new C1<long>.DefaultEqualityComparer()).ToList();

        // Sort by similarity.
        _matches = _matches
            .OrderBy(delegate (Match m) { return StringUtils.SmartFileNameKeySelector(m.SortTitle); }, StringUtils.SmartFileNameComparer)
            .OrderByDescending(x => x.Similarity)
            .ToList();
    }

    private async Task BindComicData(ComicItemViewModel model, ComicModel comic)
    {
        model.Comic = comic;
        model.Title = comic.Title;
        model.Detail = "#" + comic.Id;
        model.Rating = comic.Rating;
        model.UpdateProgress(false);
        model.IsFavorite = await FavoriteModel.Instance.FromId(comic.Id) != null;
        model.ItemHandler = _comicItemHandler;
    }

    private async Task LoadMoreResults(int count)
    {
        await _searchLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            for (int i = 0; i < count; ++_matchIndex)
            {
                if (token.CancellationRequested)
                {
                    return;
                }

                if (_matchIndex >= _matches.Count)
                {
                    break;
                }

                Match match = _matches[_matchIndex];
                ComicModel comic = await ComicModel.FromId(match.Id, "SearchLoadComic");

                if (comic == null)
                {
                    Logger.AssertNotReachHere("A86F0459678D1B60");
                    continue;
                }

                ComicItemViewModel item = new();
                await BindComicData(item, comic);
                ViewModel.SearchResults.Add(item);
                ++i;
            }

            ViewModel.UpdateUI();
        });
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        if (ViewModel.IsSelectMode)
        {
            return;
        }

        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            ComicModel comic = await ComicModel.FromId(item.Comic.Id, "SearchOpenLoadComic");
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            GetMainPageAbility().OpenInCurrentTab(route);
        });
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ComicItemViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ComicItemHorizontal;

        if (args.InRecycleQueue)
        {
            viewHolder.Unbind();
        }
        else
        {
            viewHolder.Bind(item);
        }
    }

    private void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        C0.Run(async delegate
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < scrollViewer.ActualHeight * 0.5)
            {
                await LoadMoreResults(30);
            }
        });
    }

    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            result.IsFavorite = true;
            await FavoriteModel.Instance.Add(result.Comic.Id, result.Title, true);
        });
    }

    private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            result.IsFavorite = false;
            await FavoriteModel.Instance.RemoveWithId(result.Comic.Id, true);
        });
    }

    private void OnUnhideComicClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await ctx.Comic.SaveHiddenAsync(false);
            await StartSearch();
        });
    }

    private void OnHideComicClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await ctx.Comic.SaveHiddenAsync(true);
            await StartSearch();
        });
    }

    private void MarkAsReadOrUnread(ComicItemViewModel item, bool read)
    {
        C0.Run(async delegate
        {
            if (read)
            {
                item.Comic.SetAsRead();
            }
            else
            {
                item.Comic.SetAsUnread();
            }
            await BindComicData(item, item.Comic);
        });
    }

    private void SetSelectMode(bool val)
    {
        if (val == ViewModel.IsSelectMode)
        {
            return;
        }

        ViewModel.IsSelectMode = val;
        ViewModel.ComicItemSelectionMode = val ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }

    private void OnSelectClicked(object sender, RoutedEventArgs e)
    {
        SetSelectMode(true);
    }

    private void OnScrollViewerTapped(object sender, TappedRoutedEventArgs e)
    {
        SetSelectMode(false);
    }

    private void OnGridViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCommandBarButtonStates();
    }

    private void UpdateCommandBarButtonStates()
    {
        IList<object> selected_items = SearchResultGridView.SelectedItems;

        bool all_selected = selected_items.Count == ViewModel.SearchResults.Count;
        bool favorite_enabled = false;
        bool unfavorite_enabled = false;
        bool hide_enabled = false;
        bool unhide_enabled = false;
        bool markAsReadEnabled = false;
        bool markAsUnreadEnabled = false;

        foreach (object item in selected_items)
        {
            // Never modify any element in the loop as selected_items is only a
            // reference to SearchResultGridView.SelectedItems property.
            var model = item as ComicItemViewModel;

            if (model.IsFavorite)
            {
                unfavorite_enabled = true;
            }
            else
            {
                favorite_enabled = true;
            }

            if (model.IsHide)
            {
                unhide_enabled = true;
            }
            else
            {
                hide_enabled = true;
            }

            if (!model.IsRead)
            {
                markAsReadEnabled = true;
            }

            if (!model.IsUnread)
            {
                markAsUnreadEnabled = true;
            }
        }

        ViewModel.CommandBarSelectAllToggleOmitOnce = true;
        ViewModel.IsCommandBarSelectAllToggled = all_selected;
        ViewModel.IsCommandBarFavoriteEnabled = favorite_enabled;
        ViewModel.IsCommandBarUnFavoriteEnabled = unfavorite_enabled;
        ViewModel.IsCommandBarHideEnabled = hide_enabled;
        ViewModel.IsCommandBarUnHideEnabled = unhide_enabled;
        ViewModel.IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
        ViewModel.IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
    }

    private void OnCommandBarSelectAllToggleChanged(bool toggled)
    {
        if (toggled)
        {
            SearchResultGridView.SelectAll();
        }
        else
        {
            SearchResultGridView.DeselectRange(new ItemIndexRange(0, (uint)ViewModel.SearchResults.Count));
        }
    }

    private void CommandBarFavoriteClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var selected_items = new List<object>(SearchResultGridView.SelectedItems);
            for (int i = 0; i < selected_items.Count; ++i)
            {
                var model = selected_items[i] as ComicItemViewModel;
                if (model.IsFavorite)
                {
                    continue;
                }
                model.IsFavorite = true;
                await FavoriteModel.Instance.Add(model.Comic.Id, model.Title, i == selected_items.Count - 1);
            }

            UpdateCommandBarButtonStates();
        });
    }

    private void CommandBarUnFavoriteClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var selected_items = new List<object>(SearchResultGridView.SelectedItems);
            for (int i = 0; i < selected_items.Count; ++i)
            {
                var model = selected_items[i] as ComicItemViewModel;
                if (!model.IsFavorite)
                {
                    continue;
                }
                model.IsFavorite = false;
                await FavoriteModel.Instance.RemoveWithId(model.Comic.Id, i == selected_items.Count - 1);
            }

            UpdateCommandBarButtonStates();
        });
    }

    private void CommandBarHideClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var selected_items = new List<object>(SearchResultGridView.SelectedItems);

            for (int i = 0; i < selected_items.Count; ++i)
            {
                var model = selected_items[i] as ComicItemViewModel;

                if (model.IsHide)
                {
                    continue;
                }

                await model.Comic.SaveHiddenAsync(true);
            }

            await StartSearch();
        });
    }

    private void CommandBarUnhideClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var selected_items = new List<object>(SearchResultGridView.SelectedItems);

            for (int i = 0; i < selected_items.Count; ++i)
            {
                var model = selected_items[i] as ComicItemViewModel;

                if (!model.IsHide)
                {
                    continue;
                }

                await model.Comic.SaveHiddenAsync(false);
            }

            await StartSearch();
        });
    }

    private void CommandBarMarkAsReadClicked(object sender, RoutedEventArgs e)
    {
        var selected_items = new List<object>(SearchResultGridView.SelectedItems);
        for (int i = 0; i < selected_items.Count; ++i)
        {
            var item = selected_items[i] as ComicItemViewModel;
            if (item.IsRead)
            {
                continue;
            }
            MarkAsReadOrUnread(item, true);
        }

        UpdateCommandBarButtonStates();
    }

    private void CommandBarMarkAsUnreadClicked(object sender, RoutedEventArgs e)
    {
        var selected_items = new List<object>(SearchResultGridView.SelectedItems);
        for (int i = 0; i < selected_items.Count; ++i)
        {
            var item = selected_items[i] as ComicItemViewModel;
            if (item.IsUnread)
            {
                continue;
            }
            MarkAsReadOrUnread(item, false);
        }

        UpdateCommandBarButtonStates();
    }

    private class ComicItemHandler : ComicItemViewModel.IItemHandler
    {
        private readonly SearchPage _page;

        public ComicItemHandler(SearchPage page)
        {
            _page = page;
        }

        public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            _page.OnAddToFavoritesClicked(sender, e);
        }

        public void OnHideClicked(object sender, RoutedEventArgs e)
        {
            _page.OnHideComicClicked(sender, e);
        }

        public void OnItemTapped(object sender, TappedRoutedEventArgs e)
        {
            _page.OnComicItemTapped(sender, e);
        }

        public void OnMarkAsReadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            _page.MarkAsReadOrUnread(item, true);
        }

        public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            _page.MarkAsReadOrUnread(item, false);
        }

        public void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            _page.OnOpenInNewTabClicked(sender, e);
        }

        public void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            _page.OnRemoveFromFavoritesClicked(sender, e);
        }

        public void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            _page.OnSelectClicked(sender, e);
        }

        public void OnUnhideClicked(object sender, RoutedEventArgs e)
        {
            _page.OnUnhideComicClicked(sender, e);
        }
    }
}
