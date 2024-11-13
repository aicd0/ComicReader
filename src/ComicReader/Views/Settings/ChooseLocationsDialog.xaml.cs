// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Database;
using ComicReader.DesignData;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Settings;

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

        if (Utils.Storage.AllowAddToFutureAccessList())
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
        ComicData.UpdateAllComics("ContentDialogPrimaryButtonClick", lazy: true);
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
            var item = (FolderItemViewModel)((Grid)sender).DataContext;
            await SettingDataManager.RemoveComicFolder(item.Folder, final: true);
            await Update();
            IsPrimaryButtonEnabled = true;
        });
    }
}
