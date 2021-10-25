using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Views
{
    class TabManager
    {
        private struct TabIdInfo
        {
            public PageType Type;
            public object RequestArgs;
        };

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
            if (TabId == null)
            {
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

                m_info.Add(new TabIdInfo
                {
                    Type = TabId.Type,
                    RequestArgs = TabId.RequestArgs,
                });

                m_info_index = m_info.Count - 1;
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

            TabIdInfo info = m_info[m_info_index];
            TabId.Type = info.Type;
            TabId.RequestArgs = info.RequestArgs;
            TabId.OnTabSelected += OnPageEntered;

            OnPageEntered?.Invoke();
            OnUpdate?.Invoke(TabId);
        }
    }
}
