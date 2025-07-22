// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.UserControls.ComicItemView;

internal class BaseComicItemMenuFlyoutHandler(ComicModel comic, IComicItemViewHandler handler) : IComicItemMenuFlyoutHandler
{
    void IComicItemMenuFlyoutHandler.OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        handler.OnAddToFavoritesClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnEditClick(object sender, RoutedEventArgs e)
    {
        handler.OnEditClick(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnHideClicked(object sender, RoutedEventArgs e)
    {
        handler.OnHideClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        handler.OnItemTapped(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadClicked(object sender, RoutedEventArgs e)
    {
        handler.OnMarkAsReadClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadingClicked(object sender, RoutedEventArgs e)
    {
        handler.OnMarkAsReadingClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
    {
        handler.OnMarkAsUnreadClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        handler.OnOpenInNewTabClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        handler.OnRemoveFromFavoritesClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnSelectClicked(object sender, RoutedEventArgs e)
    {
        handler.OnSelectClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnUnhideClicked(object sender, RoutedEventArgs e)
    {
        handler.OnUnhideClicked(sender, e);
    }

    void IComicItemMenuFlyoutHandler.OnOpenInFileExplorerClicked(object sender, RoutedEventArgs e)
    {
        comic.ShowInFileExplorer();
    }
}
