namespace ComicReader.Router;
internal interface IRouterInterceptor
{
    NavigationBundle Intercept(RouteInfo routeInfo);
}
