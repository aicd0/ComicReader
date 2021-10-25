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
    using SealedTask =
        Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;

    public class RootPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public PageType CurrentPageType
        {
            set
            {
                IsReaderPage = value == PageType.Reader;
                IsFullscreenButtonVisible = IsReaderPage;

                if (!IsReaderPage && IsFullscreen)
                {
                    IsFullscreen = false;
                }
            }
        }

        private bool m_IsReaderPage;
        public bool IsReaderPage
        {
            get => m_IsReaderPage;
            set
            {
                m_IsReaderPage = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs("IsReaderPage"));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs("IsReaderPageN"));
            }
        }
        public bool IsReaderPageN => !IsReaderPage;

        private bool m_IsFullscreen;
        public bool IsFullscreen
        {
            get => m_IsFullscreen;
            set
            {
                m_IsFullscreen = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs("IsFullscreen"));
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs("IsFullscreenN"));
                
                if (m_IsFullscreen == false)
                {
                    OnExitFullscreenMode?.Invoke();
                }
            }
        }
        public bool IsFullscreenN => !m_IsFullscreen;

        private bool m_IsFullscreenButtonVisible;
        public bool IsFullscreenButtonVisible
        {
            get => m_IsFullscreenButtonVisible;
            set
            {
                m_IsFullscreenButtonVisible = value;
                PropertyChanged?.Invoke(this,
                    new PropertyChangedEventArgs("IsFullscreenButtonVisible"));
            }
        }

        public Action OnExitFullscreenMode;
    }

    public sealed partial class RootPage : Page
    {
        public static RootPage Current = null;
        public RootPageShared Shared;

        private List<TabIdentifier> m_all_tabs = new List<TabIdentifier>();
        private Grid m_tab_container_grid;

        public RootPage()
        {
            Current = this;
            Shared = new RootPageShared();
            Shared.IsFullscreen = false;
            Shared.IsFullscreenButtonVisible = false;

            InitializeComponent();

            Window.Current.SetTitleBar(TitleBarArea);
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar title_bar =
                ApplicationView.GetForCurrentView().TitleBar;
            title_bar.BackgroundColor = Windows.UI.Colors.Transparent;
            title_bar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
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

            ComicItemData comic = await DataManager.GetComicWithDirectory(dir);

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
                        await DataManager.UpdateComicInfo(comic);
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
                LoadTab(null, PageType.Reader, comic);
            });
        }

        // new tab
        private bool TrySwitchToTab(PageType type, object args)
        {
            string unique_string = PageUtils.GetPageUniqueString(type, args);

            foreach (TabIdentifier tab in m_all_tabs)
            {
                if (unique_string == tab.UniqueString)
                {
                    RootTabView.SelectedItem = tab.Tab;
                    return true;
                }
            }

            return false;
        }

        private TabIdentifier AddNewTab(PageType type, object args = null)
        {
            ExitFullscreen();

            // create a new tab and switch to it.
            muxc.TabViewItem new_tab = new muxc.TabViewItem();
            new_tab.Header = "Loading...";
            new_tab.Content = new Frame();
            RootTabView.TabItems.Add(new_tab);
            RootTabView.SelectedItem = new_tab;

            TabIdentifier id = new TabIdentifier
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

        public void LoadTab(TabIdentifier tab_id, PageType type, object args = null,
            bool try_reuse = true)
        {
            if ((type == PageType.Reader || type == PageType.Settings) && try_reuse)
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

            NavigationParams nav_params = new NavigationParams
            {
                Shared = Shared,
                TabId = tab_id
            };

            Frame frame = (Frame)tab_id.Tab.Content;

            // use different loading strategies based on page type.
            if (type == PageType.Reader || type == PageType.Home ||
                type == PageType.Search)
            {
                // these pages are based on ContentPage.
                if (frame.Content == null || frame.Content.GetType() != typeof(ContentPage))
                {
                    // navigate to ContentPage first.
                    if (!frame.Navigate(typeof(ContentPage), nav_params))
                    {
                        return;
                    }
                }

                ContentPage content_page = (ContentPage)frame.Content;
                content_page.Update();
            }
            else
            {
                // these pages are based on RootPage.
                frame.Navigate(PageUtils.GetPageType(type), nav_params);
            }
        }

        // tabview
        private void TabView_AddTabButtonClick(muxc.TabView sender, object args)
        {
            LoadTab(null, PageType.Home);
        }

        private TabIdentifier GetTabIdentifier(muxc.TabViewItem tab)
        {
            foreach (TabIdentifier id in m_all_tabs)
            {
                if (id.Tab == tab)
                {
                    return id;
                }
            }

            return null;
        }

        private void TabView_TabCloseRequested(muxc.TabView sender,
            muxc.TabViewTabCloseRequestedEventArgs args)
        {
            for (int i = 0; i < m_all_tabs.Count; ++i)
            {
                TabIdentifier tab_id = m_all_tabs[i];

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

        private void TabView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTab(null, PageType.Home);
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

        private void TabView_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            foreach (muxc.TabViewItem tab in e.AddedItems)
            {
                TabIdentifier id = GetTabIdentifier(tab);

                if (id == null)
                {
                    continue;
                }

                UpdateFullscreenMode(id.Type);
                id.OnTabSelected?.Invoke();
            }
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

        private void TabContainerGrid_Loaded(object sender, RoutedEventArgs e)
        {
            m_tab_container_grid = sender as Grid;
        }

        // fullscreen
        public void UpdateFullscreenMode(PageType type)
        {
            // exit fullscreen mode if necessary
            if (type != PageType.Unknown)
            {
                Shared.IsFullscreenButtonVisible = type == PageType.Reader;
            }

            if (Shared.IsFullscreen)
            {
                if (!Shared.IsFullscreenButtonVisible ||
                    !ApplicationView.GetForCurrentView().IsFullScreenMode)
                {
                    ExitFullscreen();
                }
            }
        }

        public bool EnterFullscreen()
        {
            if (Shared.IsFullscreen)
            {
                return true;
            }

            ApplicationView view = ApplicationView.GetForCurrentView();
            if (!view.TryEnterFullScreenMode())
            {
                return false;
            }

            Shared.IsFullscreen = true;
            SetTabViewVisibility(false);

            return true;
        }

        public void ExitFullscreen()
        {
            if (Shared.IsFullscreenN)
            {
                return;
            }

            ApplicationView view = ApplicationView.GetForCurrentView();
            view.ExitFullScreenMode();
            Shared.IsFullscreen = false;
            SetTabViewVisibility(true);
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            EnterFullscreen();
        }

        private void BackToWindow_Click(object sender, RoutedEventArgs e)
        {
            ExitFullscreen();
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateFullscreenMode(PageType.Unknown);
        }

        private void E_RootPage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ExitFullscreen();
            }
        }
    }
}
