// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Helpers.Navigation;

internal interface IRouterInterceptor
{
    NavigationBundle Intercept(RouteInfo routeInfo);
}
