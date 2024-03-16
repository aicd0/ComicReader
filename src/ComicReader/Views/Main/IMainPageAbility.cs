using ComicReader.Router;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Main;

internal interface IMainPageAbility
{
    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    void SetNavigationBundle(NavigationBundle bundle);

    void OpenInCurrentTab(Route route);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void RegisterFullscreenChangedHandler(FullscreenChangedEventHandler handler);
}
