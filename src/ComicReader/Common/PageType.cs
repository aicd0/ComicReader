using System;

namespace ComicReader.Common.Router
{
    internal interface IPageTrait
    {
        Type GetPageType();

        bool HasNavigationBar();

        bool HasTopPadding();
    }

    internal class HomePageTrait : IPageTrait
    {
        private HomePageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.HomePage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool HasTopPadding()
        {
            return true;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new HomePageTrait();
                }
                return _instance;
            }
        }
    }

    internal class SearchPageTrait : IPageTrait
    {
        private SearchPageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.SearchPage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool HasTopPadding()
        {
            return true;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SearchPageTrait();
                }
                return _instance;
            }
        }
    }

    internal class ReaderPageTrait : IPageTrait
    {
        private ReaderPageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.ReaderPage);
        }

        public bool HasNavigationBar()
        {
            return true;
        }

        public bool HasTopPadding()
        {
            return false;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ReaderPageTrait();
                }
                return _instance;
            }
        }
    }

    internal class SettingPageTrait : IPageTrait
    {
        private SettingPageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.SettingsPage);
        }

        public bool HasNavigationBar()
        {
            return false;
        }

        public bool HasTopPadding()
        {
            return true;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingPageTrait();
                }
                return _instance;
            }
        }
    }

    internal class HelpPageTrait : IPageTrait
    {
        private HelpPageTrait() { }

        public Type GetPageType()
        {
            return typeof(Views.HelpPage);
        }

        public bool HasNavigationBar()
        {
            return false;
        }

        public bool HasTopPadding()
        {
            return true;
        }

        private static IPageTrait _instance;
        public static IPageTrait Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new HelpPageTrait();
                }
                return _instance;
            }
        }
    }
}
