using System.Collections.Generic;

namespace ComicReader.Router;

internal static class AppRouter
{
    private static readonly List<IRouterInterceptor> sInterceptors = new()
    {
        new CheckRouteInterceptor(),
        new OpenPageInterceptor(),
    };

    public static NavigationBundle Process(RouteInfo routeInfo)
    {
        foreach (IRouterInterceptor interceptor in sInterceptors)
        {
            NavigationBundle bundle = interceptor.Intercept(routeInfo);
            if (bundle != null)
            {
                return bundle;
            }
        }

        return null;
    }
}
