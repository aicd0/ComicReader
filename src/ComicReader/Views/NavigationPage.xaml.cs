using System;
using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ComicReader.Common.Router;
using ComicReader.Common;
using ComicReader.Utils;
using ComicReader.Common.Constants;

namespace ComicReader.Views
{
    internal class NavigationPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public TabIdentifier TabId { get; set; }

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

        public Action RefreshPage;
        public Action<string> SetSearchBox;

        private bool? _isPreviewButtonToggled = null;
        public bool IsPreviewButtonToggled
        {
            get => _isPreviewButtonToggled.HasValue && _isPreviewButtonToggled.Value;
            set
            {
                if (!_isPreviewButtonToggled.HasValue || _isPreviewButtonToggled.Value != value)
                {
                    _isPreviewButtonToggled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPreviewButtonToggled"));
                    TabId.TabEventBus.With<bool>(EventId.PreviewModeChanged).Emit(value);
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
    }

    sealed internal partial class NavigationPage : StatefulPage
    {
        public NavigationPageShared Shared { get; set; }
        public TabIdentifier TabId { get; set; }

        private double _rootTabHeight = 0;
        private double _navigationBarHeight = 0;

        public NavigationPage()
        {
            Shared = new NavigationPageShared();
            InitializeComponent();
        }

        // Navigation
        public override void OnStart(object p)
        {
            base.OnStart(p);
            var q = (NavigationParams)p;
            TabId = q.TabId;
            Shared.TabId = TabId;
            Shared.MainPageShared = (MainPageShared)q.Params;
            Shared.RefreshPage = RefreshPage;
            Shared.SetSearchBox = SetSearchBox;
        }

        public override void OnResume()
        {
            base.OnResume();
            ObserveData();
        }

        private void ObserveData()
        {
            EventBus.Default.With<double>(EventId.RootTabHeightChange).ObserveSticky(this, delegate (double h)
            {
                _rootTabHeight = h;
                UpdateTopPadding();
            });

            EventBus.Default.With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
            {
                TopTile.Opacity = opacity;
                TopTile.IsHitTestVisible = opacity > 0.5;
            });

            if (TabId != null)
            {
                TabId.TabEventBus.With(EventId.TabSelected).Observe(this, delegate
                {
                    if (NavigationPageSidePane != null)
                    {
                        NavigationPageSidePane.IsPaneOpen = false;
                    }
                });

                TabId.TabEventBus.With<IPageTrait>(EventId.TabPageChanged).Observe(this, delegate (IPageTrait pageTrait)
                {
                    Shared.IsHomePage = pageTrait is HomePageTrait;
                    Shared.IsReaderPage = pageTrait is ReaderPageTrait;
                    Shared.SetSearchBox("");
                    UpdateTopPadding();
                });
            }
        }

        // Common
        public void Navigate()
        {
            // directly pass parameters to the sub page.
            NavigationParams subParams = new NavigationParams
            {
                Params = Shared,
                TabId = TabId,
            };
            ContentFrame.Navigate(TabId.PageTrait.GetPageType(), subParams);
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
            MainPage.Current.LoadTab(TabId, TabId.PageTrait, TabId.RequestArgs, try_reuse: false);
        }

        private void OnTopTileSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _navigationBarHeight = e.NewSize.Height;
            EventBus.Default.With<double>(EventId.NavigationBarHeightChange).Emit(_navigationBarHeight);
            UpdateTopPadding();
        }

        private void UpdateTopPadding()
        {
            if (TabId.PageTrait.ImmersiveMode())
            {
                TopTile.Margin = new Thickness(0, _rootTabHeight, 0, 0);
                ContentGrid.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                // MainPage has done that job for us.
                TopTile.Margin = new Thickness(0, 0, 0, 0);
                ContentGrid.Margin = new Thickness(0, _navigationBarHeight, 0, 0);
            }
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

            MainPage.Current.LoadTab(TabId, SearchPageTrait.Instance, args.QueryText);
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
            MainPage.Current.LoadTab(TabId, HomePageTrait.Instance);
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
            MainPage.Current.LoadTab(null, SettingPageTrait.Instance);
        }

        private void OnMoreHelpClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(null, HelpPageTrait.Instance);
        }

        private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
        {
            TabId.TabEventBus.With(EventId.SwitchFavorites).Emit(0);
        }

        private void OnComicInfoClick(object sender, RoutedEventArgs e)
        {
            TabId.TabEventBus.With(EventId.ExpandInfoPane).Emit(0);
        }

        // Side pane
        private void OnSidePaneOpened(SplitView sender, object args)
        {
            EventBus.Default.With(EventId.SidePaneUpdate).Emit(0);
        }

        // Pointer events
        private PointerPoint m_last_pointer_point = null;

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
    }
}
