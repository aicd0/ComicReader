// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Views.Favorite;
using ComicReader.Views.History;
using ComicReader.Views.Navigation;

namespace ComicReader.Router;

internal class OpenPageInterceptor : IRouterInterceptor
{
    public NavigationBundle Intercept(RouteInfo routeInfo)
    {
        IPageTrait pageTrait = routeInfo.Host switch
        {
            RouterConstants.HOST_READER => ReaderPageTrait.Instance,
            RouterConstants.HOST_HOME => HomePageTrait.Instance,
            RouterConstants.HOST_SEARCH => SearchPageTrait.Instance,
            RouterConstants.HOST_SETTING => SettingPageTrait.Instance,
            RouterConstants.HOST_HELP => HelpPageTrait.Instance,
            RouterConstants.HOST_FAVORITE => new DefaultPageTrait(typeof(FavoritePage)),
            RouterConstants.HOST_HISTORY => new DefaultPageTrait(typeof(HistoryPage)),
            RouterConstants.HOST_NAVIGATION => new DefaultPageTrait(typeof(NavigationPage)),
            _ => throw new ArgumentException("Unknown host " + routeInfo.Host),
        };
        return new NavigationBundle(pageTrait, routeInfo.Queries, routeInfo.Url);
    }
}
