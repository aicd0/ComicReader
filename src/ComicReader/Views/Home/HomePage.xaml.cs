// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.UserControls.ComicItemView;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;
using ComicReader.Views.Reader;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.Storage;

namespace ComicReader.Views.Home;

internal sealed partial class HomePage : BasePage
{
    private readonly HomePageViewModel ViewModel = new();

    private ScrollViewer? _comicGridScrollViewer;

    private ComicFilterModel.ViewTypeEnum? _viewType = null;
    private bool? _usingGroupSource = null;
    private readonly IComicItemViewHandler _comicItemHandler;
    private Storyboard? _headerTextBlockAnimation = null;
    private double _lastGridViewVerticalOffset = 0.0;

    public HomePage()
    {
        InitializeComponent();
        _comicItemHandler = new ComicItemHandler(this);
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.NewTab);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Document });
        ViewModel.Initialize();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ComicData.OnUpdated += OnComicDataUpdated;
        ObserveData();
    }

    protected override void OnPause()
    {
        base.OnPause();
        ComicData.OnUpdated -= OnComicDataUpdated;
    }

    private void ObserveData()
    {
        ViewModel.FilterLiveData.ObserveSticky(this, UpdateFilters);
        ViewModel.GroupingEnabledLiveData.ObserveSticky(this, delegate (bool grouped)
        {
            if (_usingGroupSource == grouped)
            {
                return;
            }
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
        });
        ViewModel.ViewTypeLiveData.ObserveSticky(this, delegate (ComicFilterModel.ViewTypeEnum type)
        {
            if (_viewType == type)
            {
                return;
            }
            _viewType = type;
            switch (type)
            {
                case ComicFilterModel.ViewTypeEnum.Large:
                    ComicGridView.ItemTemplate = LargeComicItemTemplate;
                    ComicGridView.ItemContainerStyle = (Style)Resources["VerticalComicItemContainerStyle"];
                    ComicGridView.DesiredWidth = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"];
                    ComicGridView.ItemHeight = (double)Application.Current.Resources["ComicItemVerticalDesiredHeight"];
                    break;
                case ComicFilterModel.ViewTypeEnum.Medium:
                    ComicGridView.ItemTemplate = MediumComicItemTemplate;
                    ComicGridView.ItemContainerStyle = (Style)Resources["SearchResultItemContainerExpandedStyle"];
                    ComicGridView.DesiredWidth = (double)Application.Current.Resources["ComicItemHorizontalDesiredWidth"];
                    ComicGridView.ItemHeight = (double)Application.Current.Resources["ComicItemHorizontalDesiredHeight"];
                    break;
                default:
                    Logger.AssertNotReachHere("DBC3B0E205A8C333");
                    break;
            }
        });
        GetNavigationPageAbility().RegisterSearchTextChangeHandler(this, ViewModel.SetSearchText);
        GetNavigationPageAbility().RegisterRefreshHandler(this, ViewModel.UpdateLibrary);
    }

    //
    // GridView
    //

    private void ComicGridView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = ComicGridView.ChildrenBreadthFirst().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            _comicGridScrollViewer = scrollViewer;
            scrollViewer.ViewChanged += ComicGridScrollViewer_ViewChanged;
        }
        else
        {
            Logger.AssertNotReachHere("90801E4FD070C67A");
        }
    }

    private void ComicGridView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = _comicGridScrollViewer;
        if (scrollViewer != null)
        {
            scrollViewer.ViewChanged -= ComicGridScrollViewer_ViewChanged;
        }
        _comicGridScrollViewer = null;
    }

    private void ComicGridScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv)
        {
            return;
        }

        double verticalOffset = sv.VerticalOffset;

        {
            Thickness p = HeaderAreaGrid.Padding;
            double newTop = Math.Max(20 - verticalOffset, 6);
            if (p.Top != newTop)
            {
                p.Top = newTop;
                HeaderAreaGrid.Padding = p;
            }
        }

        {
            double newOpacity = Math.Min(1.0, Math.Max(0, verticalOffset - 10) * 0.05);
            if (HeaderAreaBackgroundGrid.Opacity != newOpacity)
            {
                HeaderAreaBackgroundGrid.Opacity = newOpacity;
            }
        }

        if (verticalOffset > 20 && _lastGridViewVerticalOffset <= 20)
        {
            AnimateHeaderTextBlockOpacity(0.0);
        }
        else if (verticalOffset < 20 && _lastGridViewVerticalOffset >= 20)
        {
            AnimateHeaderTextBlockOpacity(1.0);
        }

        _lastGridViewVerticalOffset = verticalOffset;
    }

    private void OnAdaptiveGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is not IComicItemView viewHolder || args.Item is not ComicItemViewModel item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            viewHolder.Unbind();
        }
        else
        {
            viewHolder.Bind(item, _comicItemHandler);
        }
    }

    private void CollapseExpandGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is ComicGroupViewModel group)
        {
            ViewModel.CollapseOrExpandGroup(group);
        }
    }

    private void ComicGridView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SetSelectionMode(false);
    }

    private void ComicGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        List<ComicItemViewModel> selectedItems = [];
        foreach (object? item in ComicGridView.SelectedItems)
        {
            if (item is ComicItemViewModel comicItem)
            {
                selectedItems.Add(comicItem);
            }
        }
        ViewModel.SetSelection(selectedItems);
    }

    //
    // Command Bar
    //

    private void CommandBarSelectAllClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as AppBarToggleButton;
        if (button == null)
        {
            return;
        }
        if (button.IsChecked == true)
        {
            ComicGridView.SelectAll();
        }
        else
        {
            ComicGridView.DeselectRange(new ItemIndexRange(0, (uint)ComicGridView.Items.Count));
        }
    }

    private void CommandBarFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Favorite);
    }

    private void CommandBarUnFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Unfavorite);
    }

    private void CommandBarHideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Hide);
    }

    private void CommandBarUnhideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Unhide);
    }

    private void CommandBarMarkAsReadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsRead);
    }

    private void CommandBarMarkAsReadingClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsReading);
    }

    private void CommandBarMarkAsUnreadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsUnread);
    }

    //
    // Animation
    //

    private void AnimateHeaderTextBlockOpacity(double to)
    {
        double from = HeaderTextBlock.Opacity;
        if (_headerTextBlockAnimation != null)
        {
            _headerTextBlockAnimation.Stop();
            _headerTextBlockAnimation = null;
        }
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(Math.Abs(to - from) * 0.2)),
        };
        Storyboard.SetTarget(animation, HeaderTextBlock);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _headerTextBlockAnimation = storyboard;
    }

    //
    // Filters
    //

    private void UpdateFilters(HomePageViewModel.FilterModel model)
    {
        if (model == null)
        {
            return;
        }

        BindDropDownButton(ViewTypeDropDownButton, ViewTypeDropDownButtonText, model.ViewTypeDropDown, ViewModel.SelectViewType);
        BindDropDownButton(SortAndGroupDropDownButton, SortAndGroupDropDownButtonText, model.SortAndGroupDropDown, ViewModel.SelectSortOrGroup);
        BindDropDownButton(FilterPresetDropDownButton, FilterPresetDropDownButtonText, model.FilterPresetDropDown, ViewModel.SelectFilterPreset);
    }

    private void BindDropDownButton<T>(DropDownButton button, TextBlock buttonText, HomePageViewModel.DropDownButtonModel<T> model, Action<T?> clickHandler)
    {
        if (button == null)
        {
            return;
        }

        buttonText.Text = model.Name;

        FlyoutBase flyout = button.Flyout;
        if (flyout is not MenuFlyout)
        {
            flyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
            };
            button.Flyout = flyout;
        }
        var menuFlyout = (MenuFlyout)flyout;

        menuFlyout.Items.Clear();
        foreach (HomePageViewModel.MenuFlyoutItemModel<T> item in model.Items)
        {
            menuFlyout.Items.Add(CreateMenuFlyoutItem(item, clickHandler));
        }
    }

    private MenuFlyoutItemBase CreateMenuFlyoutItem<T>(HomePageViewModel.MenuFlyoutItemModel<T> item, Action<T?> clickHandler)
    {
        MenuFlyoutItemBase menuItem;
        if (item.IsSeperator)
        {
            menuItem = new MenuFlyoutSeparator();
        }
        else if (item.SubItems != null)
        {
            var actualMenuItem = new MenuFlyoutSubItem
            {
                Text = item.Name,
            };
            foreach (HomePageViewModel.MenuFlyoutItemModel<T> subItem in item.SubItems)
            {
                actualMenuItem.Items.Add(CreateMenuFlyoutItem(subItem, clickHandler));
            }
            menuItem = actualMenuItem;
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
        return menuItem;
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    private void OnComicDataUpdated()
    {
        ViewModel.UpdateLibrary();
    }

    //
    // Comic Item
    //

    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped() || ViewModel.IsSelectMode)
        {
            return;
        }

        var item = (ComicItemViewModel)((Grid)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        ViewModel.ApplyOperationToComic(ComicOperationType.Favorite, item);
    }

    private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        ViewModel.ApplyOperationToComic(ComicOperationType.Unfavorite, item);
    }

    private void OnHideComicClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        ViewModel.ApplyOperationToComic(ComicOperationType.Hide, item);
    }

    private void EditFilterButton_Click(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var dialog = new EditFilterDialog(await ViewModel.GetFilter());
            _ = await dialog.ShowAsync(XamlRoot);
            ViewModel.UpdateFilters();
        });
    }

    private void OnEditComicInfoClick(ComicItemViewModel item)
    {
        List<ComicModel> selection = ViewModel.GetSelection(item).ConvertAll(x => x.Comic);
        C0.Run(async () =>
        {
            var dialog = new EditComicInfoDialog(selection);
            ContentDialogResult result = await dialog.ShowAsync(XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.UpdateLibrary();
                ViewModel.UpdateFilters();
            }
        });
    }

    private void AddNewFolder()
    {
        C0.Run(async delegate
        {
            StorageFolder? folder = await FilePickerUtils.PickFolder(WindowId);
            if (folder == null)
            {
                return;
            }

            AppSettingsModel.Instance.AddComicFolder(folder.Path);
            ComicModel.UpdateAllComics("HomePage#AddNewFolder", skipExistingLocation: true);
        });
    }

    private void AddFolderHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        AddNewFolder();
    }

    private void RefreshHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ComicModel.UpdateAllComics("RefreshPage");
    }

    //
    // More actions
    //

    private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CollapseAllGroups();
    }

    private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ExpandAllGroups();
    }

    private void OpenRandomComicButton_Click(object sender, RoutedEventArgs e)
    {
        ComicModel? comic = ViewModel.GetRandomComic();
        if (comic == null)
        {
            return;
        }

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    //
    // Helper Class
    //

    private class ComicItemHandler(HomePage page) : IComicItemViewHandler
    {
        private readonly WeakReference<HomePage> _pageRef = new(page);

        public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            GetPage()?.OnAddToFavoritesClicked(sender, e);
        }

        public void OnEditClick(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.OnEditComicInfoClick(item);
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
            GetPage()?.ViewModel?.ApplyOperationToComic(ComicOperationType.MarkAsRead, item);
        }

        public void OnMarkAsReadingClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.ViewModel?.ApplyOperationToComic(ComicOperationType.MarkAsReading, item);
        }

        public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.ViewModel?.ApplyOperationToComic(ComicOperationType.MarkAsUnread, item);
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
            GetPage()?.ViewModel?.SetSelectionMode(true);
        }

        public void OnUnhideClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            GetPage()?.ViewModel?.ApplyOperationToComic(ComicOperationType.Unhide, item);
        }

        private HomePage? GetPage()
        {
            return _pageRef.TryGetTarget(out HomePage? page) ? page : null;
        }
    }
}
