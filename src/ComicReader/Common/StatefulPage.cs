using ComicReader.Common.Router;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ComicReader.Common
{
    internal interface IPageStateListener
    {
        void OnStart(NavigationParams p);

        void OnResume();

        void OnPause();
    }

    internal class StatefulPage : Page, IPageStateListener
    {
        private bool mIsStarted = false;
        private bool mIsResumed = false;

        public StatefulPage()
        {
            Loaded += delegate { TryResume(); };
            Unloaded += delegate { TryPause(); };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            NavigationParams navigationParams = (NavigationParams)e.Parameter;

            switch (e.NavigationMode)
            {
                case NavigationMode.New:
                case NavigationMode.Back:
                case NavigationMode.Forward:
                    TryStart(navigationParams);
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

        public virtual void OnStart(NavigationParams p)
        {
        }

        public virtual void OnPause()
        {
        }

        public virtual void OnResume()
        {
        }

        private void TryStart(NavigationParams p)
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
