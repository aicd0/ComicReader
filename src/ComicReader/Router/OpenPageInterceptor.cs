using ComicReader.Views;
using ComicReader.Views.Navigation;
using System;

namespace ComicReader.Router;
internal class OpenPageInterceptor : IRouterInterceptor
{
    public NavigationBundle Intercept(RouteInfo routeInfo)
    {
        IPageTrait pageTrait;
        switch (routeInfo.Host)
        {
            case RouterConstants.HOST_READER:
                pageTrait = ReaderPageTrait.Instance;
                break;
            case RouterConstants.HOST_HOME:
                pageTrait = HomePageTrait.Instance;
                break;
            case RouterConstants.HOST_SEARCH:
                pageTrait = SearchPageTrait.Instance;
                break;
            case RouterConstants.HOST_SETTING:
                pageTrait = SettingPageTrait.Instance;
                break;
            case RouterConstants.HOST_HELP:
                pageTrait = HelpPageTrait.Instance;
                break;
            case RouterConstants.HOST_FAVORITE:
                pageTrait = new DefaultPageTrait(typeof(FavoritePage));
                break;
            case RouterConstants.HOST_HISTORY:
                pageTrait = new DefaultPageTrait(typeof(HistoryPage));
                break;
            case RouterConstants.HOST_NAVIGATION:
                pageTrait = new DefaultPageTrait(typeof(NavigationPage));
                break;
            default:
                throw new ArgumentException("Unknown host " + routeInfo.Host);
        }

        return new NavigationBundle(pageTrait, routeInfo.Queries, routeInfo.Url);
    }
}
