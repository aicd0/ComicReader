// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BasePage;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Navigation;

internal interface INavigationPageAbility : IPageAbility
{
    public delegate void CommonEventHandler();
    public delegate void GridViewModeChangedEventHandler(bool enabled);
    public delegate void FavoriteChangedEventHandler(bool isFavorite);
    public delegate void ReaderSettingsChangedEventHandler(ReaderSettingDataModel settings);

    void SetReaderSettings(ReaderSettingDataModel settings);

    void SetExternalComic(bool isExternal);

    void SetFavorite(bool isFavorite);

    void SetGridViewMode(bool enabled);

    bool GetIsSidePaneOpen();

    void SetIsSidePaneOpen(bool isOpen);

    void SetSearchBox(string text);

    void RegisterLeavingHandler(Page owner, CommonEventHandler handler);

    void RegisterExpandInfoPaneHandler(Page owner, CommonEventHandler handler);

    void RegisterGridViewModeChangedHandler(Page owner, GridViewModeChangedEventHandler handler);

    void RegisterReaderSettingsChangedEventHandler(Page owner, ReaderSettingsChangedEventHandler handler);

    void RegisterFavoriteChangedEventHandler(Page owner, FavoriteChangedEventHandler handler);
}