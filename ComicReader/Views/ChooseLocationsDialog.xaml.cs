using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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

            await DatabaseManager.WaitLock();

            foreach (string folder in Database.AppSettings.ComicFolders)
            {
                FolderItemDataSource.Add(new FolderItemModel
                {
                    Folder = folder,
                    IsAddNew = false
                });
            }

            DatabaseManager.ReleaseLock();
        }

        // comfirm changes
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                ComicDataManager.UpdateSealed(), "",
                Utils.TaskQueue.TaskQueueManager.EmptyQueue());
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
                    if (!await AppSettingsDataManager.AddComicFolderUsingPicker())
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
                await AppSettingsDataManager.RemoveComicFolder(item.Folder);
                await UpdateFolders();
                IsPrimaryButtonEnabled = true;
            });
        }
    }
}
