using System;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Views
{
    public class NavigationPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Action RefreshPage;

        private MainPageShared m_MainPageShared;
        public MainPageShared MainPageShared
        {
            get => m_MainPageShared;
            set
            {
                m_MainPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MainPageShared"));
            }
        }

        public Utils.Tab.PageType CurrentPageType
        {
            set
            {
                IsHomePage = value == Utils.Tab.PageType.Home;
                IsReaderPage = value == Utils.Tab.PageType.Reader;

                if (!IsReaderPage && MainPageShared.IsFullscreen)
                {
                    MainPageShared.IsFullscreen = false;
                }
            }
        }

        private bool m_IsHomePage;
        public bool IsHomePage
        {
            get => m_IsHomePage;
            set
            {
                m_IsHomePage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsHomePage"));
            }
        }

        private bool m_IsReaderPage;
        public bool IsReaderPage
        {
            get => m_IsReaderPage;
            set
            {
                m_IsReaderPage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsReaderPage"));
            }
        }

        private bool m_PaneOpen;
        public bool PaneOpen
        {
            get => m_PaneOpen;
            set
            {
                m_PaneOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PaneOpen"));
            }
        }

        private bool? m_IsPreviewModeEnabled = null;
        public bool IsPreviewModeEnabled
        {
            get => m_IsPreviewModeEnabled != null && m_IsPreviewModeEnabled.Value;
            set
            {
                if (m_IsPreviewModeEnabled == null || m_IsPreviewModeEnabled.Value != value)
                {
                    m_IsPreviewModeEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPreviewModeEnabled"));
                    OnPreviewModeChanged?.Invoke();
                }
            }
        }

        private bool m_ZoomInEnabled;
        public bool ZoomInEnabled
        {
            get => m_ZoomInEnabled;
            set
            {
                m_ZoomInEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomInEnabled"));
            }
        }

        private bool m_ZoomOutEnabled;
        public bool ZoomOutEnabled
        {
            get => m_ZoomOutEnabled;
            set
            {
                m_ZoomOutEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomOutEnabled"));
            }
        }

        private bool m_NotExternal;
        public bool NotExternal
        {
            get => m_NotExternal;
            set
            {
                m_NotExternal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NotExternal"));
            }
        }

        private bool m_IsFavorite;
        public bool IsFavorite
        {
            get => m_IsFavorite;
            set
            {
                m_IsFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFavorite"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteButtonToolTip"));
            }
        }

        public string FavoriteButtonToolTip
        {
            get
            {
                return IsFavorite ? "Remove from favorites" : "Add to favorites";
            }
        }

        public Action OnSwitchFavorites;
        public Action OnZoomIn;
        public Action OnZoomOut;
        public Action OnSwitchReaderOrientation;
        public Action OnPreviewModeChanged;
        public Action OnExpandComicInfoPane;
    }

    public sealed partial class NavigationPage : Page
    {
        public static NavigationPage Current;
        public NavigationPageShared Shared { get; set; }
        public Utils.Tab.TabIdentifier TabId => m_tab_manager.TabId;

        private readonly Utils.Tab.TabManager m_tab_manager;

        public NavigationPage()
        {
            Current = this;
            Shared = new NavigationPageShared();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnTabRegister = OnTabRegister;
            m_tab_manager.OnTabUnregister = OnTabUnregister;
            m_tab_manager.OnTabUpdate = OnTabUpdate;
            Unloaded += m_tab_manager.OnTabUnloaded;

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

        private void OnTabRegister(object shared)
        {
            Shared.MainPageShared = (MainPageShared)shared;
            Shared.RefreshPage = RefreshPage;
        }

        private void OnTabUnregister() { }

        private void OnTabUpdate()
        {
            if (NavigationPageSidePane != null)
            {
                NavigationPageSidePane.IsPaneOpen = false;
            }
        }

        // utilities
        public void Update()
        {
            if (ContentFrame == null)
            {
                return;
            }

            // directly passes tab id to the sub page.
            Utils.Tab.NavigationParams nav_params = new Utils.Tab.NavigationParams
            {
                Shared = Shared,
                TabId = m_tab_manager.TabId
            };

            _ = ContentFrame.Navigate(
                Utils.Tab.TabManager.TypeFromPageTypeEnum(m_tab_manager.TabId.Type), nav_params);
        }

        public void SetSearchBox(string keywords)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.Text = keywords;
        }

        // events
        private void SearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { }

        private void SearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.QueryText.Trim().Length == 0)
            {
                return;
            }

            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, args.QueryText);
        }

        private void OnMenuItemSettingsClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Settings);
        }

        private void OnMenuItemHelpClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Help);
        }

        private void FavoritesClick(object sender, RoutedEventArgs e)
        {
            if (NavigationPageSidePane == null)
            {
                return;
            }

            NavigationPageSidePane.IsPaneOpen = !NavigationPageSidePane.IsPaneOpen;
        }

        private void HomeClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Home);
        }

        private void RefreshPage()
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId,
                m_tab_manager.TabId.Type,
                m_tab_manager.TabId.RequestArgs, try_reuse: false);
        }

        private void OnRefreshBtClicked(object sender, RoutedEventArgs e)
        {
            RefreshPage();
        }

        private void GoBackClick(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void GoForwardClick(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoForward)
            {
                ContentFrame.GoForward();
            }
        }

        private void AddToFavoritesClick(object sender, RoutedEventArgs e)
        {
            Shared.OnSwitchFavorites?.Invoke();
        }

        private void ZoomInClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomIn?.Invoke();
        }

        private void ZoomOutClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomOut?.Invoke();
        }

        private void ComicInfoClick(object sender, RoutedEventArgs e)
        {
            Shared.OnExpandComicInfoPane?.Invoke();
        }

        private void SidePaneOpened(SplitView sender, object args)
        {
            Utils.C0.Run(async delegate
            {
                if (FavoritePage.Current != null)
                {
                    await FavoritePage.Current.Update();
                }

                if (HistoryPage.Current != null)
                {
                    await HistoryPage.Current.Update();
                }
            });
        }

        private void OnSwitchReaderOrientationClicked(object sender, RoutedEventArgs e)
        {
            Shared.OnSwitchReaderOrientation?.Invoke();
        }
    }
}
