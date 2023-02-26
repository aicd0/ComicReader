using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Common;
using ComicReader.Common.Router;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;

namespace ComicReader.Views
{
    public class SearchPageShared : INotifyPropertyChanged
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

        public bool IsLoading;

        public bool IsResultEmpty => SearchResults.Count == 0;

        public void UpdateUI()
        {
            IsLoadingRingVisible = IsLoading;
            IsResultGridVisible = !IsLoading && !IsResultEmpty;
            IsNoResultTextVisible = !IsLoading && IsResultEmpty;
        }

        // UI elements
        public Utils.ObservableCollectionPlus<ComicItemViewModel> SearchResults;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelectMode"));
            }
        }

        private ListViewSelectionMode m_ComicItemSelectionMode = ListViewSelectionMode.None;
        public ListViewSelectionMode ComicItemSelectionMode
        {
            get => m_ComicItemSelectionMode;
            set
            {
                m_ComicItemSelectionMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicItemSelectionMode"));
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

        public Action<bool> OnCommandBarSelectAllToggleChanged;
    }

    sealed internal partial class SearchPage : NavigatablePage
    {
        private SearchPageShared Shared { get; set; }

        private List<Match> m_matches = new List<Match>();
        private int m_match_index = 0;
        private readonly Utils.CancellationLock m_search_lock = new Utils.CancellationLock();
        private readonly Utils.CancellationLock m_load_image_lock = new Utils.CancellationLock();

        public SearchPage()
        {
            Shared = new SearchPageShared
            {
                Title = "",
                FilterDetails = "",
                SearchResults = new Utils.ObservableCollectionPlus<ComicItemViewModel>()
            };

            InitializeComponent();
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            base.OnLoaded(sender, e);
            var scrollViewer = SearchResultGridView.ChildrenBreadthFirst().OfType<ScrollViewer>().First();
            scrollViewer.ViewChanged += OnScrollViewerViewChanged;
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            Shared.NavigationPageShared = (NavigationPageShared)p.Params;
            Shared.OnCommandBarSelectAllToggleChanged = OnCommandBarSelectAllToggleChanged;
        }

        public override void OnResume()
        {
            base.OnResume();
            Shared.IsSelectMode = false;
            Shared.ComicItemSelectionMode = ListViewSelectionMode.None;
            Shared.NavigationPageShared.SetSearchBox((string)GetTabId().RequestArgs);

            Utils.C0.Run(async delegate
            {
                await StartSearch();
            });
        }

        // Unsorted
        private async Task StartSearch()
        {
            await m_search_lock.WaitAsync();
            try
            {
                SetSelectMode(false);
                string keyword = (string)GetTabId().RequestArgs;

                // Extract filters and keywords from string.
                Common.Search.Filter filter = Common.Search.Filter.Parse(keyword, out List<string> remaining);
                List<string> keywords = new List<string>();

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
                    string keyword_combined = Utils.StringUtils.Join(" ", keywords);
                    title_text = "\"" + keyword_combined + "\"";
                    tab_title = Utils.StringResourceProvider.GetResourceString("SearchResultsOf");
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
                    title_text = Utils.StringResourceProvider.GetResourceString("AllMatchedResults");
                    tab_title = Utils.StringResourceProvider.GetResourceString("SearchResults");
                }

                // update tab header
                GetTabId().Tab.Header = tab_title;
                GetTabId().Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Find };

                // start searching
                Shared.IsLoading = true;
                Shared.UpdateUI();
                await SearchMain(keywords, filter);
                m_match_index = 0;
                Shared.IsLoading = false;

                // update UI
                Shared.Title = title_text;
                Shared.FilterDetails = filter_details;

                string no_results = Utils.StringResourceProvider.GetResourceString("NoResults");
                no_results = no_results.Replace("$keyword", keyword);

                Shared.NoResultText = no_results;
                Shared.SearchResults.Clear();
            }
            finally
            {
                m_search_lock.Release();
            }

            await LoadMoreResults(100);
        }

        private class Match
        {
            public long Id;
            public int Similarity = 0;
        }

        private async Task SearchMain(List<string> keywords, Common.Search.Filter filter)
        {
            for (int i = 0; i < keywords.Count; ++i)
            {
                keywords[i] = keywords[i].ToLower();
            }

            List<Match> keyword_matched = new List<Match>();
            List<long> filter_matched = null;

            await ComicData.CommandBlock(async delegate (SqliteCommand command)
            {
                command.CommandText = "SELECT " + ComicData.Field.Id + "," +
                    ComicData.Field.Title1 + "," + ComicData.Field.Title2 + " FROM " +
                    SqliteDatabaseManager.ComicTable;

                using (SqliteDataReader query = await command.ExecuteReaderAsync())
                {
                    while (query.Read())
                    {
                        // Calculate similarity.
                        int similarity = 0;

                        if (keywords.Count != 0)
                        {
                            string title1 = query.GetString(1);
                            string title2 = query.GetString(2);

                            string match_text = title1 + " " + title2;
                            similarity = Utils.StringUtils.QuickMatch(keywords, match_text);

                            if (similarity < 1)
                            {
                                continue;
                            }
                        }

                        // Save results.
                        keyword_matched.Add(new Match
                        {
                            Id = query.GetInt64(0),
                            Similarity = similarity
                        });
                    }
                }

                List<long> all = new List<long>(keyword_matched.Count);

                foreach (Match match in keyword_matched)
                {
                    all.Add(match.Id);
                }

                filter_matched = filter.Match(all);
            });

            // Intersect two.
            m_matches = Utils.C3<Match, long, long>.Intersect(keyword_matched, filter_matched,
                (Match x) => x.Id, (long x) => x,
                new Utils.C1<long>.DefaultEqualityComparer()).ToList();

            // Sort by similarity.
            m_matches = m_matches.OrderByDescending(x => x.Similarity).ToList();
        }

        private async Task<ComicItemViewModel> ComicDataToViewModel(ComicData comic)
        {
            string progress;

            if (comic.Progress >= 0)
            {
                if (comic.Progress >= 100)
                {
                    progress = Utils.StringResourceProvider.GetResourceString("Finished");
                }
                else
                {
                    progress = Utils.StringResourceProvider.GetResourceString("FinishPercentage")
                        .Replace("$percentage", comic.Progress.ToString());
                }
            }
            else
            {
                progress = "";
            }

            return new ComicItemViewModel
            {
                Comic = comic,
                Title = comic.Title,
                Detail = "#" + comic.Id,
                Rating = comic.Rating,
                Progress = progress,
                IsFavorite = await FavoriteDataManager.FromId(comic.Id) != null,
                IsSelectMode = Shared.IsSelectMode,

                OnItemTapped = OnComicItemTapped,
                OnOpenInNewTabClicked = OnOpenInNewTabClicked,
                OnAddToFavoritesClicked = OnAddToFavoritesClicked,
                OnRemoveFromFavoritesClicked = OnRemoveFromFavoritesClicked,
                OnHideClicked = OnHideComicClicked,
                OnUnhideClicked = OnUnhideComicClicked,
                OnSelectClicked = OnSelectClicked,
            };
        }

        private async Task LoadMoreResults(int count)
        {
            await m_search_lock.WaitAsync();
            try
            {
                for (int i = 0; i < count; ++m_match_index)
                {
                    if (m_search_lock.CancellationRequested)
                    {
                        return;
                    }

                    if (m_match_index >= m_matches.Count)
                    {
                        break;
                    }

                    Match match = m_matches[m_match_index];
                    ComicData comic = await ComicData.FromId(match.Id);

                    ComicItemViewModel item = await ComicDataToViewModel(comic);
                    Shared.SearchResults.Add(item);
                    ++i;
                }

                Shared.UpdateUI();
            }
            finally
            {
                m_search_lock.Release();
            }

            // Load images.
            await LoadImages();
        }

        private async Task LoadImages()
        {
            await m_load_image_lock.WaitAsync();
            try
            {
                double image_width = (double)Application.Current.Resources["ComicItemHorizontalImageWidth"];
                double image_height = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
                List<Utils.ImageLoader.Token> image_loader_tokens = new List<Utils.ImageLoader.Token>();

                foreach (ComicItemViewModel item in Shared.SearchResults)
                {
                    if (item.IsImageLoaded)
                    {
                        continue;
                    }

                    image_loader_tokens.Add(new Utils.ImageLoader.Token
                    {
                        Comic = item.Comic,
                        Index = -1,
                        Callback = (BitmapImage img) =>
                        {
                            item.Image = img;
                            item.IsImageLoaded = true;
                        }
                    });
                }

                await new Utils.ImageLoader.Builder(image_loader_tokens, m_load_image_lock)
                    .WidthConstrain(image_width).HeightConstrain(image_height).Multiplication(1.4)
                    .StretchMode(Utils.ImageLoader.StretchModeEnum.UniformToFill)
                    .Commit();
            }
            finally
            {
                m_load_image_lock.Release();
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

            Utils.C0.Run(async delegate
            {
                ComicItemViewModel item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
                ComicData comic = await ComicData.FromId(item.Comic.Id);
                MainPage.Current.LoadTab(GetTabId(), ReaderPageTrait.Instance, comic);
            });
        }

        private void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ScrollViewer scrollViewer = (ScrollViewer)sender;
                if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < scrollViewer.ActualHeight * 0.5)
                {
                    await LoadMoreResults(30);
                }
            });
        }

        private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            MainPage.Current.LoadTab(null, ReaderPageTrait.Instance, item.Comic);
        }

        private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                result.IsFavorite = true;
                await FavoriteDataManager.Add(result.Comic.Id, result.Title, true);
            });
        }

        private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel result = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                result.IsFavorite = false;
                await FavoriteDataManager.RemoveWithId(result.Comic.Id, true);
            });
        }

        private void OnUnhideComicClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await ctx.Comic.SaveHiddenAsync(false);
                await StartSearch();
            });
        }

        private void OnHideComicClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await ctx.Comic.SaveHiddenAsync(true);
                await StartSearch();
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
            IList<object> selected_items = SearchResultGridView.SelectedItems;

            bool all_selected = selected_items.Count == Shared.SearchResults.Count;
            bool favorite_enabled = false;
            bool unfavorite_enabled = false;
            bool hide_enabled = false;
            bool unhide_enabled = false;

            foreach (object item in selected_items)
            {
                // Never modify any element in the loop as selected_items is only a
                // reference to SearchResultGridView.SelectedItems property.
                ComicItemViewModel model = item as ComicItemViewModel;

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
            }

            Shared.CommandBarSelectAllToggleOmitOnce = true;
            Shared.IsCommandBarSelectAllToggled = all_selected;
            Shared.IsCommandBarFavoriteEnabled = favorite_enabled;
            Shared.IsCommandBarUnFavoriteEnabled = unfavorite_enabled;
            Shared.IsCommandBarHideEnabled = hide_enabled;
            Shared.IsCommandBarUnHideEnabled = unhide_enabled;
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
            Utils.C0.Run(async delegate
            {
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

                    if (model.IsFavorite)
                    {
                        continue;
                    }

                    model.IsFavorite = true;
                    await FavoriteDataManager.Add(model.Comic.Id, model.Title, i == selected_items.Count - 1);
                }

                SetSelectMode(false);
            });
        }

        private void CommandBarUnFavoriteClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

                    if (!model.IsFavorite)
                    {
                        continue;
                    }

                    model.IsFavorite = false;
                    await FavoriteDataManager.RemoveWithId(model.Comic.Id, i == selected_items.Count - 1);
                }

                SetSelectMode(false);
            });
        }

        private void CommandBarHideClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

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
            Utils.C0.Run(async delegate
            {
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

                    if (!model.IsHide)
                    {
                        continue;
                    }

                    await model.Comic.SaveHiddenAsync(false);
                }

                await StartSearch();
            });
        }

        private void OnComicItemControlTapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }
    }
}
