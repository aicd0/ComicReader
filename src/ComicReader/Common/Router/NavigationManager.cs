using ComicReader.Common.Constants;
using ComicReader.Utils;
using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Common.Router
{
    internal class NavigationParams
    {
        public object Params;
        public TabIdentifier TabId;
    }

    internal class TabIdentifier
    {
        public TabViewItem Tab;
        public IPageTrait PageTrait;
        public object RequestArgs;
        public readonly EventBus TabEventBus = new EventBus();

        public void DispatchTabPageChanged()
        {
            TabEventBus.With<IPageTrait>(EventId.TabPageChanged).Emit(PageTrait);
        }

        public void DispatchTabSelected()
        {
            TabEventBus.With(EventId.TabSelected).Emit(0);
        }
    }

    internal static class NavigationManager
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

        private static event Action E_postTasks;

        private static readonly Dictionary<TabIdentifier, TabInfo> mTabs = new Dictionary<TabIdentifier, TabInfo>();

        public static void OnNavigateTo(NavigationEventArgs e)
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

            E_postTasks = null;
            E_postTasks += delegate {
                tabId.DispatchTabPageChanged();
            };
        }

        public static void ExecutePostTasks()
        {
            E_postTasks?.Invoke();
            E_postTasks = null;
        }
    }
}
