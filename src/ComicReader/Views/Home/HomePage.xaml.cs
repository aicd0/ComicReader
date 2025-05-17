// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.Imaging;
using ComicReader.Common.PageBase;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Data.Legacy;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.Navigation;
using ComicReader.ViewModels;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Home;

internal sealed partial class HomePage : BasePage
{
    private readonly HomePageViewModel ViewModel = new();

    private readonly CancellationSession _loadImageToken = new();
    private bool? _usingGroupSource = null;
    private readonly ComicItemViewModel.IItemHandler _comicItemHandler;

    public HomePage()
    {
        InitializeComponent();
        _comicItemHandler = new ComicItemHandler(this);
    }

    //
    // Lifecycle
    //

    protected override void OnResume()
    {
        base.OnResume();
        GetMainPageAbility().SetTitle(StringResourceProvider.GetResourceString("NewTab"));
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Document });

        ComicData.OnUpdated += OnComicDataUpdated;
        ObserveData();
        ViewModel.Initialize();
        ViewModel.UpdateLibrary();
    }

    protected override void OnPause()
    {
        base.OnPause();
        ComicData.OnUpdated -= OnComicDataUpdated;
        _loadImageToken.Next();
    }

    private void ObserveData()
    {
        ViewModel.FilterLiveData.ObserveSticky(this, UpdateFilters);
        ViewModel.GroupingEnabledLiveData.ObserveSticky(this, delegate (bool grouped)
        {
            if (_usingGroupSource != grouped)
            {
                _usingGroupSource = grouped;
                if (grouped)
                {
                    ComicGridView.SetBinding(ItemsControl.ItemsSourceProperty, new Binding()
                    {
                        Source = GroupedComicItemSource,
                        Mode = BindingMode.OneWay,
                    });
                }
                else
                {
                    ComicGridView.SetBinding(ItemsControl.ItemsSourceProperty, new Binding()
                    {
                        Source = UngroupedComicItemSource,
                        Mode = BindingMode.OneWay,
                    });
                }
            }
        });
    }

    private void UpdateFilters(HomePageViewModel.FilterModel model)
    {
        if (model == null)
        {
            return;
        }

        BindDropDownButton(ViewTypeDropDownButton, model.ViewTypeDropDown, ViewModel.SelectViewType);
        BindDropDownButton(SortByDropDownButton, model.SortByDropDown, ViewModel.SelectSortBy);
        BindDropDownButton(GroupByDropDownButton, model.GroupByDropDown, ViewModel.SelectGroupBy);
        BindDropDownButton(FilterPresetDropDownButton, model.FilterPresetDropDown, ViewModel.SelectFilterPreset);
    }

    private void BindDropDownButton<T>(DropDownButton button, HomePageViewModel.DropDownButtonModel<T> model, Action<T?> clickHandler)
    {
        if (button == null)
        {
            return;
        }

        button.Content = model.Name;

        FlyoutBase flyout = button.Flyout;
        if (flyout is not MenuFlyout)
        {
            flyout = new MenuFlyout();
            flyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            button.Flyout = flyout;
        }
        var menuFlyout = (MenuFlyout)flyout;

        menuFlyout.Items.Clear();
        foreach (HomePageViewModel.MenuFlyoutItemModel<T> item in model.Items)
        {
            MenuFlyoutItemBase menuItem;
            if (item.IsSeperator)
            {
                menuItem = new MenuFlyoutSeparator();
            }
            else if (item.CanToggle)
            {
                var actualMenuItem = new ToggleMenuFlyoutItem
                {
                    Text = item.Name,
                    IsChecked = item.Toggled,
                };
                actualMenuItem.Click += delegate (object sender, RoutedEventArgs e)
                {
                    clickHandler(item.DataContext);
                };
                menuItem = actualMenuItem;
            }
            else
            {
                var actualMenuItem = new MenuFlyoutItem
                {
                    Text = item.Name,
                };
                actualMenuItem.Click += delegate (object sender, RoutedEventArgs e)
                {
                    clickHandler(item.DataContext);
                };
                menuItem = actualMenuItem;
            }
            menuFlyout.Items.Add(menuItem);
        }
    }

    // Utilities
    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private void OnComicDataUpdated()
    {
        ViewModel.UpdateLibrary();
    }

    private void LoadImage(ComicItemVertical viewHolder, ComicItemViewModel item)
    {
        double image_width = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"] - 40.0;
        double image_height = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>();

        if (item.Image.ImageSet)
        {
            return;
        }

        item.Image.ImageSet = true;
        tokens.Add(new SimpleImageLoader.Token
        {
            Width = image_width,
            Height = image_height,
            Multiplication = 1.4,
            Source = new ComicCoverImageSource(item.Comic),
            ImageResultHandler = new LoadImageCallback(viewHolder, item)
        });

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
            _viewHolder.CompareAndBind(_viewModel);
        }
    }

    // Events
    private void OnAdaptiveGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is not ComicItemVertical viewHolder || args.Item is not ComicItemViewModel item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            item.Image.ImageSet = false;
        }
        else
        {
            item.ItemHandler = _comicItemHandler;
            viewHolder.Bind(item);
            LoadImage(viewHolder, item);
        }
    }

    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        var item = (ComicItemViewModel)((Grid)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void MarkAsReadOrUnread(ComicItemViewModel item, bool read)
    {
        if (read)
        {
            item.Comic.SetAsRead();
        }
        else
        {
            item.Comic.SetAsUnread();
        }

        item.UpdateProgress(true);
    }

    private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            item.IsFavorite = true;
            await FavoriteModel.Instance.Add(item.Comic.Id, item.Title, true);
        });
    }

    private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            item.IsFavorite = false;
            await FavoriteModel.Instance.RemoveWithId(item.Comic.Id, true);
        });
    }

    private void OnHideComicClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await item.Comic.SaveHiddenAsync(true);
            ViewModel.UpdateLibrary();
        });
    }

    private void EditFilterButton_Click(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var dialog = new EditFilterDialog(ViewModel.GetFilter());
            _ = await C0.ShowDialogAsync(dialog, XamlRoot);
            ViewModel.UpdateFilters();
        });
    }

    private void AddNewFolder()
    {
        C0.Run(async delegate
        {
            if (!await SettingDataManager.AddComicFolderUsingPicker(WindowId))
            {
                return;
            }

            ComicData.UpdateAllComics("HomePage#AddNewFolder", lazy: true);
        });
    }

    private void AddFolderHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        AddNewFolder();
    }

    private void RefreshHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ComicData.UpdateAllComics("RefreshPage", lazy: true);
    }

    private class ComicItemHandler : ComicItemViewModel.IItemHandler
    {
        private readonly WeakReference<HomePage> _pageRef;

        public ComicItemHandler(HomePage page)
        {
            _pageRef = new WeakReference<HomePage>(page);
        }

        public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            GetPage()?.OnAddToFavoritesClicked(sender, e);
        }

        public void OnHideClicked(object sender, RoutedEventArgs e)
        {
            GetPage()?.OnHideComicClicked(sender, e);
        }

        public void OnItemTapped(object sender, TappedRoutedEventArgs e)
        {
            GetPage()?.OnComicItemTapped(sender, e);
        }

        public void OnMarkAsReadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.MarkAsReadOrUnread(item, true);
        }

        public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.MarkAsReadOrUnread(item, false);
        }

        public void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            GetPage()?.OnOpenInNewTabClicked(sender, e);
        }

        public void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            GetPage()?.OnRemoveFromFavoritesClicked(sender, e);
        }

        public void OnSelectClicked(object sender, RoutedEventArgs e)
        {
        }

        public void OnUnhideClicked(object sender, RoutedEventArgs e)
        {
        }

        private HomePage? GetPage()
        {
            return _pageRef.TryGetTarget(out HomePage? page) ? page : null;
        }
    }
}
