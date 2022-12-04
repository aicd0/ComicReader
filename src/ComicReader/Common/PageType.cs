using System;

namespace ComicReader.Common.Router
{
    public enum PageType
    {
        Home,
        Search,
        Reader,
        Settings,
        Help,
        Unknown,
    }

    internal class PageTypeUtils
    {
        public static Type PageTypeToType(PageType type)
        {
            switch (type)
            {
                case PageType.Home:
                    return typeof(Views.HomePage);
                case PageType.Search:
                    return typeof(Views.SearchPage);
                case PageType.Reader:
                    return typeof(Views.ReaderPage);
                case PageType.Settings:
                    return typeof(Views.SettingsPage);
                case PageType.Help:
                    return typeof(Views.HelpPage);
                default:
                    throw new Exception();
            }
        }
    }
}
