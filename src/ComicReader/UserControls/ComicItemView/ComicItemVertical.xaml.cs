// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Imaging;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.UserControls.ComicItemView;

internal sealed partial class ComicItemVertical : BaseUserControl, IComicItemView
{
    private readonly CancellationSession _loadImageToken = new();
    private IComicItemViewHandler? _itemHandler;

    public ComicItemViewModel? Ctx => DataContext as ComicItemViewModel;
    public ComicItemViewModel? Item { get; private set; }

    public ComicItemVertical()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsLoaded)
        {
            return;
        }

        ComicItemViewModel? item = Item;
        if (item != null)
        {
            RequestImageIfNeeded(item);
        }
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.IsLoaded)
        {
            return;
        }

        _loadImageToken.Next();
        ComicItemViewModel? item = Item;
        if (item != null)
        {
            item.Image.ImageRequested = false;
        }
    }

    private void UserControl_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent tap events being dispatched to other controls
        e.Handled = true;
    }

    private void RootGrid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    public void Bind(ComicItemViewModel item, IComicItemViewHandler handler)
    {
        if (item != Item)
        {
            item.Image.ImageRequested = false;
            Item = item;
        }

        IComicItemViewHandler? oldHandler = _itemHandler;
        if (oldHandler != null)
        {
            RootGrid.Tapped -= oldHandler.OnItemTapped;
        }
        _itemHandler = handler;
        RootGrid.Tapped += handler.OnItemTapped;

        BindImage(item);
        RequestImageIfNeeded(item);

        List<MenuFlyoutItemBase> menuItems = ComicItemMenuFlyoutCreator.CreateMenuItems(item, new BaseComicItemMenuFlyoutHandler(item.Comic, handler));
        MenuFlyout menuFlyout = new();
        foreach (MenuFlyoutItemBase menuItem in menuItems)
        {
            menuFlyout.Items.Add(menuItem);
        }
        RootGrid.ContextFlyout = menuFlyout;
    }

    public void Unbind()
    {
        ComicItemViewModel? item = Item;
        Item = null;
        if (item != null)
        {
            item.Image.ImageRequested = false;
            item.Image.Image = null;
        }
    }

    private void BindImage(ComicItemViewModel item)
    {
        BitmapImage? image = item.Image.Image;
        ImageHolder1.Source = image;
        ImageHolder2.Source = image;
    }

    private void RequestImageIfNeeded(ComicItemViewModel item)
    {
        if (item.Image.Image != null || item.Image.ImageRequested)
        {
            return;
        }
        item.Image.ImageRequested = true;
        double imageWidth = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"] - 40.0;
        double imageHeight = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>
        {
            new(new ComicCoverImageSource(item.Comic), new LoadImageCallback(this, item)) {
                Width = imageWidth,
                Height = imageHeight,
                Multiplication = 1.4,
            }
        };
        new SimpleImageLoader.Transaction(_loadImageToken.Token, tokens).Commit();
    }

    private class LoadImageCallback : IImageResultHandler
    {
        private readonly ComicItemVertical _viewHolder;
        private readonly ComicItemViewModel _viewModel;

        public LoadImageCallback(ComicItemVertical viewHolder, ComicItemViewModel viewModel)
        {
            _viewHolder = viewHolder;
            _viewModel = viewModel;
        }

        public void OnSuccess(BitmapImage image)
        {
            _viewModel.Image.Image = image;
            if (_viewModel == _viewHolder.Item)
            {
                _viewHolder.BindImage(_viewModel);
            }
        }
    }
}
