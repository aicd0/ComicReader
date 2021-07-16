using System;
using System.Collections.Generic;
using muxc = Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views
{
    public enum PageType
    {
        Blank,
        Search,
        Reader,
        Settings,
        Unknown
    }

    class PageUtils
    {
        public static Type GetPageType(PageType type)
        {
            switch (type)
            {
                case PageType.Blank:
                    return typeof(BlankPage);
                case PageType.Search:
                    return typeof(SearchResultsPage);
                case PageType.Reader:
                    return typeof(ReaderPage);
                case PageType.Settings:
                    return typeof(SettingsPage);
                default:
                    throw new Exception();
            }
        }

        public static string GetPageUniqueString(PageType type, object args)
        {
            switch (type)
            {
                case PageType.Blank:
                    return BlankPage.GetPageUniqueString(args);
                case PageType.Search:
                    return SearchResultsPage.GetPageUniqueString(args);
                case PageType.Reader:
                    return ReaderPage.GetPageUniqueString(args);
                case PageType.Settings:
                    return SettingsPage.GetPageUniqueString(args);
                default:
                    throw new Exception();
            }
        }
    }

    public class TabId
    {
        public PageType Type;
        public muxc.TabViewItem Tab;
        public string UniqueString;
        public Action OnTabSelected;
    }

    public class NavigationParams
    {
        public object Shared;
        public TabId TabId;
    }
}