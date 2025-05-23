﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.PageBase;

namespace ComicReader.Helpers.Navigation;

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
