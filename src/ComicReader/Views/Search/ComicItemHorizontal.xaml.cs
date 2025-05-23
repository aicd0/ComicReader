// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Search;

internal sealed partial class ComicItemHorizontal : UserControl
{
    public ComicItemViewModel Ctx => DataContext as ComicItemViewModel;
    public ComicItemViewModel Item { get; private set; }

    public ComicItemHorizontal()
    {
        InitializeComponent();
        DataContextChanged += delegate (FrameworkElement s, DataContextChangedEventArgs e)
        {
            Bindings.Update();
            if (e.NewValue != null)
            {
                Ctx.PropertyChanged += OnDataContextPropertyChanged;
            }
        };
    }

    public bool IsContextFlyoutEnabled
    {
        get { return (bool)GetValue(IsContextFlyoutEnabledProperty); }
        set { SetValue(IsContextFlyoutEnabledProperty, value); }
    }
    public static readonly DependencyProperty IsContextFlyoutEnabledProperty =
        DependencyProperty.Register(nameof(IsContextFlyoutEnabled), typeof(bool), typeof(ComicItemHorizontal), new PropertyMetadata(true));

    private void OnDataContextPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Bindings.Update();
    }

    private void OnMenuFlyoutOpening(object sender, object e)
    {
        if (!IsContextFlyoutEnabled)
        {
            (sender as MenuFlyout).Hide();
        }
    }

    private void OnUserControlTapped(object sender, TappedRoutedEventArgs e)
    {
        // prevent tap events being dispatched to other controls
        e.Handled = true;
    }

    public void Bind(ComicItemViewModel item)
    {
        Item = item;
        CompareAndBind(item);
    }

    public void CompareAndBind(ComicItemViewModel item)
    {
        if (item != Item)
        {
            return;
        }

        if (Item == null)
        {
            ImageHolder.Source = null;
        }

        ImageHolder.Source = Item.Image.Image;
    }
}
