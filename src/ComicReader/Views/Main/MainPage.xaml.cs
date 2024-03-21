using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Database;
using ComicReader.Router;
using ComicReader.Utils;
using ComicReader.Views.Base;
using ComicReader.Views.Navigation;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;

namespace ComicReader.Views.Main
{
    sealed internal partial class MainPage : StatefulPage
    {
        public static MainPage Current = null;
        private static FileActivatedEventArgs s_startupFileArgs;

        private readonly List<TabInfo> _tabs = new List<TabInfo>();
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
            _ = OpenStartupTab();
        }

        private async Task OpenStartupTab()
        {
            bool page_started = false;

            if (s_startupFileArgs != null)
            {
                ComicData comic = await GetStartupComic(s_startupFileArgs);
                if (comic != null)
                {
                    Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    OpenInNewTab(route);
                    page_started = true;
                }
            }

            if (!page_started)
            {
                long id = AppStatusPreserver.GetReadingComic();
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
            NavigationBundle bundle = route.Process();
            LoadTab(-1, bundle);
        }

        public bool TrySwitchToTab(string url)
        {
            foreach (TabInfo tab in _tabs)
            {
                if (tab.CurrentBundle.Url == url)
                {
                    RootTabView.SelectedItem = tab.Item;
                    return true;
                }
            }

            return false;
        }

        public void ShowOrHideTitleBar(bool show)
        {
            if (_currentTab == null || !_currentTab.CurrentBundle.PageTrait.ImmersiveMode())
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
                        if (file.Name.ToLower().Equals(ComicData.ComicInfoFileName))
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
            tabInfo.CurrentBundle = bundle;
            _tabs.Add(tabInfo);
            RootTabView.TabItems.Add(item);
            RootTabView.SelectedItem = item;
            return tabId;
        }

        private void LoadTab(int tabId, NavigationBundle bundle)
        {
            if (tabId < -1)
            {
                throw new ArgumentException();
            }

            if (tabId == -1)
            {
                tabId = AddTab(bundle);
            }

            TabInfo tabInfo = GetTabInfo(tabId);
            var frame = (Frame)tabInfo.Item.Content;
            bundle.Abilities[typeof(IMainPageAbility)] = new MainPageAbility(this, tabId);
            if (bundle.PageTrait.HasNavigationBar())
            {
                if (frame.Content == null || frame.Content.GetType() != typeof(NavigationPage))
                {
                    var route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_NAVIGATION);
                    NavigationBundle navigationPageBundle = route.Process();
                    navigationPageBundle.Abilities[typeof(IMainPageAbility)] = new MainPageAbility(this, tabId);
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
            for (int i = 0; i < _tabs.Count; ++i)
            {
                TabInfo tabInfo = _tabs[i];

                if (tabInfo.Item == args.Tab)
                {
                    _tabs.RemoveAt(i);
                    break;
                }
            }

            RootTabView.TabItems.Remove(args.Tab);

            if (sender.TabItems.Count == 0)
            {
                Application.Current.Exit();
            }
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
            System.Diagnostics.Debug.Assert(newSelectedTab != null);
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
                IPageTrait pageTrait = currentTab.CurrentBundle.PageTrait;

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

            if (_currentTab.CurrentBundle.PageTrait.ImmersiveMode())
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
            _ = C0.Sync(async delegate
            {
                if (args == null || Current == null || Current.RootTabView == null || !Current.RootTabView.IsLoaded)
                {
                    s_startupFileArgs = args;
                    return;
                }

                ComicData comic = await Current.GetStartupComic(args);
                if (comic != null)
                {
                    Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    Current.OpenInNewTab(route);
                }
            });
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
            if (tab.CurrentBundle.Abilities.TryGetValue(typeof(IMainPageAbility), out IPageAbility ability))
            {
                action(ability as MainPageAbility);
            }
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

        private class MainPageAbility : IMainPageAbility
        {
            private const string EVENT_TAB_UNSELECTED = "TabUnselected";
            private const string EVENT_FULLSCREEN_CHANGED = "FullscreenChanged";

            private WeakReference<MainPage> _parent;
            private EventBus _eventBus = new EventBus();
            private int _tabId;

            public MainPageAbility(MainPage parent, int tabId)
            {
                _parent = new WeakReference<MainPage>(parent);
                _tabId = tabId;
            }

            public void OpenInCurrentTab(Route route)
            {
                if (!_parent.TryGetTarget(out MainPage parent))
                    return;
                NavigationBundle bundle = route.Process();
                parent.LoadTab(_tabId, bundle);
            }

            public void SetIcon(IconSource icon)
            {
                if (!_parent.TryGetTarget(out MainPage parent))
                    return;
                parent.GetTabInfo(_tabId).Item.IconSource = icon;
            }

            public void SetNavigationBundle(NavigationBundle bundle)
            {
                if (!_parent.TryGetTarget(out MainPage parent))
                    return;
                parent.GetTabInfo(_tabId).CurrentBundle = bundle;
                parent.OnPageChanged();
            }

            public void SetTitle(string title)
            {
                if (!_parent.TryGetTarget(out MainPage parent))
                    return;
                parent.GetTabInfo(_tabId).Item.Header = title;
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
                _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).Observe(owner, delegate (bool isFullscreen)
                {
                    handler(isFullscreen);
                });
            }

            public void SendFullscreenChangedEvent(bool isFullscreen)
            {
                _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).Emit(isFullscreen);
            }
        }
    }
}
