// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal interface IComicItemMenuFlyoutHandler
{
    void OnItemTapped(object sender, TappedRoutedEventArgs e);

    void OnOpenInNewTabClicked(object sender, RoutedEventArgs e);

    void OnAddToFavoritesClicked(object sender, RoutedEventArgs e);

    void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e);

    void OnHideClicked(object sender, RoutedEventArgs e);

    void OnUnhideClicked(object sender, RoutedEventArgs e);

    void OnMarkAsReadClicked(object sender, RoutedEventArgs e);

    void OnMarkAsReadingClicked(object sender, RoutedEventArgs e);

    void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e);

    void OnEditClick(object sender, RoutedEventArgs e);

    void OnSelectClicked(object sender, RoutedEventArgs e);

    void OnOpenInFileExplorerClicked(object sender, RoutedEventArgs e);
}
