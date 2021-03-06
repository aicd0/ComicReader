using System;
using System.ComponentModel;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Views
{
    public class NavigationPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

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

        public Common.Tab.PageType CurrentPageType
        {
            set
            {
                IsHomePage = value == Common.Tab.PageType.Home;
                IsReaderPage = value == Common.Tab.PageType.Reader;

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

        private bool m_IsSidePaneOpen;
        public bool IsSidePaneOpen
        {
            get => m_IsSidePaneOpen;
            set
            {
                m_IsSidePaneOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSidePaneOpen"));
            }
        }

        public Action<object, KeyRoutedEventArgs> OnKeyDown;
        public Action RefreshPage;
        public Action<string> SetSearchBox;

        private bool? m_IsPreviewButtonToggled = null;
        public bool IsPreviewButtonToggled
        {
            get => m_IsPreviewButtonToggled.HasValue && m_IsPreviewButtonToggled.Value;
            set
            {
                if (!m_IsPreviewButtonToggled.HasValue || m_IsPreviewButtonToggled.Value != value)
                {
                    m_IsPreviewButtonToggled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPreviewButtonToggled"));
                    OnPreviewModeChanged?.Invoke();
                }
            }
        }

        private bool m_IsVerticalReaderVisible = false;
        public bool IsVerticalReaderVisible
        {
            get => m_IsVerticalReaderVisible;
            set
            {
                m_IsVerticalReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsVerticalReaderVisible"));
            }
        }

        private bool m_IsHorizontalReaderVisible = false;
        public bool IsHorizontalReaderVisible
        {
            get => m_IsHorizontalReaderVisible;
            set
            {
                m_IsHorizontalReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsHorizontalReaderVisible"));
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

        private bool m_IsExternal;
        public bool IsExternal
        {
            get => m_IsExternal;
            set
            {
                m_IsExternal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExternal"));
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
                if (IsFavorite)
                {
                    return Utils.StringResourceProvider.GetResourceString("RemoveFromFavorites");
                }
                else
                {
                    return Utils.StringResourceProvider.GetResourceString("AddToFavorites");
                }
            }
        }

        public DesignData.ReaderSettingViewModel ReaderSettings => MainPageShared.ReaderSettings;

        public Action OnSwitchFavorites;
        public Action OnZoomIn;
        public Action OnZoomOut;
        public Action OnPreviewModeChanged;
        public Action OnExpandComicInfoPane;
    }

    public sealed partial class NavigationPage : Page
    {
        public NavigationPageShared Shared { get; set; }
        public Common.Tab.TabIdentifier TabId => m_tab_manager.TabId;

        private readonly Common.Tab.TabManager m_tab_manager;

        public NavigationPage()
        {
            Shared = new NavigationPageShared();

            m_tab_manager = new Common.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabUpdate = OnTabUpdate
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
            Shared.MainPageShared = (MainPageShared)shared;
            Shared.RefreshPage = RefreshPage;
            Shared.SetSearchBox = SetSearchBox;
        }

        private void OnTabUnregister() { }

        private void OnTabUpdate()
        {
            if (NavigationPageSidePane != null)
            {
                NavigationPageSidePane.IsPaneOpen = false;
            }
        }

        // Common
        public void Update()
        {
            if (ContentFrame == null)
            {
                return;
            }

            // directly passes tab id to the sub page.
            Common.Tab.NavigationParams nav_params = new Common.Tab.NavigationParams
            {
                Shared = Shared,
                TabId = m_tab_manager.TabId
            };

            _ = ContentFrame.Navigate(
                Common.Tab.TabManager.TypeFromPageTypeEnum(m_tab_manager.TabId.Type), nav_params);
        }

        public bool GoBack()
        {
            if (ContentFrame == null)
            {
                return false;
            }

            if (!ContentFrame.CanGoBack)
            {
                return false;
            }

            ContentFrame.GoBack();
            return true;
        }

        public bool GoForward()
        {
            if (ContentFrame == null)
            {
                return false;
            }

            if (!ContentFrame.CanGoForward)
            {
                return false;
            }

            ContentFrame.GoForward();
            return true;
        }

        private void RefreshPage()
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId,
                m_tab_manager.TabId.Type,
                m_tab_manager.TabId.RequestArgs, try_reuse: false);
        }

        // Search box
        public void SetSearchBox(string keywords)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.Text = keywords;
        }

        private void OnSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { }

        private void OnSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.QueryText.Trim().Length == 0)
            {
                return;
            }

            MainPage.Current.LoadTab(m_tab_manager.TabId, Common.Tab.PageType.Search, args.QueryText);
        }

        // Buttons
        private void OnGoBackClick(object sender, RoutedEventArgs e)
        {
            _ = GoBack();
        }

        private void OnGoForwardClick(object sender, RoutedEventArgs e)
        {
            _ = GoForward();
        }

        private void OnHomeClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId, Common.Tab.PageType.Home);
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (Shared.IsHomePage)
            {
                HomePage.RefreshPage();
            }
            else
            {
                RefreshPage();
            }
        }

        private void OnFavoritesClick(object sender, RoutedEventArgs e)
        {
            if (NavigationPageSidePane == null)
            {
                return;
            }

            NavigationPageSidePane.IsPaneOpen = !NavigationPageSidePane.IsPaneOpen;
        }

        private void OnMoreSettingsClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(null, Common.Tab.PageType.Settings);
        }

        private void OnMoreHelpClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(null, Common.Tab.PageType.Help);
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomIn?.Invoke();
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomOut?.Invoke();
        }

        private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
        {
            Shared.OnSwitchFavorites?.Invoke();
        }

        private void OnComicInfoClick(object sender, RoutedEventArgs e)
        {
            Shared.OnExpandComicInfoPane?.Invoke();
        }

        // Side pane
        private void OnSidePaneOpened(SplitView sender, object args)
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

        // Pointer events
        PointerPoint m_last_pointer_point = null;

        private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            m_last_pointer_point = e.GetCurrentPoint(sender as UIElement);
        }

        private void OnPagePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (m_last_pointer_point == null)
            {
                return;
            }

            if (m_last_pointer_point.Properties.IsXButton1Pressed)
            {
                _ = GoBack();
            }
            else if (m_last_pointer_point.Properties.IsXButton2Pressed)
            {
                _ = GoForward();
            }

            m_last_pointer_point = null;
        }

        // Keys
        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Shared.OnKeyDown?.Invoke(sender, e);
        }
    }
}
