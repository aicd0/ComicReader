// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.ReadonlyRatingView;

public partial class ReadonlyRatingView : UserControl
{
    private bool _isLoaded = false;

    public ReadonlyRatingView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateUI();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        UpdateUI();
    }

    private void OnRatingStackPanelLoaded(object sender, RoutedEventArgs e)
    {
        (sender as StackPanel).Spacing = -6;
    }

    private void UpdateUI()
    {
        if (!_isLoaded)
        {
            return;
        }

        InternalRatingView.PlaceholderValue = PlaceholderValue;
    }
}
