using ComicReader.Database;
using System;

namespace ComicReader.Common.Router
{
    internal interface IPageTrait
    {
        Type GetPageType();

        bool HasNavigationBar();

        bool ImmersiveMode();

        string GetUniqueString(object args);

        bool AllowJump();
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

        public bool ImmersiveMode()
        {
            return false;
        }

        public string GetUniqueString(object args)
        {
            return "blank";
        }

        public bool AllowJump()
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

        public bool ImmersiveMode()
        {
            return false;
        }

        public string GetUniqueString(object args)
        {
            string keyword = (string)args;
            return "Search/" + keyword;
        }

        public bool AllowJump()
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

        public string GetUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Location;
        }

        public bool AllowJump()
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

        public bool ImmersiveMode()
        {
            return false;
        }

        public string GetUniqueString(object args)
        {
            return "settings";
        }

        public bool AllowJump()
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

        public bool ImmersiveMode()
        {
            return false;
        }

        public string GetUniqueString(object args)
        {
            return "help";
        }

        public bool AllowJump()
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
