using ComicReader.Common.Router;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Common
{
    internal interface IPageStateListener
    {
        void OnStart(object p);

        void OnResume();

        void OnPause();
    }

    internal class StatefulPage : Page, IPageStateListener
    {
        private bool mIsStarted = false;
        private bool mIsResumed = false;

        public StatefulPage()
        {
            Loaded += OnLoadedInternal;
            Unloaded += OnUnloadedInternal;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (e.NavigationMode)
            {
                case NavigationMode.New:
                case NavigationMode.Back:
                case NavigationMode.Forward:
                    TryStart(e.Parameter);
                    TryResume();
                    break;
                case NavigationMode.Refresh:
                    break;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            switch (e.NavigationMode)
            {
                case NavigationMode.New:
                case NavigationMode.Back:
                case NavigationMode.Forward:
                    TryPause();
                    break;
                case NavigationMode.Refresh:
                    break;
            }
        }

        public virtual void OnStart(object p)
        {
        }

        public virtual void OnPause()
        {
        }

        public virtual void OnResume()
        {
        }

        public virtual void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        public virtual void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnLoadedInternal(object sender, RoutedEventArgs e)
        {
            if (!(sender as Page).IsLoaded)
            {
                return;
            }
            OnLoaded(sender, e);
        }

        private void OnUnloadedInternal(object sender, RoutedEventArgs e)
        {
            if ((sender as Page).IsLoaded)
            {
                return;
            }
            OnUnloaded(sender, e);
        }

        private void TryStart(object p)
        {
            if (!mIsStarted)
            {
                mIsStarted = true;
                OnStart(p);
            }
        }

        private void TryResume()
        {
            if (!mIsStarted)
            {
                return;
            }
            if (!mIsResumed)
            {
                mIsResumed = true;
                OnResume();
            }
        }

        private void TryPause()
        {
            if (mIsResumed)
            {
                mIsResumed = false;
                OnPause();
            }
        }
    }
}
