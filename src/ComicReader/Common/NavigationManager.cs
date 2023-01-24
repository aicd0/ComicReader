using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.Router
{
    internal class NavigationParams
    {
        public object Params;
        public TabIdentifier TabId;
    }

    internal class TabIdentifier
    {
        public muxc.TabViewItem Tab;
        public IPageTrait PageTrait;
        public object RequestArgs;
        public ITabListener TabListener;

        public event Action Selected;
        public event Action<IPageTrait> PageTraitChanged;

        public TabIdentifier()
        {
            Selected += delegate
            {
                TabListener?.OnSelected();
            };
        }

        public void OnSelected()
        {
            Selected?.Invoke();
        }

        public void OnPageTypeChanged()
        {
            PageTraitChanged?.Invoke(PageTrait);
        }
    }

    internal interface ITabListener
    {
        void OnSelected();

        string GetUniqueString(object args);

        bool AllowJump();

        bool SupportFullscreen();
    }

    internal class NavigationManager
    {
        private class TabInfo
        {
            public readonly List<Scene> scenes = new List<Scene>();
            public int position = -1;
        }

        private class Scene
        {
            public IPageTrait PageTrait;
            public object RequestedArgs;
            public object Shared;
            public bool Resumed = false;
        }

        private static NavigationManager mInstance;
        private event Action mPostTasks;
        public event Action OnExitFullscreen;

        private readonly Dictionary<TabIdentifier, TabInfo> mTabs = new Dictionary<TabIdentifier, TabInfo>();

        private NavigationManager()
        {
        }

        public static NavigationManager GetInstance()
        {
            if (mInstance == null)
            {
                mInstance = new NavigationManager();
            }
            return mInstance;
        }

        public void OnNavigateTo(ITabListener listener, NavigationEventArgs e)
        {
            NavigationParams navigationParams = (NavigationParams)e.Parameter;
            TabIdentifier tabId = navigationParams.TabId;
            if (!mTabs.TryGetValue(tabId, out TabInfo tabInfo))
            {
                tabInfo = new TabInfo();
                mTabs.Add(tabId, tabInfo);
            }

            Scene scene;

            if (e.NavigationMode == NavigationMode.New)
            {
                if (tabInfo.scenes.Count > 0 && tabInfo.position < tabInfo.scenes.Count - 1)
                {
                    tabInfo.scenes.RemoveRange(tabInfo.position + 1, tabInfo.scenes.Count - tabInfo.position - 1);
                }

                scene = new Scene()
                {
                    PageTrait = tabId.PageTrait,
                    RequestedArgs = tabId.RequestArgs,
                    Shared = navigationParams.Params,
                };
                tabInfo.scenes.Add(scene);
                tabInfo.position++;
            }
            else
            {
                if (e.NavigationMode == NavigationMode.Forward)
                {
                    tabInfo.position++;
                }
                else if (e.NavigationMode == NavigationMode.Back)
                {
                    tabInfo.position--;
                }
            }

            tabInfo.position = Math.Min(tabInfo.scenes.Count - 1, Math.Max(0, tabInfo.position));
            if (tabInfo.position < 0)
            {
                return;
            }

            scene = tabInfo.scenes[tabInfo.position];
            tabId.PageTrait = scene.PageTrait;
            tabId.RequestArgs = scene.RequestedArgs;
            tabId.TabListener = listener;


            if (!listener.SupportFullscreen())
            {
                mPostTasks += delegate { OnExitFullscreen?.Invoke(); };
            }
            mPostTasks += delegate { tabId.OnPageTypeChanged(); };
        }

        public void ExecutePostTasks()
        {
            mPostTasks?.Invoke();
            mPostTasks = null;
        }
    }
}
