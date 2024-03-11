using ComicReader.Router;
using ComicReader.Utils;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Main;
internal interface IMainPageAbility
{
    void SetNavigationBundle(NavigationBundle bundle);

    void OpenInCurrentTab(Route route);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    LiveData<bool> GetIsFullscreenLiveData();
}
