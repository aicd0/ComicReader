// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Imaging;
using ComicReader.Common.PageBase;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Data.SqlHelpers;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.Navigation;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using LiteDB;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Search;

internal class SearchPageShared : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsLoading;

    public bool IsResultEmpty => SearchResults.Count == 0;

    public void UpdateUI()
    {
        IsLoadingRingVisible = IsLoading;
        IsResultGridVisible = !IsLoading && !IsResultEmpty;
        IsNoResultTextVisible = !IsLoading && IsResultEmpty;
    }

    // UI elements
    public ObservableCollection<ComicItemViewModel> SearchResults;

    private bool m_IsLoadingRingVisible;
    public bool IsLoadingRingVisible
    {
        get => m_IsLoadingRingVisible;
        set
        {
            m_IsLoadingRingVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLoadingRingVisible"));
        }
    }

    private string m_Title;
    public string Title
    {
        get => m_Title;
        set
        {
            m_Title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
        }
    }

    private string m_FilterDetails;
    public string FilterDetails
    {
        get => m_FilterDetails;
        set
        {
            m_FilterDetails = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterDetails"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterDetailsVisible"));
        }
    }
    public bool FilterDetailsVisible => FilterDetails.Length > 0;

    private bool m_IsResultGridVisible;
    public bool IsResultGridVisible
    {
        get => m_IsResultGridVisible;
        set
        {
            m_IsResultGridVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsResultGridVisible"));
        }
    }

    private string m_NoResultText;
    public string NoResultText
    {
        get => m_NoResultText;
        set
        {
            m_NoResultText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NoResultText"));
        }
    }

    private bool m_IsNoResultTextVisible;
    public bool IsNoResultTextVisible
    {
        get => m_IsNoResultTextVisible;
        set
        {
            m_IsNoResultTextVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsNoResultTextVisible"));
        }
    }

    private bool m_IsSelectMode = false;
    public bool IsSelectMode
    {
        get => m_IsSelectMode;
        set
        {
            m_IsSelectMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsSelectMode)}"));
        }
    }

    private ListViewSelectionMode m_ComicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => m_ComicItemSelectionMode;
        set
        {
            m_ComicItemSelectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(ComicItemSelectionMode)}"));
        }
    }

    public bool CommandBarSelectAllToggleOmitOnce = false;
    private bool m_IsCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => m_IsCommandBarSelectAllToggled;
        set
        {
            if (m_IsCommandBarSelectAllToggled != value)
            {
                if (!CommandBarSelectAllToggleOmitOnce)
                {
                    OnCommandBarSelectAllToggleChanged(value);
                }

                m_IsCommandBarSelectAllToggled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarSelectAllToggled"));
            }

            CommandBarSelectAllToggleOmitOnce = false;
        }
    }

    private bool m_IsCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => m_IsCommandBarFavoriteEnabled;
        set
        {
            m_IsCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarFavoriteEnabled"));
        }
    }

    private bool m_IsCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => m_IsCommandBarUnFavoriteEnabled;
        set
        {
            m_IsCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarUnFavoriteEnabled"));
        }
    }

    private bool m_IsCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => m_IsCommandBarHideEnabled;
        set
        {
            m_IsCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarHideEnabled"));
        }
    }

    private bool m_IsCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => m_IsCommandBarUnHideEnabled;
        set
        {
            m_IsCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarUnHideEnabled"));
        }
    }

    private bool _isCommandBarMarkAsReadEnabled = false;
    public bool IsCommandBarMarkAsReadEnabled
    {
        get => _isCommandBarMarkAsReadEnabled;
        set
        {
            _isCommandBarMarkAsReadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarMarkAsReadEnabled"));
        }
    }

    private bool _isCommandBarMarkAsUnreadEnabled = false;
    public bool IsCommandBarMarkAsUnreadEnabled
    {
        get => _isCommandBarMarkAsUnreadEnabled;
        set
        {
            _isCommandBarMarkAsUnreadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarMarkAsUnreadEnabled"));
        }
    }

    public Action<bool> OnCommandBarSelectAllToggleChanged;
}

internal sealed partial class SearchPage : BasePage
{
    private SearchPageShared Shared { get; set; }

    private readonly ComicItemViewModel.IItemHandler _comicItemHandler;
    private List<Match> m_matches = new();
    private int m_match_index = 0;
    private readonly CancellationLock m_search_lock = new();
    private readonly CancellationSession _loadImageSession = new();
    private string _keyword = "";

    public SearchPage()
    {
        Shared = new SearchPageShared
        {
            Title = "",
            FilterDetails = "",
            SearchResults = new ObservableCollection<ComicItemViewModel>()
        };
        _comicItemHandler = new ComicItemHandler(this);

        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");
        Shared.OnCommandBarSelectAllToggleChanged = OnCommandBarSelectAllToggleChanged;
        Shared.IsSelectMode = false;
        Shared.ComicItemSelectionMode = ListViewSelectionMode.None;

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

    protected override void OnPause()
    {
        base.OnPause();
        _loadImageSession.Next();
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
        await m_search_lock.LockAsync(async delegate (CancellationLock.Token token)
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
            Shared.IsLoading = true;
            Shared.UpdateUI();
            await SearchMain(keywords, filter);
            m_match_index = 0;
            Shared.IsLoading = false;

            // update UI
            Shared.Title = title_text;
            Shared.FilterDetails = filter_details;

            string no_results = StringResourceProvider.GetResourceString("NoResults");
            no_results = no_results.Replace("$keyword", keyword);

            Shared.NoResultText = no_results;
            Shared.SearchResults.Clear();
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
            SelectCommand<ComicTable>.IToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            SelectCommand<ComicTable>.IToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            SelectCommand<ComicTable>.IToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
            using SelectCommand<ComicTable>.IReader reader = command.Execute();

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
        m_matches = C3<Match, long, long>.Intersect(keyword_matched, filter_matched,
            (Match x) => x.Id, (long x) => x,
            new C1<long>.DefaultEqualityComparer()).ToList();

        // Sort by similarity.
        m_matches = m_matches
            .OrderBy(delegate (Match m) { return StringUtils.SmartFileNameKeySelector(m.SortTitle); }, StringUtils.SmartFileNameComparer)
            .OrderByDescending(x => x.Similarity)
            .ToList();
    }

    private async Task BindComicData(ComicItemViewModel model, ComicData comic)
    {
        model.Comic = comic;
        model.Title = comic.Title;
        model.Detail = "#" + comic.Id;
        model.Rating = comic.Rating;
        model.UpdateProgress(false);
        model.IsFavorite = await FavoriteDataManager.FromId(comic.Id) != null;
        model.IsSelectMode = Shared.IsSelectMode;
        model.ItemHandler = _comicItemHandler;
    }

    private async Task LoadMoreResults(int count)
    {
        await m_search_lock.LockAsync(async delegate (CancellationLock.Token token)
        {
            for (int i = 0; i < count; ++m_match_index)
            {
                if (token.CancellationRequested)
                {
                    return;
                }

                if (m_match_index >= m_matches.Count)
                {
                    break;
                }

                Match match = m_matches[m_match_index];
                ComicData comic = await ComicData.FromId(match.Id, "SearchLoadComic");

                if (comic == null)
                {
                    DebugUtils.Assert(false);
                    continue;
                }

                ComicItemViewModel item = new();
                await BindComicData(item, comic);
                Shared.SearchResults.Add(item);
                ++i;
            }

            Shared.UpdateUI();
        });
    }

    private void LoadImage(ComicItemHorizontal viewHolder, ComicItemViewModel item)
    {
        double image_width = (double)Application.Current.Resources["ComicItemHorizontalImageWidth"];
        double image_height = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>();

        if (item.Image.ImageSet)
        {
            return;
        }

        item.Image.ImageSet = true;
        tokens.Add(new SimpleImageLoader.Token
        {
            Width = image_width,
            Height = image_height,
            Multiplication = 1.4,
            StretchMode = StretchModeEnum.UniformToFill,
            Source = new ComicCoverImageSource(item.Comic),
            ImageResultHandler = new LoadImageCallback(viewHolder, item)
        });

        new SimpleImageLoader.Transaction(_loadImageSession.Token, tokens).Commit();
    }

    private class LoadImageCallback : IImageResultHandler
    {
        private readonly ComicItemHorizontal _viewHolder;
        private readonly ComicItemViewModel _viewModel;

        public LoadImageCallback(ComicItemHorizontal viewHolder, ComicItemViewModel viewModel)
        {
            _viewHolder = viewHolder;
            _viewModel = viewModel;
        }

        public void OnSuccess(BitmapImage image)
        {
            _viewModel.Image.Image = image;
            _viewHolder.CompareAndBind(_viewModel);
        }
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        if (Shared.IsSelectMode)
        {
            return;
        }

        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            ComicData comic = await ComicData.FromId(item.Comic.Id, "SearchOpenLoadComic");
            Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
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
            item.Image.ImageSet = false;
        }

        viewHolder.Bind(item);
        if (!args.InRecycleQueue)
        {
            LoadImage(viewHolder, item);
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
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        MainPage.Current.OpenInNewTab(route);
    }

    private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            result.IsFavorite = true;
            await FavoriteDataManager.Add(result.Comic.Id, result.Title, true);
        });
    }

    private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            result.IsFavorite = false;
            await FavoriteDataManager.RemoveWithId(result.Comic.Id, true);
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
        if (val == Shared.IsSelectMode)
        {
            return;
        }

        Shared.IsSelectMode = val;
        Shared.ComicItemSelectionMode = val ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;

        foreach (ComicItemViewModel model in Shared.SearchResults)
        {
            model.IsSelectMode = val;
        }
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

        bool all_selected = selected_items.Count == Shared.SearchResults.Count;
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

        Shared.CommandBarSelectAllToggleOmitOnce = true;
        Shared.IsCommandBarSelectAllToggled = all_selected;
        Shared.IsCommandBarFavoriteEnabled = favorite_enabled;
        Shared.IsCommandBarUnFavoriteEnabled = unfavorite_enabled;
        Shared.IsCommandBarHideEnabled = hide_enabled;
        Shared.IsCommandBarUnHideEnabled = unhide_enabled;
        Shared.IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
        Shared.IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
    }

    private void OnCommandBarSelectAllToggleChanged(bool toggled)
    {
        if (toggled)
        {
            SearchResultGridView.SelectAll();
        }
        else
        {
            SearchResultGridView.DeselectRange(new ItemIndexRange(0, (uint)Shared.SearchResults.Count));
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
                await FavoriteDataManager.Add(model.Comic.Id, model.Title, i == selected_items.Count - 1);
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
                await FavoriteDataManager.RemoveWithId(model.Comic.Id, i == selected_items.Count - 1);
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
