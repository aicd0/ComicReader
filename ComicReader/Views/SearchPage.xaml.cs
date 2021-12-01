using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Data;

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
        public Utils.ObservableCollectionPlus<ComicItemModel> SearchResults;

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
    }

    public sealed partial class SearchPage : Page
    {
        public static SearchPage Current;
        private SearchPageShared Shared { get; set; }

        private Utils.Tab.TabManager m_tab_manager;
        private List<ComicItemData> m_all_results;
        private Utils.CancellationLock m_search_lock;

        public SearchPage()
        {
            Current = this;
            Shared = new SearchPageShared();
            Shared.Title = "";
            Shared.FilterDetails = "";
            Shared.SearchResults = new Utils.ObservableCollectionPlus<ComicItemModel>();
            
            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnRegister = OnRegister;
            m_tab_manager.OnUnregister = OnUnregister;
            m_tab_manager.OnUpdate = OnUpdate;
            Unloaded += m_tab_manager.OnUnloaded;
            m_all_results = new List<ComicItemData>();
            m_search_lock = new Utils.CancellationLock();

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

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            Utils.Methods.Run(async delegate
            {
                Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Search;
                NavigationPage.Current.SetSearchBox((string)tab_id.RequestArgs);
                await StartSearch();
            });
        }

        public static string PageUniqueString(object args)
        {
            string keyword = (string)args;
            return "Search/" + keyword;
        }

        // update
        private async Task StartSearch()
        {
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

            if (!await SearchMain(keywords, filter))
            {
                return;
            }

            Shared.IsLoading = false;

            // update UI
            Shared.Title = title_text;
            Shared.FilterDetails = filter_details;
            Shared.NoResultText = "No results for \"" + keyword + "\"";
            Shared.SearchResults.Clear();
            await LoadMoreResults(40);
        }

        private class Match
        {
            public ComicItemData Comic;
            public int Similarity = 0;
        }

        private async Task<bool> SearchMain(List<string> keywords, Utils.Search.Filter filter)
        {
            await m_search_lock.WaitAsync();

            try
            {
                for (int i = 0; i < keywords.Count; ++i)
                {
                    keywords[i] = keywords[i].ToLower();
                }

                await DatabaseManager.WaitLock();
                List<Match> matches = new List<Match>();

                foreach (ComicItemData comic in Database.Comic.Items)
                {
                    // cancel the current session if the next search begins
                    if (m_search_lock.CancellationRequested)
                    {
                        return false;
                    }

                    if (!filter.Pass(comic))
                    {
                        continue;
                    }

                    int similarity = 0;
                    if (keywords.Count != 0)
                    {
                        string match_text = comic.Title1 + " " + comic.Title2;
                        similarity = Utils.StringUtils.QuickMatch(keywords, match_text);

                        if (similarity < 1)
                        {
                            continue;
                        }
                    }

                    Match match = new Match
                    {
                        Comic = comic,
                        Similarity = similarity
                    };

                    matches.Add(match);
                }

                DatabaseManager.ReleaseLock();
                matches = matches.OrderByDescending(x => x.Similarity).ToList();
                m_all_results.Clear();

                foreach (Match match in matches)
                {
                    m_all_results.Add(match.Comic);
                }
            }
            finally
            {
                m_search_lock.Release();
            }

            return true;
        }

        private async Task LoadMoreResults(int count)
        {
            await m_search_lock.WaitAsync();

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
                List<ComicItemModel> results_tmp = new List<ComicItemModel>();

                for (int i = items_loaded; i < end_i; ++i)
                {
                    ComicItemData comic = m_all_results[i];

                    ComicItemModel result = new ComicItemModel
                    {
                        OnItemPressed = OnListViewPressed,
                        OnHideClicked = HideClick,
                        OnUnhideClicked = UnhideClick,
                        OnAddToFavoritesClicked = AddToFavoritesBtClick,
                        OnRemoveFromFavoritesClicked = RemoveFromFavoritesBtClick,
                        Comic = comic,
                        Title = comic.Title,
                        Detail = "#" + comic.Id,
                        Id = comic.Id,
                        IsFavorite = await FavoriteDataManager.FromId(comic.Id) != null
                    };

                    ComicExtraItemData extra_data = await comic.GetExtraData();
                    
                    if (extra_data != null)
                    {
                        result.Rating = extra_data.Rating;
                        result.Progress = extra_data.Progress >= 100 ? "Finished" : extra_data.Progress.ToString() + "% Completed";
                    }

                    results_tmp.Add(result);
                }

                // update UI
                foreach (ComicItemModel result in results_tmp)
                {
                    Shared.SearchResults.Add(result);
                }

                Shared.UpdateUI();

                // load images
                double image_height = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
                List<ImageLoaderToken> image_loader_tokens = new List<ImageLoaderToken>();
                
                foreach (ComicItemModel item in Shared.SearchResults.Skip(items_loaded))
                {
                    image_loader_tokens.Add(new ImageLoaderToken
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

                await ComicDataManager.LoadImages(image_loader_tokens, double.PositiveInfinity, image_height, m_search_lock);
            }
            finally
            {
                Shared.UpdateUI();
                m_search_lock.Release();
            }
        }

        // events processing
        private void OnListViewPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                PointerPoint pt = e.GetCurrentPoint((UIElement)sender);
                if (!pt.Properties.IsLeftButtonPressed)
                {
                    return;
                }

                ComicItemModel item = (ComicItemModel)((FrameworkElement)sender).DataContext;
                ComicItemData comic = await ComicDataManager.FromId(item.Id);
                MainPage.Current.LoadTab(null, Utils.Tab.PageType.Reader, comic);
            });
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ScrollViewer scrollViewer = (ScrollViewer)sender;

                if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < scrollViewer.ActualHeight * 0.5)
                {
                    await LoadMoreResults(12);
                }
            });
        }

        private void AddToFavoritesBtClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel result = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                result.IsFavorite = true;
                Utils.Methods1<ComicItemModel>.NotifyCollectionChanged(Shared.SearchResults, result);
                await FavoriteDataManager.Add(result.Id, result.Title, true);
            });
        }

        private void RemoveFromFavoritesBtClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel result = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                result.IsFavorite = false;
                Utils.Methods1<ComicItemModel>.NotifyCollectionChanged(Shared.SearchResults, result);
                await FavoriteDataManager.RemoveWithId(result.Id, true);
            });
        }

        private void UnhideClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel ctx = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Unhide(ctx.Comic);
                await StartSearch();
            });
        }

        private void HideClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel ctx = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Hide(ctx.Comic);
                await StartSearch();
            });
        }
    }
}
