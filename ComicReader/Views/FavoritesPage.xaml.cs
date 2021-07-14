using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using ComicReader.Data;

namespace ComicReader.Views
{
    public sealed partial class FavoritesPage : Page
    {
        public static FavoritesPage Current = null;
        ObservableCollection<FavoritesItem> DataSource { get; set; }

        public FavoritesPage()
        {
            Current = this;
            DataSource = new ObservableCollection<FavoritesItem>();

            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Utils.Methods.Run(async delegate
            {
                await UpdateTreeExplorer();
            });
        }

        public async Task UpdateTreeExplorer()
        {
            DataSource.Clear();
            await DataManager.WaitLock();
            UpdateTreeExplorerHelper(Database.Favorites, DataSource, null);
            DataManager.ReleaseLock();
        }

        private void UpdateTreeExplorerHelper(List<FavoritesNode> it, ObservableCollection<FavoritesItem> et, FavoritesItem parent)
        {
            foreach (var inode in it) {
                TreeItemType type = inode.Type == "i" ? TreeItemType.Item : TreeItemType.Filter;
                var enode = new FavoritesItem(inode.Name, type, parent);

                if (type == TreeItemType.Filter)
                {
                    UpdateTreeExplorerHelper(inode.Children, enode.Children, enode);
                }
                else
                {
                    enode.Id = inode.Id;
                }

                et.Add(enode);
            }
        }

        public async Task SaveTreeExplorer()
        {
            await DataManager.WaitLock();
            Database.Favorites.Clear();
            SaveTreeExplorerHelper(Database.Favorites, DataSource);
            DataManager.ReleaseLock();
            Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Favorites));
        }

        private void SaveTreeExplorerHelper(List<FavoritesNode> it, ObservableCollection<FavoritesItem> et)
        {
            foreach (var enode in et)
            {
                FavoritesNode inode = new FavoritesNode();
                inode.Type = enode.Type == TreeItemType.Filter ? "f" : "i";
                inode.Name = enode.Name;
                inode.Id = enode.Id;

                if (enode.Children.Count > 0)
                {
                    SaveTreeExplorerHelper(inode.Children, enode.Children);
                }

                it.Add(inode);
            }
        }

        private void TreeExplorer_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await UpdateTreeExplorer();
            });
        }

        private void TreeViewBackground_Pressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await ResetItemStatus();
            });
        }

        // 处理右键事件
        private void Item_Pressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                var item = (Microsoft.UI.Xaml.Controls.TreeViewItem)sender;
                var itemData = (FavoritesItem)item.DataContext;

                if (itemData.IsRenaming)
                {
                    return;
                }

                await ResetItemStatus();
                e.Handled = true;
            });
        }

        // 处理左键事件
        private void Item_Invoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FavoritesItem item = (FavoritesItem)e.InvokedItem;

                if (item.IsRenaming)
                {
                    return;
                }

                await ResetItemStatus();

                if (item.Type == TreeItemType.Item)
                {
                    ComicData comic = await DataManager.GetComicWithId(item.Id);

                    if (comic == null)
                    {
                        await DeleteItem(item);
                    }
                    else
                    {
                        await RootPage.Current.LoadTab(null, PageType.Reader, comic);
                    }
                }
            });
        }

        private async Task DeleteItem(FavoritesItem item)
        {
            if (item.Parent != null)
            {
                _ = item.Parent.Children.Remove(item);
            }
            else
            {
                _ = DataSource.Remove(item);
            }

            await SaveTreeExplorer();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FavoritesItem item = (FavoritesItem)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var item = (FavoritesItem)((MenuFlyoutItem)sender).DataContext;
            item.IsRenaming = true;
            Utils.T1<FavoritesItem>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
        }

        public async Task ResetItemStatus()
        {
            await ResetItemStatusHelper(DataSource);
        }

        private async Task<bool> ResetItemStatusHelper(ObservableCollection<FavoritesItem> root)
        {
            foreach (var item in root)
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

                    Utils.T1<FavoritesItem>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);

                    await SaveTreeExplorer();

                    return true;
                }
                if (item.IsExpanded && item.Type == TreeItemType.Filter)
                {
                    if (await ResetItemStatusHelper(item.Children))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void RenameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var item = (FavoritesItem)textBox.DataContext;

            item.EditingName = textBox.Text;
        }

        private void RenameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    await ResetItemStatus();
                    e.Handled = true;
                }
            });
        }

        private void CreateNewFolder(ObservableCollection<FavoritesItem> folder, FavoritesItem parent)
        {
            string folderName;
            for (int folderIndex = 1; folderIndex < 65536; ++folderIndex)
            {
                if (folderIndex > 1)
                {
                    folderName = "New folder (" + folderIndex.ToString();
                    folderName = folderName + ")";
                }
                else
                {
                    folderName = "New folder";
                }

                bool isDuplicated = false;
                foreach (var i in folder)
                {
                    if (i.Name == folderName)
                    {
                        isDuplicated = true;
                        break;
                    }
                }

                if (!isDuplicated)
                {
                    var newFolder = new FavoritesItem(folderName, TreeItemType.Filter, parent);
                    newFolder.IsRenaming = true;
                    folder.Add(newFolder);
                    break;
                }
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                var item = (FavoritesItem)((MenuFlyoutItem)sender).DataContext;

                if (item.Type == TreeItemType.Filter)
                {
                    if (!item.IsExpanded)
                    {
                        item.IsExpanded = true;
                        Utils.T1<FavoritesItem>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
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

                await SaveTreeExplorer();
            });
        }

        private void NewFolder_Click2(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                CreateNewFolder(DataSource, null);
                await SaveTreeExplorer();
            });
        }

        private void TreeExplorer_DragItemsCompleted(Microsoft.UI.Xaml.Controls.TreeView sender,
            Microsoft.UI.Xaml.Controls.TreeViewDragItemsCompletedEventArgs args)
        {
            Utils.Methods.Run(async delegate
            {
                var parent = (FavoritesItem)args.NewParentItem;

                foreach (FavoritesItem item in args.Items)
                {
                    item.Parent = parent;
                }

                await SaveTreeExplorer();
            });
        }

        private void OpenInNewTabClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FavoritesItem item = (FavoritesItem)((MenuFlyoutItem)sender).DataContext;
                ComicData comic = await DataManager.GetComicWithId(item.Id);
                await RootPage.Current.LoadTab(null, PageType.Reader, comic);
            });
        }

        private void SortByNameClick(object sender, RoutedEventArgs e)
        {
            var item = (FavoritesItem)((MenuFlyoutItem)sender).DataContext;
            ObservableCollection<FavoritesItem> parent = item.Parent != null ? item.Parent.Children : DataSource;
            SortFavorites(parent);
        }

        private void SortByNameClick2(object sender, RoutedEventArgs e)
        {
            SortFavorites(DataSource);
        }

        private void SortFavorites(ObservableCollection<FavoritesItem> source)
        {
            Utils.Methods.Run(async delegate
            {
                var ordered = source.OrderBy(x => x.Name, new Utils.StringUtils.OrdinalComparer()).ToList();

                for (int i = 0; i < ordered.Count; ++i)
                {
                    var item = ordered[i];

                    if (source.IndexOf(item) == i)
                    {
                        continue;
                    }

                    source.Remove(item);
                    source.Insert(i, item);
                }

                await SaveTreeExplorer();
            });
        }
    }
}
