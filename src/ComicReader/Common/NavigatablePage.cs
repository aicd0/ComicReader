using ComicReader.Common.Router;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Common
{
    internal class NavigatablePage : StatefulPage
    {
        private TabIdentifier mTabId;
        private PointerPoint mLastPointerPoint;

        public NavigatablePage()
        {
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NavigationManager.OnNavigateTo(e);
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
            NavigationManager.ExecutePostTasks();
        }

        public virtual void OnSelected()
        {
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
