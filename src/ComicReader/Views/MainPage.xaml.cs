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
using ComicReader.Database;

namespace ComicReader.Views
{
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;

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

    public sealed partial class MainPage : Page
    {
        public static MainPage Current = null;
        public MainPageShared Shared;

        private List<Common.Tab.TabIdentifier> m_all_tabs = new List<Common.Tab.TabIdentifier>();
        private Grid m_tab_container_grid;

        public MainPage()
        {
            Current = this;
            Shared = new MainPageShared();

            Shared.IsFullscreen = false;
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

        // File activation
        public SealedTask OnFileActivatedSealed(FileActivatedEventArgs args)
        {
            return delegate (Task<Utils.TaskResult> _t)
            {
                OnFileActivatedUnsealed(args).Wait();
                return new Utils.TaskResult();
            };
        }

        private async Task OnFileActivatedUnsealed(FileActivatedEventArgs args)
        {
            LockContext db = new LockContext();

            StorageFile target_file = (StorageFile)args.Files[0];

            if (!Common.AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
            {
                return;
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
                        var files = await args.NeighboringFilesQuery.GetFilesAsync();
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

            if (comic == null)
            {
                return;
            }

            await Utils.C0.Sync(delegate
            {
                LoadTab(null, Common.Tab.PageType.Reader, comic);
            });
        }

        // New tab
        private bool TrySwitchToTab(Common.Tab.PageType type, object args)
        {
            if (type != Common.Tab.PageType.Reader &&
                type != Common.Tab.PageType.Settings &&
                type != Common.Tab.PageType.Help)
            {
                return false;
            }

            string unique_string = Common.Tab.TabManager.PageUniqueString(type, args);

            foreach (Common.Tab.TabIdentifier tab in m_all_tabs)
            {
                if (unique_string == tab.UniqueString)
                {
                    RootTabView.SelectedItem = tab.Tab;
                    return true;
                }
            }

            return false;
        }

        private Common.Tab.TabIdentifier AddNewTab(Common.Tab.PageType type, object args = null)
        {
            ExitFullscreen();

            // create a new tab and switch to it.
            muxc.TabViewItem new_tab = new muxc.TabViewItem();
            new_tab.Header = "Loading...";
            new_tab.Content = new Frame();
            RootTabView.TabItems.Add(new_tab);
            RootTabView.SelectedItem = new_tab;

            Common.Tab.TabIdentifier id = new Common.Tab.TabIdentifier
            {
                Tab = new_tab,
                Type = type,
                RequestArgs = args,
            };

            m_all_tabs.Add(id);

            // remember tab content are not loaded at this moment, further process
            // is required.
            return id;
        }

        public void LoadTab(Common.Tab.TabIdentifier tab_id, Common.Tab.PageType type, object args = null,
            bool try_reuse = true)
        {
            if (try_reuse)
            {
                // switch to an existed tab if possible
                if (TrySwitchToTab(type, args))
                {
                    return;
                }
            }

            if (tab_id == null)
            {
                // if no tab id provided, create one.
                tab_id = AddNewTab(type, args);
            }
            else
            {
                tab_id.Type = type;
                tab_id.RequestArgs = args;
                tab_id.OnTabSelected = null;
            }

            Common.Tab.NavigationParams nav_params = new Common.Tab.NavigationParams
            {
                Shared = Shared,
                TabId = tab_id
            };

            Frame frame = (Frame)tab_id.Tab.Content;

            // use different loading strategies based on page type.
            if (type == Common.Tab.PageType.Reader || type == Common.Tab.PageType.Home ||
                type == Common.Tab.PageType.Search)
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
                content_page.Update();
            }
            else
            {
                // these pages are based on MainPage.
                frame.Navigate(Common.Tab.TabManager.TypeFromPageTypeEnum(type), nav_params);
            }
        }

        // TabView
        private void OnAddTabButtonClicked(muxc.TabView sender, object args)
        {
            LoadTab(null, Common.Tab.PageType.Home);
        }

        private Common.Tab.TabIdentifier GetTabId(muxc.TabViewItem tab)
        {
            foreach (Common.Tab.TabIdentifier id in m_all_tabs)
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
            for (int i = 0; i < m_all_tabs.Count; ++i)
            {
                Common.Tab.TabIdentifier tab_id = m_all_tabs[i];

                if (tab_id.Tab == args.Tab)
                {
                    m_all_tabs.RemoveAt(i);
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
            LoadTab(null, Common.Tab.PageType.Home);
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
            Common.Tab.TabIdentifier id = GetTabId(tab);

            if (id == null)
            {
                return;
            }

            UpdateFullscreenMode();
            id.OnTabSelected?.Invoke();
        }

        private void SetTabViewVisibility(bool visibility)
        {
            if (m_tab_container_grid == null)
            {
                return;
            }

            m_tab_container_grid.Visibility = visibility ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void OnTabContainerGridLoaded(object sender, RoutedEventArgs e)
        {
            m_tab_container_grid = sender as Grid;
        }

        // Fullscreen
        public void UpdateFullscreenMode()
        {
            // make IsFullscreen consistent with the actual state.
            if (!ApplicationView.GetForCurrentView().IsFullScreenMode)
            {
                ExitFullscreen();
            }
        }

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
            UpdateFullscreenMode();
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
            bool handled = true;

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
    }
}
