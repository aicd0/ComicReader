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

        public NavigationPage()
        {
            InitializeComponent();
        }

        protected override void OnStart(PageBundle bundle)
        {
            base.OnStart(bundle);
            RspReaderSetting.SetData(ViewModel.ReaderSettingLiveData.GetValue());
        }

        protected override void OnResume()
        {
            base.OnResume();
            ObserveData();
        }

        public LiveData<bool> GetPreviewButtonToggledLiveData()
        {
            return ViewModel.IsPreviewButtonToggledLiveData;
        }

        public LiveData<ReaderSettingDataModel> GetReaderSettingLiveData()
        {
            return ViewModel.ReaderSettingLiveData;
        }

        public LiveData<bool> GetIsExternalLiveData()
        {
            return ViewModel.IsExternalLiveData;
        }

        public LiveData<bool> GetIsFavoriteLiveData()
        {
            return ViewModel.IsFavoriteLiveData;
        }

        public LiveData<bool> GetIsPreviewModeLiveData()
        {
            return ViewModel.IsPreviewButtonToggledLiveData;
        }

        public LiveData<bool> GetExpandInfoPaneLiveData()
        {
            return ViewModel.ExpandInfoPaneLiveData;
        }

        public LiveData<bool> GetIsSidePaneOnLiveData()
        {
            return ViewModel.IsSidePaneOnLiveData;
        }

        public LiveData<bool> GetRefreshLiveData()
        {
            return ViewModel.RefreshLiveData;
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

            ViewModel.IsPreviewButtonToggledLiveData.ObserveSticky(this, delegate (bool toggled)
            {
                if (AbtbPreviewButton.IsChecked != toggled)
                {
                    AbtbPreviewButton.IsChecked = toggled;
                }
            });

            ViewModel.IsFavoriteLiveData.Observe(this, delegate (bool isFavorite)
            {
                FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
                FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
                string toolTip = isFavorite ? StringResourceProvider.GetResourceString("RemoveFromFavorites") :
                    StringResourceProvider.GetResourceString("AddToFavorites");
                ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
            });

            ViewModel.IsExternalLiveData.Observe(this, delegate (bool isExternal)
            {
                AbbAddToFavorite.IsEnabled = !isExternal;
            });
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
            ViewModel.RefreshLiveData.Emit(true);
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
            ViewModel.IsFavoriteLiveData.Emit(!ViewModel.IsFavoriteLiveData.GetValue());
        }

        private void OnComicInfoClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ExpandInfoPaneLiveData.Emit(true);
        }

        // Side pane
        private void OnSidePaneOpened(SplitView sender, object args)
        {
            ViewModel.IsSidePaneOnLiveData.Emit(true);
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
            ViewModel.IsPreviewButtonToggledLiveData.Emit(true);
        }

        private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsPreviewButtonToggledLiveData.Emit(false);
        }

        private void RspReaderSetting_DataChanged(ReaderSettingDataModel data)
        {
            ViewModel.ReaderSettingLiveData.Emit(data);
        }

        private void NavigationPageSidePane_PaneClosed(SplitView sender, object args)
        {
            ViewModel.IsSidePaneOnLiveData.Emit(false);
        }

        private void SidePane_Navigating(NavigationBundle bundle)
        {
            TransferAbilities(bundle);
            bundle.Abilities[typeof(INavigationPageAbility)] = this;
        }
    }
}
