using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Core;
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
    public sealed partial class ChooseLocationsDialog : ContentDialog
    {
        public ObservableCollection<FolderItemModel> FolderItemDataSource { get; set; }

        public ChooseLocationsDialog()
        {
            FolderItemDataSource = new ObservableCollection<FolderItemModel>();
            InitializeComponent();
        }

        // user-defined functions
        // update
        private async Task UpdateFolders()
        {
            FolderItemDataSource.Clear();

            if (StorageApplicationPermissions.FutureAccessList.Entries.Count <
                StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed)
            {
                FolderItemDataSource.Add(new FolderItemModel
                {
                    IsAddNew = true
                });
            }

            await DataManager.WaitLock();

            foreach (string folder in Database.AppSettings.ComicFolders)
            {
                FolderItemDataSource.Add(new FolderItemModel
                {
                    Folder = folder,
                    IsAddNew = false
                });
            }

            DataManager.ReleaseLock();
        }

        // comfirm changes
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Utils.BackgroundTasks.AppendTask(DataManager.UpdateComicDataSealed(),
                "", Utils.BackgroundTasks.EmptyQueue());
        }

        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await UpdateFolders();
            });
        }

        // add a folder
        private void AddNewPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (!IsPrimaryButtonEnabled)
                {
                    return;
                }

                IsPrimaryButtonEnabled = false;

                try
                {
                    if (!await DataManager.UtilsAddToComicFoldersUsingPicker())
                    {
                        return;
                    }

                    await UpdateFolders();
                }
                finally
                {
                    IsPrimaryButtonEnabled = true;
                }
            });
        }

        // remove a folder
        private void RemoveFolderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (!IsPrimaryButtonEnabled)
                {
                    return;
                }

                IsPrimaryButtonEnabled = false;
                FolderItemModel item = (FolderItemModel)((Grid)sender).DataContext;
                await DataManager.RemoveFromComicFolders(item.Folder);
                await UpdateFolders();
                IsPrimaryButtonEnabled = true;
            });
        }
    }
}
