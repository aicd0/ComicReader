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

            await DataManager.WaitLock();

            // remove unnecessary folders from future access list
            if (StorageApplicationPermissions.FutureAccessList.Entries.Count > Database.AppSettings.ComicFolders.Count)
            {
                List<string> tokens = new List<string>(StorageApplicationPermissions.FutureAccessList.Entries.Count);

                foreach (AccessListEntry entry in StorageApplicationPermissions.FutureAccessList.Entries)
                {
                    tokens.Add(entry.Token);
                }

                List<string> tokens_in_lib = new List<string>(Database.AppSettings.ComicFolders.Count);

                foreach (string path in Database.AppSettings.ComicFolders)
                {
                    tokens_in_lib.Add(Utils.StringUtils.TokenFromPath(path));
                }

                string[] tokens_remove = tokens.Except(tokens_in_lib).ToArray();

                foreach (string token in tokens_remove)
                {
                    StorageApplicationPermissions.FutureAccessList.Remove(token);
                }
            }

            if (StorageApplicationPermissions.FutureAccessList.Entries.Count < StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed)
            {
                FolderItemDataSource.Add(new FolderItemModel
                {
                    IsAddNew = true
                });
            }

            foreach (string folder in Database.AppSettings.ComicFolders)
            {
                FolderItemModel item = new FolderItemModel
                {
                    Folder = folder,
                    IsAddNew = false
                };
                FolderItemDataSource.Add(item);
            }

            DataManager.ReleaseLock();
        }

        // comfirm changes
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Utils.BackgroundTasks.AppendTask(DataManager.UpdateComicDataSealed(), "", Utils.BackgroundTasks.EmptyQueue());
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
                await DataManager.UtilsRemoveFromFolders(item.Folder);
                await UpdateFolders();

                IsPrimaryButtonEnabled = true;
            });
        }
    }
}
