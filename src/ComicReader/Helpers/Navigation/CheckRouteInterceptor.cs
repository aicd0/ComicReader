﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.PageBase;

namespace ComicReader.Helpers.Navigation;

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
