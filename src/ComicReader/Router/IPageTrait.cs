using ComicReader.Views.Help;
using ComicReader.Views.Home;
using ComicReader.Views.Search;
using ComicReader.Views.Settings;
using System;

namespace ComicReader.Router
{
    internal interface IPageTrait
    {
        Type GetPageType();

        bool HasNavigationBar();

        bool ImmersiveMode();

        bool SupportFullscreen();
    }

    internal class DefaultPageTrait : IPageTrait
    {
        private readonly Type _pageType;

        public DefaultPageTrait(Type pageType)
        {
            _pageType = pageType;
        }

        public Type GetPageType()
        {
            return _pageType;
        }

        public bool HasNavigationBar()
        {
            return false;
        }

        public bool ImmersiveMode()
        {
            return false;
        }

        public bool SupportFullscreen()
        {
            return false;
        }
    }

    internal class HomePageTrait : IPageTrait
    {
        private HomePageTrait() { }

        public Type GetPageType()
        {
            return typeof(HomePage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool ImmersiveMode()
        {
            return false;
        }

        public bool SupportFullscreen()
        {
            return false;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                _instance ??= new HomePageTrait();
                return _instance;
            }
        }
    }

    internal class SearchPageTrait : IPageTrait
    {
        private SearchPageTrait() { }

        public Type GetPageType()
        {
            return typeof(SearchPage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool ImmersiveMode()
        {
            return false;
        }

        public bool SupportFullscreen()
        {
            return false;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                _instance ??= new SearchPageTrait();
                return _instance;
            }
        }
    }

    internal class ReaderPageTrait : IPageTrait
    {
        private ReaderPageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.Reader.ReaderPage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool ImmersiveMode()
        {
            return true;
        }

        public bool SupportFullscreen()
        {
            return true;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                _instance ??= new ReaderPageTrait();
                return _instance;
            }
        }
    }

    internal class SettingPageTrait : IPageTrait
    {
        private SettingPageTrait() { }

        public Type GetPageType()
        {
            return typeof(SettingsPage);
        }

        public bool HasNavigationBar()
        {
            return false;
        }

        public bool ImmersiveMode()
        {
            return false;
        }

        public bool SupportFullscreen()
        {
            return false;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                _instance ??= new SettingPageTrait();
                return _instance;
            }
        }
    }

    internal class HelpPageTrait : IPageTrait
    {
        private HelpPageTrait() { }

        public Type GetPageType()
        {
            return typeof(HelpPage);
        }

        public bool HasNavigationBar()
        {
            return false;
        }

        public bool ImmersiveMode()
        {
            return false;
        }

        public bool SupportFullscreen()
        {
            return false;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                _instance ??= new HelpPageTrait();
                return _instance;
            }
        }
    }
}
