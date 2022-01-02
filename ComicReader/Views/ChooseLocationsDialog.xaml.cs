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

        // utilities
        private async Task Update()
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

            await XmlDatabaseManager.WaitLock();

            foreach (string folder in XmlDatabase.Settings.ComicFolders)
            {
                FolderItemDataSource.Add(new FolderItemModel
                {
                    Folder = folder,
                    IsAddNew = false
                });
            }

            XmlDatabaseManager.ReleaseLock();
        }

        // events
        private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // comfirm changes
            Utils.TaskQueue.TaskQueueManager.AppendTask(
                DatabaseManager.UpdateSealed(lazy_load: false), "",
                Utils.TaskQueue.TaskQueueManager.EmptyQueue());
        }

        private void ListViewLoaded(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await Update();
            });
        }

        private void AddNewPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // add a folder
            Utils.Methods.Run(async delegate
            {
                if (!IsPrimaryButtonEnabled)
                {
                    return;
                }

                IsPrimaryButtonEnabled = false;

                try
                {
                    if (!await SettingDataManager.AddComicFolderUsingPicker())
                    {
                        return;
                    }

                    await Update();
                }
                finally
                {
                    IsPrimaryButtonEnabled = true;
                }
            });
        }

        private void RemoveFolderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // remove a folder
            Utils.Methods.Run(async delegate
            {
                if (!IsPrimaryButtonEnabled)
                {
                    return;
                }

                IsPrimaryButtonEnabled = false;
                FolderItemModel item = (FolderItemModel)((Grid)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(item.Folder);
                await Update();
                IsPrimaryButtonEnabled = true;
            });
        }
    }
}
