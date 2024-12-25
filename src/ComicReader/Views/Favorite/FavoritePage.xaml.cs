// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Favorite;

internal sealed partial class FavoritePage : BasePage
{
    private const string TAG = "FavoritePage";
    private ObservableCollection<FavoriteItemViewModel> DataSource { get; set; }

    public FavoritePage()
    {
        DataSource = new ObservableCollection<FavoriteItemViewModel>();

        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();

        C0.Run(async delegate
        {
            await Update();
        });
    }

    private void ObserveData()
    {
        EventBus.Default.With(EventId.SidePaneUpdate).Observe(this, delegate
        {
            C0.Run(async delegate
            {
                await Update();
            });
        });
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    // utilities
    private async Task Update()
    {
        void helper(List<FavoriteNodeData> it, ObservableCollection<FavoriteItemViewModel> et, FavoriteItemViewModel parent)
        {
            foreach (FavoriteNodeData inode in it)
            {
                FavoriteNodeType type = inode.Type == "i" ? FavoriteNodeType.Item : FavoriteNodeType.Filter;
                var enode = new FavoriteItemViewModel(inode.Name, type, parent);

                if (type == FavoriteNodeType.Filter)
                {
                    helper(inode.Children, enode.Children, enode);
                }
                else
                {
                    enode.Id = inode.Id;
                }

                et.Add(enode);
            }
        }

        await XmlDatabaseManager.WaitLock();
        DataSource.Clear();
        helper(XmlDatabase.Favorites.RootNodes, DataSource, null);
        XmlDatabaseManager.ReleaseLock();
        UpdateView();
    }

    private void UpdateView()
    {
        TbNoFavorite.Visibility = DataSource.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task Save()
    {
        void helper(List<FavoriteNodeData> it, ObservableCollection<FavoriteItemViewModel> et)
        {
            foreach (FavoriteItemViewModel enode in et)
            {
                var inode = new FavoriteNodeData
                {
                    Type = enode.Type == FavoriteNodeType.Filter ? "f" : "i",
                    Name = enode.Name,
                    Id = enode.Id
                };

                if (enode.Children.Count > 0)
                {
                    helper(inode.Children, enode.Children);
                }

                it.Add(inode);
            }
        }

        await XmlDatabaseManager.WaitLock();
        XmlDatabase.Favorites.RootNodes.Clear();
        helper(XmlDatabase.Favorites.RootNodes, DataSource);
        XmlDatabaseManager.ReleaseLock();
        TaskDispatcher.DefaultQueue.Submit($"{TAG}#Save", XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Favorites));
    }

    private async Task DeleteItem(FavoriteItemViewModel item)
    {
        if (item.Parent != null)
        {
            _ = item.Parent.Children.Remove(item);
        }
        else
        {
            _ = DataSource.Remove(item);
        }

        await Save();
        UpdateView();
    }

    private async Task ResetItems()
    {
        async Task<bool> helper(ObservableCollection<FavoriteItemViewModel> root)
        {
            foreach (FavoriteItemViewModel item in root)
            {
                if (item.IsRenaming)
                {
                    if (item.EditingName.Length == 0)
                    {
                        item.EditingName = item.Name;
                    }
                    else
                    {
                        item.Name = item.EditingName;
                    }

                    item.IsRenaming = false;
                    ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
                    C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
                    await Save();
                    return true;
                }

                if (item.Expanded && item.Type == FavoriteNodeType.Filter)
                {
                    if (await helper(item.Children))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        await helper(DataSource);
        UpdateView();
    }

    private void CreateNewFolder(ObservableCollection<FavoriteItemViewModel> folder, FavoriteItemViewModel parent)
    {
        string folderName;
        string new_folder_string = StringResourceProvider.GetResourceString("NewFolder");

        for (int folderIndex = 1; folderIndex < 65536; ++folderIndex)
        {
            if (folderIndex > 1)
            {
                folderName = new_folder_string + " (" + folderIndex.ToString() + ")";
            }
            else
            {
                folderName = new_folder_string;
            }

            bool isDuplicated = false;

            foreach (FavoriteItemViewModel item in folder)
            {
                if (item.Name == folderName)
                {
                    isDuplicated = true;
                    break;
                }
            }

            if (!isDuplicated)
            {
                var newFolder = new FavoriteItemViewModel(folderName, FavoriteNodeType.Filter, parent)
                {
                    IsRenaming = true
                };

                folder.Add(newFolder);
                break;
            }
        }

        UpdateView();
    }

    private void SortFavorites(ObservableCollection<FavoriteItemViewModel> source)
    {
        C0.Run(async delegate
        {
            var ordered = source.OrderBy(x => x.Name, new StringUtils.OrdinalComparer()).ToList();

            for (int i = 0; i < ordered.Count; ++i)
            {
                FavoriteItemViewModel item = ordered[i];

                if (source.IndexOf(item) == i)
                {
                    continue;
                }

                source.Remove(item);
                source.Insert(i, item);
            }

            await Save();
        });
    }

    // events
    private void MainTreeViewBackgroundPressed(object sender, PointerRoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            await ResetItems();
        });
    }

    private void MainTreeViewItemPressed(object sender, PointerRoutedEventArgs e)
    {
        // right-click
        C0.Run(async delegate
        {
            var item = (Microsoft.UI.Xaml.Controls.TreeViewItem)sender;
            var ctx = (FavoriteItemViewModel)item.DataContext;

            if (ctx.IsRenaming)
            {
                return;
            }

            await ResetItems();
            e.Handled = true;
        });
    }

    private void MainTreeViewItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs e)
    {
        // left-click
        C0.Run(async delegate
        {
            var item = (FavoriteItemViewModel)e.InvokedItem;

            if (item.IsRenaming)
            {
                return;
            }

            await ResetItems();

            if (item.Type == FavoriteNodeType.Item)
            {
                ComicData comic = await ComicData.FromId(item.Id, "FavoriteLoadComic");

                if (comic == null)
                {
                    await DeleteItem(item);
                }
                else
                {
                    Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    GetMainPageAbility().OpenInCurrentTab(route);
                    GetNavigationPageAbility().SetIsSidePaneOpen(false);
                }
            }
        });
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await DeleteItem(item);
        });
    }

    private void RenameClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
        item.IsRenaming = true;
        ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
        C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
    }

    private void RenameTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        var item = (FavoriteItemViewModel)textBox.DataContext;
        item.EditingName = textBox.Text;
    }

    private void RenameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await ResetItems();
                e.Handled = true;
            }
        });
    }

    private void RenameTextBoxDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        var ctx = (FavoriteItemViewModel)args.NewValue;

        if (ctx.IsRenaming)
        {
            var textbox = (TextBox)sender;
            textbox.Focus(FocusState.Programmatic);
            textbox.SelectAll();
        }
    }

    private void NewFolderClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;

            if (item.Type == FavoriteNodeType.Filter)
            {
                if (!item.Expanded)
                {
                    item.Expanded = true;
                    C1<FavoriteItemViewModel>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
                }

                CreateNewFolder(item.Children, item);
            }
            else
            {
                if (item.Parent != null)
                {
                    CreateNewFolder(item.Parent.Children, item.Parent);
                }
                else
                {
                    CreateNewFolder(DataSource, null);
                }
            }

            await Save();
        });
    }

    private void RootNewFolderClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            CreateNewFolder(DataSource, null);
            await Save();
        });
    }

    private void MainTreeViewDragItemsCompleted(Microsoft.UI.Xaml.Controls.TreeView sender,
        Microsoft.UI.Xaml.Controls.TreeViewDragItemsCompletedEventArgs args)
    {
        C0.Run(async delegate
        {
            var parent = (FavoriteItemViewModel)args.NewParentItem;

            foreach (FavoriteItemViewModel item in args.Items.Cast<FavoriteItemViewModel>())
            {
                item.Parent = parent;
            }

            await Save();
        });
    }

    private void OpenInNewTabClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
            ComicData comic = await ComicData.FromId(item.Id, "FavoriteOpenInNewTabLoadComic");
            Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            GetMainPageAbility().OpenInCurrentTab(route);
            MainPage.Current.OpenInNewTab(route);
        });
    }

    private void SortByNameClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
        ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
        SortFavorites(parent);
    }

    private void RootSortByNameClick(object sender, RoutedEventArgs e)
    {
        SortFavorites(DataSource);
    }
}
