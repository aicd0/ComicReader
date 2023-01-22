using ComicReader.Common.Router;
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
