using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Common.Router;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Utils;

namespace ComicReader.Views
{
    internal class FavoritePageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private NavigationPageShared m_NavigationPageShared;
        public NavigationPageShared NavigationPageShared
        {
            get => m_NavigationPageShared;
            set
            {
                m_NavigationPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NavigationPageShared"));
            }
        }

        private bool m_IsEmpty = false;
        public bool IsEmpty
        {
            get => m_IsEmpty;
            set
            {
                m_IsEmpty = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEmpty"));
            }
        }
    }

    sealed internal partial class FavoritePage : StatefulPage
    {
        public FavoritePageShared Shared { get; set; }
        private ObservableCollection<FavoriteItemViewModel> DataSource { get; set; }
        private TabIdentifier _tabId;

        public FavoritePage()
        {
            Shared = new FavoritePageShared();
            DataSource = new ObservableCollection<FavoriteItemViewModel>();

            InitializeComponent();
        }

        public override void OnStart(object p)
        {
            base.OnStart(p);
            var q = (NavigationParams)p;
            _tabId = q.TabId;
            Shared.NavigationPageShared = (NavigationPageShared)q.Params;
        }

        public override void OnResume()
        {
            base.OnResume();
            ObserveData();

            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        private void ObserveData()
        {
            EventBus.Default.With(EventId.SidePaneUpdate).Observe(this, delegate
            {
                Utils.C0.Run(async delegate
                {
                    await Update();
                });
            });
        }

        // utilities
        private async Task Update()
        {
            void helper(List<FavoriteNodeData> it, ObservableCollection<FavoriteItemViewModel> et, FavoriteItemViewModel parent)
            {
                foreach (FavoriteNodeData inode in it)
                {
                    FavoriteNodeType type = inode.Type == "i" ? FavoriteNodeType.Item : FavoriteNodeType.Filter;
                    FavoriteItemViewModel enode = new FavoriteItemViewModel(inode.Name, type, parent);

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
            Shared.IsEmpty = DataSource.Count == 0;
        }

        public async Task Save()
        {
            void helper(List<FavoriteNodeData> it, ObservableCollection<FavoriteItemViewModel> et)
            {
                foreach (FavoriteItemViewModel enode in et)
                {
                    FavoriteNodeData inode = new FavoriteNodeData
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
            Utils.TaskQueue.DefaultQueue.Enqueue(XmlDatabaseManager.SaveSealed(XmlDatabaseItem.Favorites));
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
            Shared.IsEmpty = DataSource.Count == 0;
        }

        private async Task ResetItems()
        {
            async Task<bool> helper(ObservableCollection<FavoriteItemViewModel> root)
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
                        ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
                        Utils.C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
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
            Shared.IsEmpty = DataSource.Count == 0;
        }

        private void CreateNewFolder(ObservableCollection<FavoriteItemViewModel> folder, FavoriteItemViewModel parent)
        {
            string folderName;
            string new_folder_string = Utils.StringResourceProvider.GetResourceString("NewFolder");

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

            Shared.IsEmpty = DataSource.Count == 0;
        }

        private void SortFavorites(ObservableCollection<FavoriteItemViewModel> source)
        {
            Utils.C0.Run(async delegate
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
            Utils.C0.Run(async delegate
            {
                await ResetItems();
            });
        }

        private void MainTreeViewItemPressed(object sender, PointerRoutedEventArgs e)
        {
            // right-click
            Utils.C0.Run(async delegate
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
            Utils.C0.Run(async delegate
            {
                FavoriteItemViewModel item = (FavoriteItemViewModel)e.InvokedItem;

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
                        MainPage.Current.LoadTab(_tabId, ReaderPageTrait.Instance, comic);
                        Shared.NavigationPageShared.IsSidePaneOpen = false;
                    }
                }
            });
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                FavoriteItemViewModel item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await DeleteItem(item);
            });
        }

        private void RenameClick(object sender, RoutedEventArgs e)
        {
            FavoriteItemViewModel item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
            item.IsRenaming = true;
            ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
            Utils.C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
        }

        private void RenameTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var item = (FavoriteItemViewModel)textBox.DataContext;
            item.EditingName = textBox.Text;
        }

        private void RenameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
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
            FavoriteItemViewModel ctx = (FavoriteItemViewModel)args.NewValue;

            if (ctx.IsRenaming)
            {
                TextBox textbox = (TextBox)sender;
                textbox.Focus(FocusState.Programmatic);
                textbox.SelectAll();
            }
        }

        private void NewFolderClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;

                if (item.Type == FavoriteNodeType.Filter)
                {
                    if (!item.Expanded)
                    {
                        item.Expanded = true;
                        Utils.C1<FavoriteItemViewModel>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
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
            Utils.C0.Run(async delegate
            {
                CreateNewFolder(DataSource, null);
                await Save();
            });
        }

        private void MainTreeViewDragItemsCompleted(Microsoft.UI.Xaml.Controls.TreeView sender,
            Microsoft.UI.Xaml.Controls.TreeViewDragItemsCompletedEventArgs args)
        {
            Utils.C0.Run(async delegate
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
            Utils.C0.Run(async delegate
            {
                FavoriteItemViewModel item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
                ComicData comic = await ComicData.FromId(item.Id, "FavoriteOpenInNewTabLoadComic");
                MainPage.Current.LoadTab(null, ReaderPageTrait.Instance, comic);
            });
        }

        private void SortByNameClick(object sender, RoutedEventArgs e)
        {
            FavoriteItemViewModel item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
            ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
            SortFavorites(parent);
        }

        private void RootSortByNameClick(object sender, RoutedEventArgs e)
        {
            SortFavorites(DataSource);
        }
    }
}
