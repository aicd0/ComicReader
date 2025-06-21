// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.Imaging;
using ComicReader.Common.PageBase;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.UserControls.ComicItemView;

internal sealed partial class ComicItemHorizontal : BaseUserControl, IComicItemView
{
    private readonly CancellationSession _loadImageToken = new();
    private IComicItemViewHandler? _itemHandler;

    public ComicItemViewModel? Ctx => DataContext as ComicItemViewModel;
    public ComicItemViewModel? Item { get; private set; }

    public ComicItemHorizontal()
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

        List<MenuFlyoutItemBase> menuItems = ComicItemMenuFlyoutCreator.CreateMenuItems(item, handler);
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
        ImageHolder.Source = image;
    }

    private void RequestImageIfNeeded(ComicItemViewModel item)
    {
        if (item.Image.Image != null || item.Image.ImageRequested)
        {
            return;
        }
        item.Image.ImageRequested = true;
        double imageWidth = (double)Application.Current.Resources["ComicItemHorizontalImageWidth"];
        double imageHeight = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>
        {
            new() {
                Width = imageWidth,
                Height = imageHeight,
                Multiplication = 1.4,
                StretchMode = StretchModeEnum.UniformToFill,
                Source = new ComicCoverImageSource(item.Comic),
                ImageResultHandler = new LoadImageCallback(this, item)
            }
        };
        new SimpleImageLoader.Transaction(_loadImageToken.Token, tokens).Commit();
    }

    private class LoadImageCallback : IImageResultHandler
    {
        private readonly ComicItemHorizontal _viewHolder;
        private readonly ComicItemViewModel _viewModel;

        public LoadImageCallback(ComicItemHorizontal viewHolder, ComicItemViewModel viewModel)
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
