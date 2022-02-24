using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Views
{
    public sealed partial class ChooseLocationsDialog : ContentDialog
    {
        public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }

        public ChooseLocationsDialog()
        {
            FolderItemDataSource = new ObservableCollection<FolderItemViewModel>();

            InitializeComponent();
        }

        // utilities
        private async Task Update()
        {
            FolderItemDataSource.Clear();

            if (StorageApplicationPermissions.FutureAccessList.Entries.Count <
                StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed)
            {
                FolderItemDataSource.Add(new FolderItemViewModel
                {
                    IsAddNew = true
                });
            }

            await XmlDatabaseManager.WaitLock();

            foreach (string folder in XmlDatabase.Settings.ComicFolders)
            {
                FolderItemDataSource.Add(new FolderItemViewModel
                {
                    Folder = folder,
                    IsAddNew = false
                });
            }

            XmlDatabaseManager.ReleaseLock();
        }

        private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Update comics.
            Utils.TaskQueueManager.NewTask(ComicData.Manager.UpdateSealed(lazy_load: true));
        }

        private void ListViewLoaded(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        private void AddNewPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Add a folder.
            Utils.C0.Run(async delegate
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
            Utils.C0.Run(async delegate
            {
                if (!IsPrimaryButtonEnabled)
                {
                    return;
                }

                IsPrimaryButtonEnabled = false;
                FolderItemViewModel item = (FolderItemViewModel)((Grid)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(item.Folder, final: true);
                await Update();
                IsPrimaryButtonEnabled = true;
            });
        }
    }
}
