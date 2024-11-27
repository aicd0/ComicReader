// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReader.Common.ReadonlyRatingView;

public partial class ReadonlyRatingView
{
    public static readonly DependencyProperty PlaceholderValueProperty =
        DependencyProperty.Register(nameof(PlaceholderValue), typeof(int), typeof(ReadonlyRatingView), new PropertyMetadata(-1, PlaceholderValueChanged));

    public int PlaceholderValue
    {
        get { return (int)GetValue(PlaceholderValueProperty); }
        set { SetValue(PlaceholderValueProperty, value); }
    }

    private static void PlaceholderValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = d as ReadonlyRatingView;
        self.UpdateUI();
    }
}
