using System;

namespace ComicReader.Router;
internal class CheckRouteInterceptor : IRouterInterceptor
{
    public NavigationBundle Intercept(RouteInfo routeInfo)
    {
        if (routeInfo.Scheme != RouterConstants.SCHEME_APP_NO_PREFIX)
        {
            throw new ArgumentException($"Invalid scheme {routeInfo.Scheme}");
        }

        if (routeInfo.Port != -1)
        {
            throw new ArgumentException($"Invalid port {routeInfo.Port}");
        }

        if (routeInfo.Path.Length > 0)
        {
            throw new ArgumentException($"Invalid path {routeInfo.Path}");
        }

        if (routeInfo.Fragment.Length > 0)
        {
            throw new ArgumentException($"Invalid fragment {routeInfo.Fragment}");
        }

        return null;
    }
}
