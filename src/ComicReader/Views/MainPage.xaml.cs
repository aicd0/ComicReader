using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Common.Router;
using ComicReader.Database;
using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Utils;
using Windows.UI.Composition;
using SharpCompress.Common;

namespace ComicReader.Views
{
    public class MainPageShared
    {
        public DesignData.ReaderSettingViewModel ReaderSettings = new DesignData.ReaderSettingViewModel();
    }

    sealed internal partial class MainPage : StatefulPage
    {
        public static MainPage Current = null;
        private static FileActivatedEventArgs s_startupFileArgs;

        public MainPageShared Shared;

        private readonly List<TabIdentifier> _allTabs = new List<TabIdentifier>();
        private TabIdentifier _currentTab;
        private Grid _tabContainerGrid;
        private ContentPresenter _tabContentPresenter;
        private double _rootTabHeight = 0;
        private double _navigationBarHeight = 0;
        private Utils.KeyFrameAnimation _titleBarAnimation;

        public MainPage()
        {
            Current = this;

            Shared = new MainPageShared();
            Shared.ReaderSettings.IsVertical = Database.XmlDatabase.Settings.VerticalReading;
            Shared.ReaderSettings.IsLeftToRight = Database.XmlDatabase.Settings.LeftToRight;
            Shared.ReaderSettings.IsVerticalContinuous = Database.XmlDatabase.Settings.VerticalContinuous;
            Shared.ReaderSettings.IsHorizontalContinuous = Database.XmlDatabase.Settings.HorizontalContinuous;
            Shared.ReaderSettings.VerticalPageArrangement = Database.XmlDatabase.Settings.VerticalPageArrangement;
            Shared.ReaderSettings.HorizontalPageArrangement = Database.XmlDatabase.Settings.HorizontalPageArrangement;
            Shared.ReaderSettings.OnVerticalChanged += SaveReaderSettingsEventSealed;
            Shared.ReaderSettings.OnFlowDirectionChanged += SaveReaderSettingsEventSealed;
            Shared.ReaderSettings.OnVerticalContinuousChanged += SaveReaderSettingsEventSealed;
            Shared.ReaderSettings.OnHorizontalContinuousChanged += SaveReaderSettingsEventSealed;
            Shared.ReaderSettings.OnVerticalPageArrangementChanged += SaveReaderSettingsEventSealed;
            Shared.ReaderSettings.OnHorizontalPageArrangementChanged += SaveReaderSettingsEventSealed;

            InitializeComponent();
        }

        public override void OnLoaded(object sender, RoutedEventArgs e)
        {
            base.OnLoaded(sender, e);

            Utils.C0.Run(async delegate
            {
                if (s_startupFileArgs != null)
                {
                    ComicData comic = await GetStartupComic(s_startupFileArgs);
                    if (comic != null)
                    {
                        LoadTab(null, ReaderPageTrait.Instance, comic);
                        return;
                    }
                }
                LoadTab(null, HomePageTrait.Instance);
            });

            EventBus.Instance.With<double>(EventId.RootTabHeightChange).Observe(this, delegate (double h)
            {
                _rootTabHeight = h;
                EventBus.Instance.With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
                UpdateTopPadding();
            }, true);

            EventBus.Instance.With<double>(EventId.NavigationBarHeightChange).Observe(this, delegate (double h)
            {
                _navigationBarHeight = h;
                EventBus.Instance.With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
            }, true);

            EventBus.Instance.With<double>(EventId.TitleBarOpacity).Observe(this, delegate (double opacity)
            {
                if (_tabContainerGrid != null)
                {
                    _tabContainerGrid.Opacity = opacity;
                    _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
                }
            }, true);
        }

        // File activation
        private async Task<ComicData> GetStartupComic(FileActivatedEventArgs args)
        {
            StorageFile target_file = (StorageFile)args.Files[0];

            if (!Common.AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
            {
                return null;
            }

            ComicData comic = null;

            if (Common.AppInfoProvider.IsSupportedDocumentExtension(target_file.FileType))
            {
                comic = await ComicData.FromLocation(target_file.Path);

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
            else if (Common.AppInfoProvider.IsSupportedArchiveExtension(target_file.FileType))
            {
                comic = await ComicData.FromLocation(target_file.Path);

                if (comic == null)
                {
                    comic = await ComicArchiveData.FromExternal(target_file);
                }
            }
            else if (Common.AppInfoProvider.IsSupportedImageExtension(target_file.FileType))
            {
                string dir = target_file.Path;
                dir = Utils.StringUtils.ParentLocationFromLocation(dir);
                comic = await ComicData.FromLocation(dir);

                if (comic == null)
                {
                    StorageFile info_file = null;
                    List<StorageFile> all_files = new List<StorageFile>();
                    List<StorageFile> img_files = new List<StorageFile>();
                    StorageFileQueryResult neighboring_file_query =
                        args.NeighboringFilesQuery;

                    if (neighboring_file_query != null)
                    {
                        IReadOnlyList<StorageFile> files = await args.NeighboringFilesQuery.GetFilesAsync();
                        all_files = files.ToList();
                    }
                    else
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
                        else if (Common.AppInfoProvider.IsSupportedImageExtension(file.FileType))
                        {
                            img_files.Add(file);
                        }
                    }

                    comic = await ComicFolderData.FromExternal(dir, img_files, info_file);
                }
            }
            return comic;
        }

        // New tab
        private bool TrySwitchToTab(IPageTrait pageTrait, object args)
        {
            foreach (TabIdentifier tab in _allTabs)
            {
                if (tab.PageTrait.GetPageType() != pageTrait.GetPageType())
                {
                    continue;
                }

                if (!tab.TabListener.AllowJump())
                {
                    continue;
                }

                if (tab.TabListener.GetUniqueString(args) == tab.TabListener.GetUniqueString(tab.RequestArgs))
                {
                    RootTabView.SelectedItem = tab.Tab;
                    return true;
                }
            }
            return false;
        }

        private TabIdentifier AddNewTab(IPageTrait pageTrait, object args = null)
        {
            // create a new tab and switch to it.
            muxc.TabViewItem new_tab = new muxc.TabViewItem
            {
                Header = "Loading...",
                Content = new Frame()
            };
            TabIdentifier id = new TabIdentifier
            {
                Tab = new_tab,
                PageTrait = pageTrait,
                RequestArgs = args,
            };
            id.PageTraitChanged += OnPageChanged;
            _allTabs.Add(id);
            RootTabView.TabItems.Add(new_tab);
            RootTabView.SelectedItem = new_tab;
            // remember tab content are not loaded at this moment, further process
            // is required.
            return id;
        }

        public void LoadTab(TabIdentifier tab_id, IPageTrait pageTrait, object args = null, bool try_reuse = true)
        {
            // switch to an existed tab if possible
            if (try_reuse && TrySwitchToTab(pageTrait, args))
            {
                return;
            }
            if (tab_id == null)
            {
                tab_id = AddNewTab(pageTrait, args);
            }
            else
            {
                tab_id.PageTrait = pageTrait;
                tab_id.RequestArgs = args;
                tab_id.TabListener = null;
            }
            _currentTab = tab_id;

            NavigationParams nav_params = new NavigationParams
            {
                Params = Shared,
                TabId = tab_id
            };

            Frame frame = (Frame)tab_id.Tab.Content;

            // use different loading strategies based on page type.
            if (pageTrait.HasNavigationBar())
            {
                // these pages are based on NavigationPage.
                if (frame.Content == null || frame.Content.GetType() != typeof(NavigationPage))
                {
                    // navigate to NavigationPage first.
                    if (!frame.Navigate(typeof(NavigationPage), nav_params))
                    {
                        return;
                    }
                }
                NavigationPage content_page = (NavigationPage)frame.Content;
                content_page.Navigate();
            }
            else
            {
                // these pages are based on MainPage.
                frame.Navigate(pageTrait.GetPageType(), nav_params);
            }
        }

        // TabView
        private void OnAddTabButtonClicked(muxc.TabView sender, object args)
        {
            LoadTab(null, HomePageTrait.Instance, try_reuse: false);
        }

        private TabIdentifier GetTabId(muxc.TabViewItem tab)
        {
            foreach (TabIdentifier id in _allTabs)
            {
                if (id.Tab == tab)
                {
                    return id;
                }
            }
            return null;
        }

        private void OnTabCloseRequested(muxc.TabView sender,
            muxc.TabViewTabCloseRequestedEventArgs args)
        {
            for (int i = 0; i < _allTabs.Count; ++i)
            {
                TabIdentifier tab_id = _allTabs[i];

                if (tab_id.Tab == args.Tab)
                {
                    _allTabs.RemoveAt(i);
                    break;
                }
            }

            RootTabView.TabItems.Remove(args.Tab);

            if (sender.TabItems.Count == 0)
            {
                CoreApplication.Exit();
            }
        }

        // Background tasks
        public void SetRootToolTip(string text)
        {
            if (RootToolTip == null)
            {
                return;
            }

            if (text.Length == 0)
            {
                RootToolTip.Visibility = Visibility.Collapsed;
            }
            else
            {
                RootToolTip.Content = text;
                RootToolTip.Visibility = Visibility.Visible;
            }
        }

        private void OnTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                return;
            }
            muxc.TabViewItem tab = (muxc.TabViewItem)e.AddedItems[0];
            _currentTab = GetTabId(tab);
            System.Diagnostics.Debug.Assert(_currentTab != null);
            if (_currentTab != null)
            {
                OnPageChanged(_currentTab.PageTrait);
                _currentTab.OnSelected();
            }
        }

        private void OnPageChanged(IPageTrait pageTrait)
        {
            UpdateTopPadding();
            if (!pageTrait.ImmersiveMode())
            {
                _titleBarAnimation?.Stop();
                EventBus.Instance.With<double>(EventId.TitleBarOpacity).Emit(1.0);
            }
        }

        private void UpdateTopPadding()
        {
            if (_currentTab == null || _tabContentPresenter == null)
            {
                return;
            }
            if (_currentTab.PageTrait.ImmersiveMode())
            {
                _tabContentPresenter.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                _tabContentPresenter.Margin = new Thickness(0, _rootTabHeight, 0, 0);
            }
        }

        public void ShowOrHideTitleBar(bool show)
        {
            if (_currentTab == null || !_currentTab.PageTrait.ImmersiveMode())
            {
                return;
            }
            if (_titleBarAnimation == null)
            {
                _titleBarAnimation = new Utils.KeyFrameAnimation
                {
                    Duration = 0.2,
                    UpdateCallback = delegate (double value)
                    {
                        EventBus.Instance.With<double>(EventId.TitleBarOpacity).Emit(value);
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
            EventBus.Instance.With<double>(EventId.RootTabHeightChange).Emit(e.NewSize.Height);
        }

        // Reader settings
        private void SaveReaderSettingsEventSealed()
        {
            Utils.C0.Run(async delegate
            {
                await XmlDatabaseManager.WaitLock();

                Database.XmlDatabase.Settings.VerticalReading = Shared.ReaderSettings.IsVertical;
                Database.XmlDatabase.Settings.LeftToRight = Shared.ReaderSettings.IsLeftToRight;
                Database.XmlDatabase.Settings.VerticalContinuous = Shared.ReaderSettings.IsVerticalContinuous;
                Database.XmlDatabase.Settings.HorizontalContinuous = Shared.ReaderSettings.IsHorizontalContinuous;
                Database.XmlDatabase.Settings.VerticalPageArrangement = Shared.ReaderSettings.VerticalPageArrangement;
                Database.XmlDatabase.Settings.HorizontalPageArrangement = Shared.ReaderSettings.HorizontalPageArrangement;

                XmlDatabaseManager.ReleaseLock();
                Utils.TaskQueueManager.AppendTask(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Settings));
            });
        }

        // Keys
        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool handled;
            switch (e.Key)
            {
                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                e.Handled = true;
            }
        }

        public static async Task OnFileActivated(FileActivatedEventArgs args)
        {
            if (args == null || Current == null || Current.RootTabView == null || !Current.RootTabView.IsLoaded)
            {
                s_startupFileArgs = args;
                return;
            }
            ComicData comic = await Current.GetStartupComic(args);
            if (comic != null)
            {
                Current.LoadTab(null, ReaderPageTrait.Instance, comic);
            }
        }
    }
}
