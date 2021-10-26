using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ComicReader.Data;

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

        private bool m_IsTwoPagesMode;
        public bool IsTwoPagesMode
        {
            get => m_IsTwoPagesMode;
            set
            {
                m_IsTwoPagesMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTwoPagesMode"));
                OnTwoPagesModeChanged?.Invoke();
            }
        }

        private bool m_IsGridViewMode;
        public bool IsGridViewMode
        {
            get => m_IsGridViewMode;
            set
            {
                m_IsGridViewMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGridViewMode"));
                OnGridViewModeChanged?.Invoke();
            }
        }

        private bool m_CanZoomIn;
        public bool CanZoomIn
        {
            get => m_CanZoomIn;
            set
            {
                m_CanZoomIn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanZoomIn"));
            }
        }

        private bool m_CanZoomOut;
        public bool CanZoomOut
        {
            get => m_CanZoomOut;
            set
            {
                m_CanZoomOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanZoomOut"));
            }
        }

        private bool m_IsInLib;
        public bool IsInLib
        {
            get => m_IsInLib;
            set
            {
                m_IsInLib = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsInLib"));
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

        // events
        public Action OnFavoritesButtonClicked;
        public Action OnZoomInButtonClicked;
        public Action OnZoomOutButtonClicked;
        public Action OnTwoPagesModeChanged;
        public Action OnGridViewModeChanged;
    }

    public sealed partial class ContentPage : Page
    {
        public static ContentPage Current;
        public ContentPageShared Shared { get; set; }

        private Utils.Tab.TabManager m_tab_manager;

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
            if (ContentPageUtilityPane != null)
            {
                ContentPageUtilityPane.IsPaneOpen = false;
            }
        }

        // update
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

        // events processing
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {

        }

        public void SetSearchBox(string keywords)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.Text = keywords;
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RootPage.Current.LoadTab(m_tab_manager.TabId,
                Utils.Tab.PageType.Search, args.QueryText);
        }

        private void SettingBt_Click(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(null, Utils.Tab.PageType.Settings);
        }

        private void FavoriteBt_Click(object sender, RoutedEventArgs e)
        {
            if (ContentPageUtilityPane == null)
            {
                return;
            }

            ContentPageUtilityPane.IsPaneOpen = !ContentPageUtilityPane.IsPaneOpen;
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

        private void AddToFavoriteBt_Click(object sender, RoutedEventArgs e)
        {
            Shared.OnFavoritesButtonClicked?.Invoke();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomInButtonClicked?.Invoke();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Shared.OnZoomOutButtonClicked?.Invoke();
        }

        private void ComicInfoBt_Click(object sender, RoutedEventArgs e)
        {
            ReaderPage.Current.ExpandInfoPane();
        }

        private void ContentPageUtilityPane_PaneOpened(SplitView sender, object args)
        {
            Utils.Methods.Run(async delegate
            {
                if (FavoritesPage.Current != null)
                {
                    await FavoritesPage.Current.UpdateTreeExplorer();
                }

                if (HistoryPage.Current != null)
                {
                    await HistoryPage.Current.UpdateHistory();
                }
            });
        }
    }
}
