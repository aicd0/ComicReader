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

namespace ComicReader.Views
{
    public class MainPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool m_IsFullscreen;
        public bool IsFullscreen
        {
            get => m_IsFullscreen;
            set
            {
                m_IsFullscreen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFullscreen"));
            }
        }

        public DesignData.ReaderSettingViewModel ReaderSettings = new DesignData.ReaderSettingViewModel();
    }

    sealed internal partial class MainPage : StatefulPage
    {
        public static MainPage Current = null;
        private static FileActivatedEventArgs s_startupFileArgs;

        public MainPageShared Shared;

        private readonly List<TabIdentifier> _allTabs = new List<TabIdentifier>();
        private Grid _tabContainerGrid;

        public MainPage()
        {
            Current = this;

            Shared = new MainPageShared
            {
                IsFullscreen = false
            };

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

            NavigationManager.GetInstance().OnExitFullscreen += delegate
            {
                ExitFullscreen();
            };

            InitializeComponent();
        }

        // File activation
        private async Task<ComicData> GetStartupComic(LockContext db, FileActivatedEventArgs args)
        {
            StorageFile target_file = (StorageFile)args.Files[0];

            if (!Common.AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
            {
                return null;
            }

            ComicData comic = null;

            if (Common.AppInfoProvider.IsSupportedDocumentExtension(target_file.FileType))
            {
                comic = await ComicData.Manager.FromLocation(db, target_file.Path);

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
                comic = await ComicData.Manager.FromLocation(db, target_file.Path);

                if (comic == null)
                {
                    comic = await ComicArchiveData.FromExternal(target_file);
                }
            }
            else if (Common.AppInfoProvider.IsSupportedImageExtension(target_file.FileType))
            {
                string dir = target_file.Path;
                dir = Utils.StringUtils.ParentLocationFromLocation(dir);
                comic = await ComicData.Manager.FromLocation(db, dir);

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
                        if (file.Name.ToLower().Equals(ComicData.Manager.ComicInfoFileName))
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
        private bool TrySwitchToTab(PageType type, object args)
        {
            foreach (TabIdentifier tab in _allTabs)
            {
                if (tab.pageType != type)
                {
                    continue;
                }

                if (!tab.listener.AllowJump())
                {
                    continue;
                }

                if (tab.listener.GetUniqueString(args) == tab.listener.GetUniqueString(tab.RequestArgs))
                {
                    RootTabView.SelectedItem = tab.Tab;
                    return true;
                }
            }
            return false;
        }

        private TabIdentifier AddNewTab(PageType type, object args = null)
        {
            // create a new tab and switch to it.
            muxc.TabViewItem new_tab = new muxc.TabViewItem
            {
                Header = "Loading...",
                Content = new Frame()
            };
            RootTabView.TabItems.Add(new_tab);
            RootTabView.SelectedItem = new_tab;

            TabIdentifier id = new TabIdentifier
            {
                Tab = new_tab,
                pageType = type,
                RequestArgs = args,
            };

            _allTabs.Add(id);

            // remember tab content are not loaded at this moment, further process
            // is required.
            return id;
        }

        public void LoadTab(TabIdentifier tab_id, PageType type, object args = null, bool try_reuse = true)
        {
            // switch to an existed tab if possible
            if (try_reuse)
            {
                if (TrySwitchToTab(type, args))
                {
                    return;
                }
            }

            // if no tab id provided, create one.
            if (tab_id == null)
            {
                tab_id = AddNewTab(type, args);
            }
            else
            {
                tab_id.pageType = type;
                tab_id.RequestArgs = args;
                tab_id.listener = null;
            }

            NavigationParams nav_params = new NavigationParams
            {
                shared = Shared,
                tabId = tab_id
            };

            Frame frame = (Frame)tab_id.Tab.Content;

            // use different loading strategies based on page type.
            if (type == PageType.Reader || type == PageType.Home || type == PageType.Search)
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
                frame.Navigate(PageTypeUtils.PageTypeToType(type), nav_params);
            }
        }

        // TabView
        private void OnAddTabButtonClicked(muxc.TabView sender, object args)
        {
            LoadTab(null, PageType.Home, try_reuse: false);
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

        private void OnTabViewLoaded(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                var db = new LockContext();
                if (s_startupFileArgs != null)
                {
                    ComicData comic = await GetStartupComic(db, s_startupFileArgs);
                    if (comic != null)
                    {
                        LoadTab(null, PageType.Reader, comic);
                        return;
                    }
                }
                LoadTab(null, PageType.Home);
            });
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
            TabIdentifier id = GetTabId(tab);
            if (id == null)
            {
                return;
            }
            id.OnSelected();
        }

        private void SetTabViewVisibility(bool visibility)
        {
            if (_tabContainerGrid == null)
            {
                return;
            }

            _tabContainerGrid.Visibility = visibility ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void OnTabContainerGridLoaded(object sender, RoutedEventArgs e)
        {
            _tabContainerGrid = sender as Grid;
        }

        // Fullscreen
        public bool EnterFullscreen()
        {
            if (Shared.IsFullscreen)
            {
                return true;
            }

            if (!ApplicationView.GetForCurrentView().TryEnterFullScreenMode())
            {
                return false;
            }

            Shared.IsFullscreen = true;
            SetTabViewVisibility(false);
            return true;
        }

        public bool ExitFullscreen()
        {
            if (!Shared.IsFullscreen)
            {
                return false;
            }

            ApplicationView.GetForCurrentView().ExitFullScreenMode();
            Shared.IsFullscreen = false;
            SetTabViewVisibility(true);
            return true;
        }

        private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // make IsFullscreen consistent with the actual state.
            if (!ApplicationView.GetForCurrentView().IsFullScreenMode)
            {
                ExitFullscreen();
            }
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
                case Windows.System.VirtualKey.Escape:
                    handled = ExitFullscreen();
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

        public static async Task OnFileActivated(LockContext db, FileActivatedEventArgs args)
        {
            if (args == null || Current == null || Current.RootTabView == null || !Current.RootTabView.IsLoaded)
            {
                s_startupFileArgs = args;
                return;
            }
            ComicData comic = await Current.GetStartupComic(db, args);
            if (comic != null)
            {
                Current.LoadTab(null, PageType.Reader, comic);
            }
        }
    }
}
