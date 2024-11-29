// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Helpers.Navigation;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Navigation;

internal sealed partial class SidePane : UserControl
{
    public delegate void NavigatingCancelEventHandler(NavigationBundle bundle);
    public event NavigatingCancelEventHandler Navigating;

    public SidePane()
    {
        InitializeComponent();
    }

    private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string item = (string)((NavigationViewItem)args.SelectedItem).Content;

        Route route = item switch
        {
            "Favorites" => new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_FAVORITE),
            "History" => new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_HISTORY),
            _ => throw new Exception(),
        };
        NavigationBundle bundle = route.Process();
        Navigating?.Invoke(bundle);
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }
}
