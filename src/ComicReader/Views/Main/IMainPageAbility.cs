// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.PageBase;
using ComicReader.Helpers.Navigation;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Main;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    void OpenInCurrentTab(Route route);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void SetCurrentPageInfo(string url, IPageTrait pageTrait);

    void RegisterTabUnselectedHandler(Page owner, TabUnselectedEventHandler handler);

    void RegisterFullscreenChangedHandler(Page owner, FullscreenChangedEventHandler handler);
}
