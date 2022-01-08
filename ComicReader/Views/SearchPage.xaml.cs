using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Database;
using ComicReader.DesignData;

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

    public sealed partial class SearchPage : Page
    {
        private SearchPageShared Shared { get; set; }

        private Utils.Tab.TabManager m_tab_manager;
        private List<ComicData> m_all_results = new List<ComicData>();
        private Utils.CancellationLock m_search_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_load_result_lock = new Utils.CancellationLock();

        public SearchPage()
        {
            Shared = new SearchPageShared();
            Shared.Title = "";
            Shared.FilterDetails = "";
            Shared.SearchResults = new Utils.ObservableCollectionPlus<ComicItemViewModel>();

            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabStart = OnTabStart
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
            Shared.OnCommandBarSelectAllToggleChanged = OnCommandBarSelectAllToggleChanged;
        }

        private void OnTabUnregister() { }

        private void OnTabStart(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Search;
                Shared.NavigationPageShared.SetSearchBox((string)tab_id.RequestArgs);
                Shared.IsSelectMode = false;
                Shared.ComicItemSelectionMode = ListViewSelectionMode.None;

                await StartSearch(db);
            });
        }

        public static string PageUniqueString(object args)
        {
            string keyword = (string)args;
            return "Search/" + keyword;
        }

        // Unsorted
        private async Task StartSearch(LockContext db)
        {
            SetSelectMode(false);

            string keyword = (string)m_tab_manager.TabId.RequestArgs;

            // extract filters and keywords from string
            Utils.Search.Filter filter = Utils.Search.Filter.Parse(keyword, out List<string> remaining);
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
                tab_title = "Results of '" + keyword_combined + "'";
            }
            else if (filter_brief.Length != 0)
            {
                title_text = filter_brief;
                tab_title = filter_brief;
                filter_details = "";
            }
            else
            {
                title_text = "All matched results";
                tab_title = "Search results";
            }

            // update tab header
            m_tab_manager.TabId.Tab.Header = tab_title;
            m_tab_manager.TabId.Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Find };

            // start searching
            Shared.IsLoading = true;
            Shared.UpdateUI();

            if (!await SearchMain(db, keywords, filter))
            {
                return;
            }

            Shared.IsLoading = false;

            // update UI
            Shared.Title = title_text;
            Shared.FilterDetails = filter_details;
            Shared.NoResultText = "No results for \"" + keyword + "\"";
            Shared.SearchResults.Clear();
            await LoadMoreResults(db, 40);
        }

        private class Match
        {
            public long Id;
            public int Similarity = 0;
        }

        private async Task<bool> SearchMain(LockContext db, List<string> keywords, Utils.Search.Filter filter)
        {
            await m_search_lock.WaitAsync();
            try
            {
                for (int i = 0; i < keywords.Count; ++i)
                {
                    keywords[i] = keywords[i].ToLower();
                }

                var matched = new List<Match>();

                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "SELECT " + ComicData.FieldId + "," +
                    ComicData.FieldTitle1 + "," + ComicData.FieldTitle2 + " FROM " +
                    SqliteDatabaseManager.ComicTable;

                await ComicDataManager.WaitLock(db); // Lock on.
                SqliteDataReader query = await command.ExecuteReaderAsync();

                while(query.Read())
                {
                    // Cancel the current session if the next search has begun.
                    if (m_search_lock.CancellationRequested)
                    {
                        return false;
                    }

                    string title1 = query.GetString(1);
                    string title2 = query.GetString(2);

                    // Calculate similarity.
                    int similarity = 0;

                    if (keywords.Count != 0)
                    {
                        string match_text = title1 + " " + title2;
                        similarity = Utils.StringUtils.QuickMatch(keywords, match_text);
                        if (similarity < 1) continue;
                    }

                    // Save results.
                    matched.Add(new Match
                    {
                        Id = query.GetInt32(0),
                        Similarity = similarity
                    });
                }
                ComicDataManager.ReleaseLock(db); // Lock off.

                // Sort by similarity.
                matched = matched.OrderByDescending(x => x.Similarity).ToList();

                // Save results.
                m_all_results.Clear();

                foreach (Match match in matched)
                {
                    ComicData comic = await ComicDataManager.FromId(db, match.Id);

                    if (!filter.Pass(comic))
                    {
                        continue;
                    }

                    m_all_results.Add(comic);
                }
            }
            finally
            {
                m_search_lock.Release();
            }

            return true;
        }

        private async Task LoadMoreResults(LockContext db, int count)
        {
            await m_load_result_lock.WaitAsync();
            try
            {
                int items_loaded = Shared.SearchResults.Count;

                if (items_loaded + count > m_all_results.Count)
                {
                    count = m_all_results.Count - items_loaded;
                }

                if (count == 0)
                {
                    return;
                }

                int end_i = items_loaded + count;
                List<ComicItemViewModel> results_tmp = new List<ComicItemViewModel>();

                for (int i = items_loaded; i < end_i; ++i)
                {
                    ComicData comic = m_all_results[i];

                    ComicItemViewModel result = new ComicItemViewModel
                    {
                        Comic = comic,
                        Title = comic.Title,
                        Detail = "#" + comic.Id,
                        Rating = comic.Rating,
                        Progress = comic.Progress < 0 ? "" :
                            (comic.Progress >= 100 ? "Finished" :
                            comic.Progress.ToString() + "% Completed"),
                        IsFavorite = await FavoriteDataManager.FromId(comic.Id) != null,
                        IsSelectMode = Shared.IsSelectMode,

                        OnItemPressed = OnComicItemPressed,
                        OnOpenInNewTabClicked = OnOpenInNewTabClicked,
                        OnAddToFavoritesClicked = OnAddToFavoritesClicked,
                        OnRemoveFromFavoritesClicked = OnRemoveFromFavoritesClicked,
                        OnHideClicked = OnHideComicClicked,
                        OnUnhideClicked = OnUnhideComicClicked,
                        OnSelectClicked = OnSelectClicked,
                    };

                    results_tmp.Add(result);
                }

                // update UI
                foreach (ComicItemViewModel result in results_tmp)
                {
                    Shared.SearchResults.Add(result);
                }

                Shared.UpdateUI();

                // load images
                double image_height = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
                var image_loader_tokens = new List<Utils.ImageLoaderToken>();
                
                foreach (ComicItemViewModel item in Shared.SearchResults)
                {
                    if (item.IsImageLoaded)
                    {
                        continue;
                    }

                    image_loader_tokens.Add(new Utils.ImageLoaderToken
                    {
                        Comic = item.Comic,
                        Index = 0,
                        Callback = (BitmapImage img) =>
                        {
                            item.Image = img;
                            item.IsImageLoaded = true;
                        }
                    });
                }

                await Utils.ImageLoader.Load(db, image_loader_tokens,
                    double.PositiveInfinity, image_height, m_load_result_lock);
            }
            finally
            {
                Shared.UpdateUI();
                m_load_result_lock.Release();
            }
        }

        private void OnComicItemPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                if (Shared.IsSelectMode)
                {
                    return;
                }

                PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

                if (!pt.Properties.IsLeftButtonPressed)
                {
                    return;
                }

                ComicItemViewModel item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
                ComicData comic = await ComicDataManager.FromId(db, item.Comic.Id);
                MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Reader, comic);
            });
        }

        private void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                ScrollViewer scrollViewer = (ScrollViewer)sender;

                if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < scrollViewer.ActualHeight * 0.5)
                {
                    await LoadMoreResults(db, 12);
                }
            });
        }

        private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Reader, item.Comic);
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
                LockContext db = new LockContext();
                ComicItemViewModel ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Unhide(db, ctx.Comic);
                await StartSearch(db);
            });
        }

        private void OnHideComicClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                ComicItemViewModel ctx = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Hide(db, ctx.Comic);
                await StartSearch(db);
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

        private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
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
                LockContext db = new LockContext();
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

                    if (model.IsHide)
                    {
                        continue;
                    }

                    await ComicDataManager.Hide(db, model.Comic);
                }

                await StartSearch(db);
            });
        }

        private void CommandBarUnhideClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();
                List<object> selected_items = new List<object>(SearchResultGridView.SelectedItems);

                for (int i = 0; i < selected_items.Count; ++i)
                {
                    ComicItemViewModel model = selected_items[i] as ComicItemViewModel;

                    if (!model.IsHide)
                    {
                        continue;
                    }

                    await ComicDataManager.Unhide(db, model.Comic);
                }

                await StartSearch(db);
            });
        }
    }
}
