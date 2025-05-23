// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.Views.Navigation;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;

namespace ComicReader.Views.Main;

internal sealed partial class MainPage : BasePage
{
    public static MainPage Current = null;
    private static FileActivatedEventArgs s_startupFileArgs;

    private bool _isFirstStartUp = true;
    private readonly List<TabInfo> _tabs = new();
    private TabInfo _currentTab;
    private int _nextTabId = 0;
    private Grid _tabContainerGrid;
    private ContentPresenter _tabContentPresenter;
    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;
    private KeyFrameAnimation _titleBarAnimation;

    public MainPage()
    {
        Current = this;
        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();

        if (_isFirstStartUp)
        {
            _isFirstStartUp = false;
            _ = OnFirstStartUp();
        }
    }

    private async Task OnFirstStartUp()
    {
        bool page_started = false;

        if (s_startupFileArgs != null)
        {
            page_started = await OpenFileActivatedComic(s_startupFileArgs);
        }

        if (!page_started)
        {
            long id = AppData.GetReadingComic();
            if (id >= 0)
            {
                ComicData comic = await ComicData.FromId(id, "FetchLastComic");
                if (comic != null)
                {
                    Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    OpenInNewTab(route);
                    page_started = true;
                }
            }
        }

        if (!page_started)
        {
            var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
            OpenInNewTab(route);
        }
    }

    public void OpenInNewTab(Route route)
    {
        LoadTab(-1, route);
    }

    public void ShowOrHideTitleBar(bool show)
    {
        if (_currentTab == null || !_currentTab.CurrentPageTrait.ImmersiveMode())
        {
            return;
        }

        if (_titleBarAnimation == null)
        {
            _titleBarAnimation = new KeyFrameAnimation
            {
                Duration = 0.2,
                UpdateCallback = delegate (double value)
                {
                    EventBus.Default.With<double>(EventId.TitleBarOpacity).Emit(value);
                }
            };
        }
        else
        {
            _titleBarAnimation.RemoveAllKeyFrames();
        }

        _titleBarAnimation.StartValue = _tabContainerGrid.Opacity;
        if (show)
        {
            _titleBarAnimation.InsertKeyFrame(1.0, 1.0);
        }
        else
        {
            _titleBarAnimation.InsertKeyFrame(1.0, 0.0);
        }

        _titleBarAnimation.Start();
    }

    private void ObserveData()
    {
        EventBus.Default.With<double>(EventId.RootTabHeightChange).ObserveSticky(this, delegate (double h)
        {
            _rootTabHeight = h;
            EventBus.Default.With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
            UpdateTopPadding();
        });

        EventBus.Default.With<double>(EventId.NavigationBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            _navigationBarHeight = h;
            EventBus.Default.With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
        });

        EventBus.Default.With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            if (_tabContainerGrid != null)
            {
                _tabContainerGrid.Opacity = opacity;
                _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
            }
        });
    }

    // File activation
    private async Task<ComicData> GetStartupComic(FileActivatedEventArgs args)
    {
        var target_file = (StorageFile)args.Files[0];

        if (!AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
        {
            return null;
        }

        ComicData comic = null;

        if (AppInfoProvider.IsSupportedDocumentExtension(target_file.FileType))
        {
            comic = await ComicData.FromLocation(target_file.Path, "MainGetStartupComicFromDocument");

            if (comic == null)
            {
                switch (target_file.FileType.ToLower())
                {
                    case ".pdf":
                        comic = await ComicPdfData.FromExternal(target_file);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (AppInfoProvider.IsSupportedArchiveExtension(target_file.FileType))
        {
            comic = await ComicData.FromLocation(target_file.Path, "MainGetStartupComicFromArchive");
            comic ??= await ComicArchiveData.FromExternal(target_file);
        }
        else if (AppInfoProvider.IsSupportedImageExtension(target_file.FileType))
        {
            string dir = target_file.Path;
            dir = StringUtils.ParentLocationFromLocation(dir);
            comic = await ComicData.FromLocation(dir, "MainGetStartupComicFromImage");

            if (comic == null)
            {
                StorageFile info_file = null;
                var all_files = new List<StorageFile>();
                var img_files = new List<StorageFile>();
                StorageFileQueryResult neighboring_file_query =
                    args.NeighboringFilesQuery;

                if (neighboring_file_query != null)
                {
                    IReadOnlyList<StorageFile> files = await args.NeighboringFilesQuery.GetFilesAsync();
                    all_files = files.ToList();
                }

                if (all_files.Count == 0)
                {
                    foreach (IStorageItem item in args.Files)
                    {
                        if (item is StorageFile file)
                        {
                            all_files.Add(file);
                        }
                    }
                }

                foreach (StorageFile file in all_files)
                {
                    if (file.Name.ToLower().Equals(ComicData.COMIC_INFO_FILE_NAME))
                    {
                        info_file = file;
                    }
                    else if (AppInfoProvider.IsSupportedImageExtension(file.FileType))
                    {
                        img_files.Add(file);
                    }
                }

                comic = await ComicFolderData.FromExternal(dir, img_files, info_file);
            }
        }

        return comic;
    }

    private int AddTab(NavigationBundle bundle)
    {
        var item = new TabViewItem
        {
            Header = "Loading...",
            Content = new Frame()
        };

        int tabId = _nextTabId++;
        var tabInfo = new TabInfo(tabId, item);
        var ability = new MainPageAbility(this, tabId);
        tabInfo.CurrentPageTrait = bundle.PageTrait;
        tabInfo.CurrentUrl = bundle.Url;
        tabInfo.Ability = ability;
        RegisterPageAbility(bundle.Communicator, ability);
        _tabs.Add(tabInfo);
        RootTabView.TabItems.Add(item);
        return tabId;
    }

    private void LoadTab(int tabId, Route route)
    {
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            LoadTabInternal(tabId, route);
        });
    }

    private void LoadTabInternal(int tabId, Route route)
    {
        if (tabId < -1)
        {
            throw new ArgumentException();
        }

        RouteInfo routeInfo = route.Build();
        NavigationBundle bundle = AppRouter.Process(routeInfo);

        if (!bundle.PageTrait.SupportMultiInstance())
        {
            foreach (TabInfo tab in _tabs)
            {
                if (tab.CurrentUrl == bundle.Url)
                {
                    RootTabView.SelectedItem = tab.Item;
                    return;
                }
            }
        }

        bool newTab = tabId == -1;

        if (newTab)
        {
            tabId = AddTab(bundle);
        }

        TabInfo tabInfo = GetTabInfo(tabId);

        if (tabInfo == null)
        {
            DebugUtils.Assert(false);
            return;
        }

        RootTabView.SelectedItem = tabInfo.Item;

        if (!newTab && tabInfo.CurrentUrl == bundle.Url)
        {
            return;
        }

        var frame = (Frame)tabInfo.Item.Content;

        if (bundle.PageTrait.HasNavigationBar())
        {
            if (frame.Content == null || frame.Content.GetType() != typeof(NavigationPage))
            {
                var navigationRoute = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_NAVIGATION);
                NavigationBundle navigationPageBundle = AppRouter.Process(navigationRoute.Build());
                RegisterPageAbility(navigationPageBundle.Communicator, tabInfo.Ability);
                if (!frame.Navigate(navigationPageBundle.PageTrait.GetPageType(), navigationPageBundle))
                {
                    return;
                }
            }

            var contentPage = (NavigationPage)frame.Content;
            contentPage.Navigate(bundle);
        }
        else
        {
            frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        }

        OnPageChanged();
    }

    private void CloseTab(int tabId)
    {
        if (tabId < 0)
        {
            return;
        }

        TabInfo closingTab = null;
        for (int i = 0; i < _tabs.Count; ++i)
        {
            TabInfo tabInfo = _tabs[i];
            if (tabInfo.Id == tabId)
            {
                closingTab = tabInfo;
                break;
            }
        }
        if (closingTab == null)
        {
            return;
        }

        closingTab.Ability.DispatchPageStoppedEvent();
        _tabs.Remove(closingTab);
        RootTabView.TabItems.Remove(closingTab.Item);

        if (RootTabView.TabItems.Count <= 0)
        {
            AppUtils.Exit();
        }
    }

    private TabInfo GetTabInfo(int tabId)
    {
        foreach (TabInfo tab in _tabs)
        {
            if (tab.Id == tabId)
            {
                return tab;
            }
        }

        return null;
    }

    // TabView
    private void OnAddTabButtonClicked(TabView sender, object args)
    {
        var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
        OpenInNewTab(route);
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        int closingTabId = -1;
        for (int i = 0; i < _tabs.Count; ++i)
        {
            TabInfo tabInfo = _tabs[i];
            if (tabInfo.Item == args.Tab)
            {
                closingTabId = tabInfo.Id;
                break;
            }
        }

        CloseTab(closingTabId);
    }

    private void OnTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }

        TabInfo lastSelectedTab = _currentTab;
        var newSelectedTabItem = (TabViewItem)e.AddedItems[0];
        TabInfo newSelectedTab = null;
        foreach (TabInfo tabInfo in _tabs)
        {
            if (tabInfo.Item == newSelectedTabItem)
            {
                newSelectedTab = tabInfo;
            }
        }

        DebugUtils.Assert(newSelectedTab != null);
        _currentTab = newSelectedTab;

        if (lastSelectedTab != null)
        {
            DispatchToTab(lastSelectedTab, delegate (MainPageAbility ability)
            {
                ability.SendTabUnselectedEvent();
            });
        }

        OnPageChanged();
    }

    private void OnPageChanged()
    {
        UpdateTopPadding();

        TabInfo currentTab = _currentTab;
        if (currentTab != null)
        {
            IPageTrait pageTrait = currentTab.CurrentPageTrait;

            if (!pageTrait.ImmersiveMode())
            {
                _titleBarAnimation?.Stop();
                EventBus.Default.With<double>(EventId.TitleBarOpacity).Emit(1.0);
            }

            if (!pageTrait.SupportFullscreen())
            {
                ExitFullscreen();
            }
        }
    }

    private void UpdateTopPadding()
    {
        if (_currentTab == null || _tabContentPresenter == null)
        {
            return;
        }

        if (_currentTab.CurrentPageTrait.ImmersiveMode())
        {
            _tabContentPresenter.Margin = new Thickness(0, 0, 0, 0);
            RootTabView.Background = (Brush)Application.Current.Resources["TitleBarBackground"];
        }
        else
        {
            _tabContentPresenter.Margin = new Thickness(0, _rootTabHeight, 0, 0);
            RootTabView.Background = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void OnTabContainerGridLoaded(object sender, RoutedEventArgs e)
    {
        _tabContainerGrid = sender as Grid;
    }

    private void OnTabContentPresenterLoaded(object sender, RoutedEventArgs e)
    {
        _tabContentPresenter = sender as ContentPresenter;
    }

    private void OnTabContainerGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        EventBus.Default.With<double>(EventId.RootTabHeightChange).Emit(e.NewSize.Height);
    }

    // Keys
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                ExitFullscreen();
                handled = true;
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    public static void OnFileActivated(FileActivatedEventArgs args)
    {
        if (args == null || Current == null || Current._isFirstStartUp)
        {
            s_startupFileArgs = args;
            return;
        }

        _ = OpenFileActivatedComic(args);
    }

    private static async Task<bool> OpenFileActivatedComic(FileActivatedEventArgs args)
    {
        ComicData comic = await Current.GetStartupComic(args);
        if (comic == null)
        {
            return false;
        }

        string token = AppData.PutComicData(comic);
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_TOKEN, token);
        Current.OpenInNewTab(route);
        return true;
    }

    // Fullscreen
    public void EnterFullscreen()
    {
        if (IsFullScreen())
        {
            return;
        }

        App.Window.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

        DispatchToAllTabs(delegate (MainPageAbility ability)
        {
            ability.SendFullscreenChangedEvent(true);
        });
    }

    public void ExitFullscreen()
    {
        if (!IsFullScreen())
        {
            return;
        }

        App.Window.AppWindow.SetPresenter(AppWindowPresenterKind.Default);

        DispatchToAllTabs(delegate (MainPageAbility ability)
        {
            ability.SendFullscreenChangedEvent(false);
        });
    }

    private void DispatchToTab(TabInfo tab, Action<MainPageAbility> action)
    {
        action(tab.Ability);
    }

    private void DispatchToAllTabs(Action<MainPageAbility> action)
    {
        foreach (TabInfo tab in _tabs)
        {
            DispatchToTab(tab, action);
        }
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsFullScreen())
        {
            ExitFullscreen();
        }
    }

    private bool IsFullScreen()
    {
        return App.Window.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
    }

    private static void RegisterPageAbility(PageCommunicator communicator, MainPageAbility ability)
    {
        communicator.RegisterAbility<ICommonPageAbility>(ability);
        communicator.RegisterAbility<IMainPageAbility>(ability);
    }

    private class MainPageAbility : ICommonPageAbility, IMainPageAbility
    {
        private const string EVENT_TAB_UNSELECTED = "TabUnselected";
        private const string EVENT_FULLSCREEN_CHANGED = "FullscreenChanged";

        private readonly WeakReference<MainPage> _parent;
        private readonly EventBus _eventBus = new();
        private readonly int _tabId;

        private PageStopEventHandler _pageStopped;

        public MainPageAbility(MainPage parent, int tabId)
        {
            _parent = new WeakReference<MainPage>(parent);
            _tabId = tabId;
        }

        public void RegisterPageStopHandler(PageStopEventHandler handler)
        {
            _pageStopped += handler;
        }

        public void UnregisterPageStopHandler(PageStopEventHandler handler)
        {
            _pageStopped -= handler;
        }

        public void DispatchPageStoppedEvent()
        {
            _pageStopped?.Invoke();
            _pageStopped = null;
        }

        public void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage parent))
            {
                return;
            }

            parent.LoadTab(_tabId, route);
        }

        public void SetTitle(string title)
        {
            TabInfo tab = GetTab();
            if (tab == null)
            {
                return;
            }

            tab.Item.Header = title;
        }

        public void SetIcon(IconSource icon)
        {
            TabInfo tab = GetTab();
            if (tab == null)
            {
                return;
            }

            tab.Item.IconSource = icon;
        }

        public void SetCurrentPageInfo(string url, IPageTrait pageTrait)
        {
            if (!_parent.TryGetTarget(out MainPage parent))
            {
                return;
            }

            TabInfo tab = parent.GetTabInfo(_tabId);
            if (tab == null)
            {
                return;
            }

            tab.CurrentPageTrait = pageTrait;
            tab.CurrentUrl = url;
            parent.OnPageChanged();
        }

        public void RegisterTabUnselectedHandler(Page owner, IMainPageAbility.TabUnselectedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_TAB_UNSELECTED).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendTabUnselectedEvent()
        {
            _eventBus.With<bool>(EVENT_TAB_UNSELECTED).Emit(true);
        }

        public void RegisterFullscreenChangedHandler(Page owner, IMainPageAbility.FullscreenChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).ObserveSticky(owner, delegate (bool isFullscreen)
            {
                handler(isFullscreen);
            });
        }

        public void SendFullscreenChangedEvent(bool isFullscreen)
        {
            _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).Emit(isFullscreen);
        }

        private TabInfo GetTab()
        {
            if (!_parent.TryGetTarget(out MainPage parent))
            {
                return null;
            }

            return parent.GetTabInfo(_tabId);
        }
    }

    private class TabInfo
    {
        public TabInfo(int id, TabViewItem item)
        {
            Id = id;
            Item = item;
        }

        public TabViewItem Item { get; }
        public int Id { get; }
        public MainPageAbility Ability { get; set; }
        public string CurrentUrl { get; set; }
        public IPageTrait CurrentPageTrait { get; set; }
    }
}
