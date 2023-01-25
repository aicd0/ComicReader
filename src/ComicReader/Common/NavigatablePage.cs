using ComicReader.Common.Router;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Common
{
    internal class NavigatablePage : StatefulPage, ITabListener
    {
        private TabIdentifier mTabId;
        private PointerPoint mLastPointerPoint;

        public NavigatablePage()
        {
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NavigationManager.GetInstance().OnNavigateTo(this, e);
            base.OnNavigatedTo(e);
        }

        public virtual void OnStart(NavigationParams p)
        {
            mTabId = p.TabId;
        }

        public override void OnStart(object p)
        {
            base.OnStart(p);
            OnStart((NavigationParams)p);
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

        protected TabIdentifier GetTabId()
        {
            return mTabId;
        }

        private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            mLastPointerPoint = e.GetCurrentPoint((UIElement)sender);
        }

        protected bool CanHandleTapped()
        {
            if (mLastPointerPoint == null)
            {
                return true;
            }
            if (mLastPointerPoint.Properties.IsXButton1Pressed || mLastPointerPoint.Properties.IsXButton2Pressed)
            {
                return false;
            }
            return true;
        }
    }
}
