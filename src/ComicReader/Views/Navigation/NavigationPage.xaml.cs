using ComicReader.Common.Constants;
using ComicReader.Router;
using ComicReader.Utils;
using ComicReader.Views.Base;
using ComicReader.Views.Main;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Views.Navigation
{
    internal class NavigationPageBase : BasePage<NavigationPageViewModel>;

    sealed internal partial class NavigationPage : NavigationPageBase, INavigationPageAbility
    {
        private double _rootTabHeight = 0;
        private double _navigationBarHeight = 0;
        private NavigationBundle _currentBundle;

        private event INavigationPageAbility.ExpandInfoPaneEventHandler ExpandInfoPane;
        private event INavigationPageAbility.GridViewModeChangedEventHandler GridViewModeChanged;
        private event INavigationPageAbility.ReaderSettingsChangedEventHandler ReaderSettingsChanged;
        private event INavigationPageAbility.FavoriteChangedEventHandler FavoriteChanged;

        public NavigationPage()
        {
            InitializeComponent();
        }

        protected override void OnResume()
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

            ViewModel.GridViewModeEnabledLiveData.ObserveSticky(this, new ChangedObserver<bool>(delegate (bool toggled)
            {
                AbtbPreviewButton.IsChecked = toggled;
            }));

            ViewModel.IsFavoriteLiveData.ObserveSticky(this, new ChangedObserver<bool>(delegate (bool isFavorite)
            {
                FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
                FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
                string toolTip = isFavorite ? StringResourceProvider.GetResourceString("RemoveFromFavorites") :
                    StringResourceProvider.GetResourceString("AddToFavorites");
                ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
                FavoriteChanged?.Invoke(isFavorite);
            }));
        }

        public void Navigate(NavigationBundle bundle)
        {
            ContentFrame.Navigated += OnPageChanged;
            TransferAbilities(bundle);
            bundle.Abilities[typeof(INavigationPageAbility)] = this;
            if (!ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle))
            {
                return;
            }
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

        private void OnPageChanged(object sender, NavigationEventArgs e)
        {
            _currentBundle = e.Parameter as NavigationBundle;
            GetMainPageAbility().SetNavigationBundle(_currentBundle);

            NavigationPageSidePane.IsPaneOpen = false;
            bool isHomePage = _currentBundle.PageTrait is HomePageTrait;
            bool isReaderPage = _currentBundle.PageTrait is ReaderPageTrait;
            AbbHomeButton.Visibility = isHomePage ? Visibility.Collapsed : Visibility.Visible;
            AbbRefreshButton.Visibility = isHomePage ? Visibility.Visible : Visibility.Collapsed;
            SearchBox.Visibility = isReaderPage ? Visibility.Collapsed : Visibility.Visible;
            SpCenterButtons.Visibility = isReaderPage ? Visibility.Visible : Visibility.Collapsed;
            SetSearchBox("");
            UpdateTopPadding();
        }

        private IMainPageAbility GetMainPageAbility()
        {
            return GetAbility<IMainPageAbility>();
        }

        private void OnTopTileSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _navigationBarHeight = e.NewSize.Height;
            EventBus.Default.With<double>(EventId.NavigationBarHeightChange).Emit(_navigationBarHeight);
            UpdateTopPadding();
        }

        private void UpdateTopPadding()
        {
            if (_currentBundle.PageTrait.ImmersiveMode())
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

            Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, args.QueryText);
            GetMainPageAbility().OpenInCurrentTab(route);
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
            var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
            GetMainPageAbility().OpenInCurrentTab(route);
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            // TODO implement refresh feature
        }

        private void OnFavoritesClick(object sender, RoutedEventArgs e)
        {
            if (NavigationPageSidePane != null)
            {
                NavigationPageSidePane.IsPaneOpen = !NavigationPageSidePane.IsPaneOpen;
            }
        }

        private void OnMoreSettingsClick(object sender, RoutedEventArgs e)
        {
            var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SETTING);
            MainPage.Current.OpenInNewTab(route);
        }

        private void OnMoreHelpClick(object sender, RoutedEventArgs e)
        {
            var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_HELP);
            MainPage.Current.OpenInNewTab(route);
        }

        private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
        {
            bool isFavorite = !ViewModel.IsFavoriteLiveData.GetValue();
            ViewModel.SetIsFavorite(isFavorite);
        }

        private void OnComicInfoClick(object sender, RoutedEventArgs e)
        {
            ExpandInfoPane?.Invoke();
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

        private void AbtbPreviewButton_Checked(object sender, RoutedEventArgs e)
        {
            GridViewModeChanged?.Invoke(true);
        }

        private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
        {
            GridViewModeChanged?.Invoke(false);
        }

        private void RspReaderSetting_DataChanged(ReaderSettingDataModel data)
        {
            ReaderSettingsChanged?.Invoke(data);
        }

        private void SidePane_Navigating(NavigationBundle bundle)
        {
            TransferAbilities(bundle);
            bundle.Abilities[typeof(INavigationPageAbility)] = this;
        }

        public void SetIsSidePaneOpen(bool isOpen)
        {
            NavigationPageSidePane.IsPaneOpen = isOpen;
        }

        public bool GetIsSidePaneOpen()
        {
            return NavigationPageSidePane.IsPaneOpen;
        }

        public void RegisterReaderSettingsChangedEventHandler(INavigationPageAbility.ReaderSettingsChangedEventHandler handler)
        {
            ReaderSettingsChanged += handler;
        }

        public void SetExternalComic(bool isExternal)
        {
            AbbAddToFavorite.IsEnabled = !isExternal;
        }

        public void RegisterFavoriteChangedEventHandler(INavigationPageAbility.FavoriteChangedEventHandler onFavoriteChanged)
        {
            FavoriteChanged += onFavoriteChanged;
        }

        public void SetFavorite(bool isFavorite)
        {
            ViewModel.SetIsFavorite(isFavorite);
        }

        public void RegisterGridViewModeChangedHandler(INavigationPageAbility.GridViewModeChangedEventHandler handler)
        {
            GridViewModeChanged += handler;
        }

        public void SetGridViewMode(bool enabled)
        {
            ViewModel.SetGridViewMode(enabled);
        }

        public void RegisterExpandInfoPaneHandler(INavigationPageAbility.ExpandInfoPaneEventHandler handler)
        {
            ExpandInfoPane += handler;
        }

        public void SetReaderSettings(ReaderSettingDataModel settings)
        {
            RspReaderSetting.SetData(settings);
        }
    }
}
