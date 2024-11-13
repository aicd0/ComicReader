// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Router;
using ComicReader.Views.Base;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Main;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    void SetNavigationBundle(NavigationBundle bundle);

    void OpenInCurrentTab(Route route);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void RegisterTabUnselectedHandler(Page owner, TabUnselectedEventHandler handler);

    void RegisterFullscreenChangedHandler(Page owner, FullscreenChangedEventHandler handler);
}
