// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Database;
using ComicReader.Router;
using ComicReader.Views.Base;
using ComicReader.Views.Main;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Views.Navigation;

internal sealed partial class NavigationPage : BasePage
{
    private const string TAG = "NavigationPage";

    private bool _isFavorite = false;
    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;
    private NavigationBundle _currentBundle;
    private readonly NavigationPageAbility _ability;

    public NavigationPage()
    {
        InitializeComponent();
        _ability = new(this);
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
    }

    public void Navigate(NavigationBundle bundle)
    {
        TransferAbilities(bundle);
        bundle.Abilities[typeof(INavigationPageAbility)] = _ability;

        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
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
        _ability.SendLeavingEvent();
        _ability.ClearSubscriptions();

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
        _isFavorite = !_isFavorite;
        SetIsFavorite(_isFavorite);
    }

    private void OnComicInfoClick(object sender, RoutedEventArgs e)
    {
        _ability.SendExpandInfoPaneEvent();
    }

    private void SetIsFavorite(bool isFavorite)
    {
        FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
        string toolTip = isFavorite ? StringResourceProvider.GetResourceString("RemoveFromFavorites") :
            StringResourceProvider.GetResourceString("AddToFavorites");
        ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
        _ability.SendFavoriteChangedEvent(isFavorite);
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
        _ability.SendGridViewModeChangedEvent(true);
    }

    private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _ability.SendGridViewModeChangedEvent(false);
    }

    private void RspReaderSetting_DataChanged(ReaderSettingDataModel data)
    {
        _ability.SendReaderSettingsChangedEvent(data);
        C0.Run(async delegate
        {
            await XmlDatabaseManager.WaitLock();

            Database.XmlDatabase.Settings.VerticalReading = data.IsVertical;
            Database.XmlDatabase.Settings.LeftToRight = data.IsLeftToRight;
            Database.XmlDatabase.Settings.VerticalContinuous = data.IsVerticalContinuous;
            Database.XmlDatabase.Settings.HorizontalContinuous = data.IsHorizontalContinuous;
            Database.XmlDatabase.Settings.VerticalPageArrangement = data.VerticalPageArrangement;
            Database.XmlDatabase.Settings.HorizontalPageArrangement = data.HorizontalPageArrangement;

            XmlDatabaseManager.ReleaseLock();
            TaskQueue.DefaultQueue.Enqueue($"{TAG}#RspReaderSetting_DataChanged", XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
        });
    }

    private void SidePane_Navigating(NavigationBundle bundle)
    {
        TransferAbilities(bundle);
        bundle.Abilities[typeof(INavigationPageAbility)] = _ability;
    }

    private void SetGridViewModeEnabled(bool enabled)
    {
        AbtbPreviewButton.IsChecked = enabled;
    }

    private class NavigationPageAbility : INavigationPageAbility
    {
        private const string EVENT_LEAVING = "Leaving";
        private const string EVENT_EXPAND_INFO_PANE = "ExpandInfoPane";
        private const string EVENT_FAVORITE_CHANGED = "FavoriteChanged";
        private const string EVENT_GRID_VIEW_MODE_CHANGED = "GridViewModeChanged";
        private const string EVENT_READER_SETTINGS_CHANGED = "ReaderSettingsChanged";

        private readonly WeakReference<NavigationPage> _parent;
        private readonly EventBus _eventBus = new();

        public NavigationPageAbility(NavigationPage parent)
        {
            _parent = new WeakReference<NavigationPage>(parent);
        }

        public void ClearSubscriptions()
        {
            _eventBus.Clear();
        }

        public bool GetIsSidePaneOpen()
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return false;
            }

            return parent.NavigationPageSidePane.IsPaneOpen;
        }

        public void SetExternalComic(bool isExternal)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.AbbAddToFavorite.IsEnabled = !isExternal;
        }

        public void SetFavorite(bool isFavorite)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.SetIsFavorite(isFavorite);
        }

        public void SetGridViewMode(bool enabled)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.SetGridViewModeEnabled(enabled);
        }

        public void SetIsSidePaneOpen(bool isOpen)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.NavigationPageSidePane.IsPaneOpen = isOpen;
        }

        public void SetReaderSettings(ReaderSettingDataModel settings)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.RspReaderSetting.SetData(settings);
        }

        public void SetSearchBox(string text)
        {
            if (!_parent.TryGetTarget(out NavigationPage parent))
            {
                return;
            }

            parent.SetSearchBox(text);
        }

        public void RegisterLeavingHandler(Page owner, INavigationPageAbility.CommonEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_LEAVING).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendLeavingEvent()
        {
            _eventBus.With<bool>(EVENT_LEAVING).Emit(true);
        }

        public void RegisterExpandInfoPaneHandler(Page owner, INavigationPageAbility.CommonEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_EXPAND_INFO_PANE).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendExpandInfoPaneEvent()
        {
            _eventBus.With<bool>(EVENT_EXPAND_INFO_PANE).Emit(true);
        }

        public void RegisterFavoriteChangedEventHandler(Page owner, INavigationPageAbility.FavoriteChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).Observe(owner, delegate (bool isFavorite)
            {
                handler(isFavorite);
            });
        }

        public void SendFavoriteChangedEvent(bool isFavorite)
        {
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).Emit(isFavorite);
        }

        public void RegisterGridViewModeChangedHandler(Page owner, INavigationPageAbility.GridViewModeChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).Observe(owner, delegate (bool isGridViewMode)
            {
                handler(isGridViewMode);
            });
        }

        public void SendGridViewModeChangedEvent(bool isGridViewMode)
        {
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).Emit(isGridViewMode);
        }

        public void RegisterReaderSettingsChangedEventHandler(Page owner, INavigationPageAbility.ReaderSettingsChangedEventHandler handler)
        {
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).Observe(owner, delegate (ReaderSettingDataModel settings)
            {
                handler(settings);
            });
        }

        public void SendReaderSettingsChangedEvent(ReaderSettingDataModel settings)
        {
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).Emit(settings.Clone());
        }
    }
}
