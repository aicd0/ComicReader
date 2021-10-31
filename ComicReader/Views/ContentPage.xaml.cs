using System;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Views
{
    public class ContentPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private RootPageShared m_RootPageShared;
        public RootPageShared RootPageShared
        {
            get => m_RootPageShared;
            set
            {
                m_RootPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RootPageShared"));
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

        private bool m_TwoPagesMode;
        public bool TwoPagesMode
        {
            get => m_TwoPagesMode;
            set
            {
                m_TwoPagesMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TwoPagesMode"));
                OnTwoPagesModeChanged?.Invoke();
            }
        }

        private bool m_PreviewMode;
        public bool PreviewMode
        {
            get => m_PreviewMode;
            set
            {
                m_PreviewMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PreviewMode"));
                OnGridViewModeChanged?.Invoke();
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
            }
        }

        public Action OnFavoritesClicked;
        public Action OnZoomInButtonClicked;
        public Action OnZoomOutButtonClicked;
        public Action OnTwoPagesModeChanged;
        public Action OnGridViewModeChanged;
    }

    public sealed partial class ContentPage : Page
    {
        public static ContentPage Current;
        public ContentPageShared Shared { get; set; }
        public Utils.Tab.TabIdentifier TabId => m_tab_manager.TabId;

        private readonly Utils.Tab.TabManager m_tab_manager;

        public ContentPage()
        {
            Current = this;
            Shared = new ContentPageShared();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnPageEntered = OnPageEntered;

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

        private void OnSetShared(object shared)
        {
            Shared.RootPageShared = (RootPageShared)shared;
        }

        private void OnPageEntered()
        {
            if (ContentPageSidePane != null)
            {
                ContentPageSidePane.IsPaneOpen = false;
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

            RootPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, args.QueryText);
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(null, Utils.Tab.PageType.Settings);
        }

        private void FavoritesClick(object sender, RoutedEventArgs e)
        {
            if (ContentPageSidePane == null)
            {
                return;
            }

            ContentPageSidePane.IsPaneOpen = !ContentPageSidePane.IsPaneOpen;
        }

        private void HomeClick(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Home);
        }

        private void RefreshClick(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(m_tab_manager.TabId,
                m_tab_manager.TabId.Type,
                m_tab_manager.TabId.RequestArgs, try_reuse: false);
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
            Shared.OnFavoritesClicked?.Invoke();
        }

        private void ZoomInClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomInButtonClicked?.Invoke();
        }

        private void ZoomOutClick(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomOutButtonClicked?.Invoke();
        }

        private void ComicInfoClick(object sender, RoutedEventArgs e)
        {
            ReaderPage.Current.ExpandInfoPane();
        }

        private void SidePaneOpened(SplitView sender, object args)
        {
            Utils.Methods.Run(async delegate
            {
                if (FavoritesPage.Current != null)
                {
                    await FavoritesPage.Current.Update();
                }

                if (HistoryPage.Current != null)
                {
                    await HistoryPage.Current.Update();
                }
            });
        }
    }
}
