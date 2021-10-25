using System;
using System.Collections.Generic;
using muxc = Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views
{
    public enum PageType
    {
        Home,
        Search,
        Reader,
        Settings,
        Unknown,
    }

    class PageUtils
    {
        public static Type GetPageType(PageType type)
        {
            switch (type)
            {
                case PageType.Home:
                    return typeof(HomePage);
                case PageType.Search:
                    return typeof(SearchPage);
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
                case PageType.Home:
                    return HomePage.GetPageUniqueString(args);
                case PageType.Search:
                    return SearchPage.GetPageUniqueString(args);
                case PageType.Reader:
                    return ReaderPage.GetPageUniqueString(args);
                case PageType.Settings:
                    return SettingsPage.GetPageUniqueString(args);
                default:
                    throw new Exception();
            }
        }
    }

    public class TabIdentifier
    {
        public PageType Type;
        public muxc.TabViewItem Tab;
        public object RequestArgs;
        public Action OnTabSelected;

        public string UniqueString => PageUtils.GetPageUniqueString(Type, RequestArgs);
    }

    public class NavigationParams
    {
        public object Shared;
        public TabIdentifier TabId;
    }
}