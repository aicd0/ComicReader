// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Router;
internal interface IRouterInterceptor
{
    NavigationBundle Intercept(RouteInfo routeInfo);
}
