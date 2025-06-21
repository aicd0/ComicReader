// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.PageBase;

namespace ComicReader.Helpers.Navigation;

internal interface IRouterInterceptor
{
    NavigationBundle Intercept(Route route);
}
