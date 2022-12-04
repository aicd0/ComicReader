using System;
using ComicReader.Common.Router;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Common
{
    internal class NavigatablePage : StatefulPage, ITabListener
    {
        private TabIdentifier mTabId;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NavigationManager.GetInstance().OnNavigateTo(this, e);
            base.OnNavigatedTo(e);
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            mTabId = p.tabId;
        }

        public override void OnResume()
        {
            base.OnResume();
            NavigationManager.GetInstance().ExecutePostTasks();
        }

        public virtual void OnSelected()
        {
        }

        public virtual string GetUniqueString(object args)
        {
            return "BasePage";
        }

        public virtual bool AllowJump()
        {
            return true;
        }

        public virtual bool SupportFullscreen()
        {
            return false;
        }

        protected TabIdentifier GetTabId()
        {
            return mTabId;
        }
    }
}
