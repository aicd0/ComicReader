// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

using ComicReader.Common.DebugTools;
using ComicReader.Common.PageBase;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.Navigation;

internal static class AppRouter
{
    private static readonly List<IRouterInterceptor> sInterceptors = new()
    {
        new CheckRouteInterceptor(),
        new OpenPageInterceptor(),
    };

    public static NavigationBundle Process(Route route)
    {
        foreach (IRouterInterceptor interceptor in sInterceptors)
        {
            NavigationBundle bundle = interceptor.Intercept(route);
            if (bundle != null)
            {
                return bundle;
            }
        }

        return null;
    }

    public static void OpenInFrame(Frame frame, Route route)
    {
        NavigationBundle bundle = Process(route);
        if (bundle == null)
        {
            Logger.AssertNotReachHere("E029F8E967CF6196");
            return;
        }

        bool success = frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        Logger.Assert(success, "E331AFB9DB866700");
    }
}
