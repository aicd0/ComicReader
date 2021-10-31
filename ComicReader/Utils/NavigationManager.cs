using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;

namespace ComicReader.Utils.Tab
{
    public enum PageType
    {
        Home,
        Search,
        Reader,
        Settings,
        Unknown,
    }

    public class TabIdentifier
    {
        public PageType Type;
        public muxc.TabViewItem Tab;
        public object RequestArgs;
        public Action OnTabSelected;

        public string UniqueString => TabManager.PageUniqueString(Type, RequestArgs);
    }

    public class NavigationParams
    {
        public object Shared;
        public TabIdentifier TabId;
    }

    class TabManager
    {
        private struct TabIdInfo
        {
            public PageType Type;
            public object RequestArgs;
        };

        private bool m_initialized = false;
        private List<TabIdInfo> m_info = new List<TabIdInfo>();
        private int m_info_index = -1;
        private NavigationMode m_navigation_mode = NavigationMode.New;

        public TabIdentifier TabId { get; private set; }

        public Action<object> OnSetShared { get; set; }

        public Action OnPageEntered { get; set; }

        public Action<TabIdentifier> OnUpdate { get; set; }

        public void OnNavigatedFrom(NavigatingCancelEventArgs e)
        {
            m_navigation_mode = e.NavigationMode;
        }

        public void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!m_initialized)
            {
                m_initialized = true;
                NavigationParams nav_params = (NavigationParams)e.Parameter;
                TabId = nav_params.TabId;
                OnSetShared?.Invoke(nav_params.Shared);
            }

            if (e.NavigationMode == NavigationMode.New)
            {
                if (m_info.Count != 0 && m_info_index < m_info.Count - 1)
                {
                    m_info.RemoveRange(m_info_index + 1,
                        m_info.Count - m_info_index - 1);
                }

                TabIdInfo new_info = new TabIdInfo();

                if (TabId != null)
                {
                    new_info.Type = TabId.Type;
                    new_info.RequestArgs = TabId.RequestArgs;
                }

                m_info.Add(new_info);
                m_info_index = m_info.Count - 1;
            }
            else if (e.NavigationMode == NavigationMode.Refresh)
            {
                // do nothing.
            }
            else
            {
                if (e.NavigationMode == m_navigation_mode)
                {
                    if (e.NavigationMode == NavigationMode.Back)
                    {
                        m_info_index--;
                    }
                    else if (e.NavigationMode == NavigationMode.Forward)
                    {
                        m_info_index++;
                    }
                }
            }

            if (TabId != null)
            {
                TabIdInfo info = m_info[m_info_index];
                TabId.Type = info.Type;
                TabId.RequestArgs = info.RequestArgs;
                TabId.OnTabSelected += OnPageEntered;
            }

            OnPageEntered?.Invoke();
            OnUpdate?.Invoke(TabId);
        }

        public static Type TypeFromPageTypeEnum(Utils.Tab.PageType type)
        {
            switch (type)
            {
                case PageType.Home:
                    return typeof(Views.HomePage);
                case PageType.Search:
                    return typeof(Views.SearchPage);
                case PageType.Reader:
                    return typeof(Views.ReaderPage);
                case PageType.Settings:
                    return typeof(Views.SettingsPage);
                default:
                    throw new Exception();
            }
        }

        public static string PageUniqueString(Utils.Tab.PageType type, object args)
        {
            switch (type)
            {
                case PageType.Home:
                    return Views.HomePage.PageUniqueString(args);
                case PageType.Search:
                    return Views.SearchPage.PageUniqueString(args);
                case PageType.Reader:
                    return Views.ReaderPage.PageUniqueString(args);
                case PageType.Settings:
                    return Views.SettingsPage.PageUniqueString(args);
                default:
                    throw new Exception();
            }
        }
    }
}
