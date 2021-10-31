using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public class FavoritesPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ContentPageShared m_ContentPageShared;
        public ContentPageShared ContentPageShared
        {
            get => m_ContentPageShared;
            set
            {
                m_ContentPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentPageShared"));
            }
        }
    }

    public sealed partial class FavoritesPage : Page
    {
        public static FavoritesPage Current = null;
        public FavoritesPageShared Shared { get; set; }
        private ObservableCollection<FavoritesItemModel> DataSource { get; set; }

        private Utils.Tab.TabManager m_tab_manager;

        public FavoritesPage()
        {
            Current = this;
            Shared = new FavoritesPageShared();
            DataSource = new ObservableCollection<FavoritesItemModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnPageEntered = OnPageEntered;

            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_manager.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            m_tab_manager.OnNavigatedFrom(e);
        }

        private void OnSetShared(object shared)
        {
            Shared.ContentPageShared = (ContentPageShared)shared;
        }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await Update();
            });
        }

        // utilities
        public async Task Update()
        {
            void helper(List<FavoritesNodeData> it, ObservableCollection<FavoritesItemModel> et, FavoritesItemModel parent)
            {
                foreach (FavoritesNodeData inode in it)
                {
                    TreeItemType type = inode.Type == "i" ? TreeItemType.Item : TreeItemType.Filter;
                    FavoritesItemModel enode = new FavoritesItemModel(inode.Name, type, parent);

                    if (type == TreeItemType.Filter)
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

            await DatabaseManager.WaitLock();
            DataSource.Clear();
            helper(Database.Favorites.RootNodes, DataSource, null);
            DatabaseManager.ReleaseLock();
        }

        public async Task Save()
        {
            void helper(List<FavoritesNodeData> it, ObservableCollection<FavoritesItemModel> et)
            {
                foreach (FavoritesItemModel enode in et)
                {
                    FavoritesNodeData inode = new FavoritesNodeData();
                    inode.Type = enode.Type == TreeItemType.Filter ? "f" : "i";
                    inode.Name = enode.Name;
                    inode.Id = enode.Id;

                    if (enode.Children.Count > 0)
                    {
                        helper(inode.Children, enode.Children);
                    }

                    it.Add(inode);
                }
            }

            await DatabaseManager.WaitLock();
            Database.Favorites.RootNodes.Clear();
            helper(Database.Favorites.RootNodes, DataSource);
            DatabaseManager.ReleaseLock();
            Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.Favorites));
        }

        private async Task DeleteItem(FavoritesItemModel item)
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
        }

        private async Task ResetItems()
        {
            async Task<bool> helper(ObservableCollection<FavoritesItemModel> root)
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
                        ObservableCollection<FavoritesItemModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
                        Utils.Methods1<FavoritesItemModel>.NotifyCollectionChanged(parent, item);
                        await Save();
                        return true;
                    }

                    if (item.Expanded && item.Type == TreeItemType.Filter)
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
        }

        private void CreateNewFolder(ObservableCollection<FavoritesItemModel> folder, FavoritesItemModel parent)
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
                    var newFolder = new FavoritesItemModel(folderName, TreeItemType.Filter, parent)
                    {
                        IsRenaming = true
                    };

                    folder.Add(newFolder);
                    break;
                }
            }
        }

        private void SortFavorites(ObservableCollection<FavoritesItemModel> source)
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

                await Save();
            });
        }

        // events
        private void MainTreeViewBackgroundPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await ResetItems();
            });
        }

        private void MainTreeViewItemPressed(object sender, PointerRoutedEventArgs e)
        {
            // right-click
            Utils.Methods.Run(async delegate
            {
                var item = (Microsoft.UI.Xaml.Controls.TreeViewItem)sender;
                var ctx = (FavoritesItemModel)item.DataContext;

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
            Utils.Methods.Run(async delegate
            {
                FavoritesItemModel item = (FavoritesItemModel)e.InvokedItem;

                if (item.IsRenaming)
                {
                    return;
                }

                await ResetItems();

                if (item.Type == TreeItemType.Item)
                {
                    ComicItemData comic = await ComicDataManager.FromId(item.Id);

                    if (comic == null)
                    {
                        await DeleteItem(item);
                    }
                    else
                    {
                        RootPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Reader, comic);
                        Shared.ContentPageShared.PaneOpen = false;
                    }
                }
            });
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FavoritesItemModel item = (FavoritesItemModel)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void RenameClick(object sender, RoutedEventArgs e)
        {
            FavoritesItemModel item = (FavoritesItemModel)((MenuFlyoutItem)sender).DataContext;
            item.IsRenaming = true;
            ObservableCollection<FavoritesItemModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
            Utils.Methods1<FavoritesItemModel>.NotifyCollectionChanged(parent, item);
        }

        private void RenameTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var item = (FavoritesItemModel)textBox.DataContext;
            item.EditingName = textBox.Text;
        }

        private void RenameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
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
            FavoritesItemModel ctx = (FavoritesItemModel)args.NewValue;

            if (ctx.IsRenaming)
            {
                TextBox textbox = (TextBox)sender;
                textbox.Focus(FocusState.Programmatic);
                textbox.SelectAll();
            }
        }

        private void NewFolderClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                var item = (FavoritesItemModel)((MenuFlyoutItem)sender).DataContext;

                if (item.Type == TreeItemType.Filter)
                {
                    if (!item.Expanded)
                    {
                        item.Expanded = true;
                        Utils.Methods1<FavoritesItemModel>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
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
            Utils.Methods.Run(async delegate
            {
                CreateNewFolder(DataSource, null);
                await Save();
            });
        }

        private void MainTreeViewDragItemsCompleted(Microsoft.UI.Xaml.Controls.TreeView sender,
            Microsoft.UI.Xaml.Controls.TreeViewDragItemsCompletedEventArgs args)
        {
            Utils.Methods.Run(async delegate
            {
                var parent = (FavoritesItemModel)args.NewParentItem;

                foreach (FavoritesItemModel item in args.Items)
                {
                    item.Parent = parent;
                }

                await Save();
            });
        }

        private void OpenInNewTabClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FavoritesItemModel item = (FavoritesItemModel)((MenuFlyoutItem)sender).DataContext;
                ComicItemData comic = await ComicDataManager.FromId(item.Id);
                RootPage.Current.LoadTab(null, Utils.Tab.PageType.Reader, comic);
            });
        }

        private void SortByNameClick(object sender, RoutedEventArgs e)
        {
            FavoritesItemModel item = (FavoritesItemModel)((MenuFlyoutItem)sender).DataContext;
            ObservableCollection<FavoritesItemModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
            SortFavorites(parent);
        }

        private void RootSortByNameClick(object sender, RoutedEventArgs e)
        {
            SortFavorites(DataSource);
        }
    }
}
