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
        private bool _isStarted = false;
        private bool _isResumed = false;
        private bool _isLoaded = false;

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

        private void OnLoadedInternal(object sender, RoutedEventArgs e)
        {
            if (!(sender as Page).IsLoaded)
            {
                return;
            }
            _isLoaded = true;
            TryResume();
        }

        private void OnUnloadedInternal(object sender, RoutedEventArgs e)
        {
            if ((sender as Page).IsLoaded)
            {
                return;
            }
            _isLoaded = false;
            TryPause();
        }

        private void TryStart(object p)
        {
            if (!_isStarted)
            {
                _isStarted = true;
                OnStart(p);
            }
        }

        private void TryResume()
        {
            if (!_isStarted || !_isLoaded)
            {
                return;
            }
            if (!_isResumed)
            {
                _isResumed = true;
                OnResume();
            }
        }

        private void TryPause()
        {
            if (_isResumed)
            {
                _isResumed = false;
                OnPause();
            }
        }

        public virtual void OnStart(object p)
        {
        }

        public virtual void OnResume()
        {
        }

        public virtual void OnPause()
        {
        }
    }
}
