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
using ComicReader.Data;

namespace ComicReader.Views
{
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;

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
                
                if (m_IsFullscreen == false)
                {
                    OnExitFullscreenMode?.Invoke();
                }
            }
        }

        public Action OnExitFullscreenMode;
    }

    public sealed partial class MainPage : Page
    {
        public static MainPage Current = null;
        public MainPageShared Shared;

        private List<Utils.Tab.TabIdentifier> m_all_tabs = new List<Utils.Tab.TabIdentifier>();
        private Grid m_tab_container_grid;

        public MainPage()
        {
            Current = this;
            Shared = new MainPageShared();
            Shared.IsFullscreen = false;

            InitializeComponent();
        }

        // file activation
        public SealedTask OnFileActivatedSealed(FileActivatedEventArgs args)
        {
            return delegate (Task<Utils.TaskQueue.TaskResult> _t)
            {
                Task task = OnFileActivatedAsync(args);
                task.Wait();
                return new Utils.TaskQueue.TaskResult();
            };
        }

        private async Task OnFileActivatedAsync(FileActivatedEventArgs args)
        {
            string dir = args.Files[0].Path;

            for (int p = dir.Length - 1; p >= 0; --p)
            {
                if (dir[p] == '\\')
                {
                    dir = dir.Substring(0, p);
                    break;
                }
            }

            ComicItemData comic = await ComicDataManager.FromDirectory(dir);

            if (comic == null)
            {
                comic = new ComicItemData();
                comic.IsExternal = true;
                comic.Directory = dir;
                List<StorageFile> all_files = new List<StorageFile>();
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
                    if (file.Name.ToLower().Equals("info.txt"))
                    {
                        comic.InfoFile = file;
                        await ComicDataManager.UpdateInfo(comic);
                    }
                    else if (file.FileType == ".jpg" || file.FileType == ".jpeg" ||
                        file.FileType == ".png" || file.FileType == ".bmp")
                    {
                        comic.ImageFiles.Add(file);
                    }
                }

                all_files.OrderBy(x => x.DisplayName,
                    new Utils.StringUtils.FileNameComparer());
            }

            await Utils.Methods.Sync(delegate
            {
                LoadTab(null, Utils.Tab.PageType.Reader, comic);
            });
        }

        // new tab
        private bool TrySwitchToTab(Utils.Tab.PageType type, object args)
        {
            if (type != Utils.Tab.PageType.Reader &&
                type != Utils.Tab.PageType.Settings &&
                type != Utils.Tab.PageType.Help)
            {
                return false;
            }

            string unique_string = Utils.Tab.TabManager.PageUniqueString(type, args);

            foreach (Utils.Tab.TabIdentifier tab in m_all_tabs)
            {
                if (unique_string == tab.UniqueString)
                {
                    RootTabView.SelectedItem = tab.Tab;
                    return true;
                }
            }

            return false;
        }

        private Utils.Tab.TabIdentifier AddNewTab(Utils.Tab.PageType type, object args = null)
        {
            ExitFullscreen();

            // create a new tab and switch to it.
            muxc.TabViewItem new_tab = new muxc.TabViewItem();
            new_tab.Header = "Loading...";
            new_tab.Content = new Frame();
            RootTabView.TabItems.Add(new_tab);
            RootTabView.SelectedItem = new_tab;

            Utils.Tab.TabIdentifier id = new Utils.Tab.TabIdentifier
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

        public void LoadTab(Utils.Tab.TabIdentifier tab_id, Utils.Tab.PageType type, object args = null,
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

            Utils.Tab.NavigationParams nav_params = new Utils.Tab.NavigationParams
            {
                Shared = Shared,
                TabId = tab_id
            };

            Frame frame = (Frame)tab_id.Tab.Content;

            // use different loading strategies based on page type.
            if (type == Utils.Tab.PageType.Reader || type == Utils.Tab.PageType.Home ||
                type == Utils.Tab.PageType.Search)
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
                frame.Navigate(Utils.Tab.TabManager.TypeFromPageTypeEnum(type), nav_params);
            }
        }

        // tabview
        private void OnAddTabButtonClicked(muxc.TabView sender, object args)
        {
            LoadTab(null, Utils.Tab.PageType.Home);
        }

        private Utils.Tab.TabIdentifier GetTabId(muxc.TabViewItem tab)
        {
            foreach (Utils.Tab.TabIdentifier id in m_all_tabs)
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
                Utils.Tab.TabIdentifier tab_id = m_all_tabs[i];

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
            LoadTab(null, Utils.Tab.PageType.Home);
        }

        // background tasks indication
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
            Utils.Tab.TabIdentifier id = GetTabId(tab);

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

        // fullscreen
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

        public void ExitFullscreen()
        {
            if (!Shared.IsFullscreen)
            {
                return;
            }

            ApplicationView.GetForCurrentView().ExitFullScreenMode();
            Shared.IsFullscreen = false;
            SetTabViewVisibility(true);
        }

        private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateFullscreenMode();
        }

        private void OnMainPageKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ExitFullscreen();
            }
        }
    }
}
