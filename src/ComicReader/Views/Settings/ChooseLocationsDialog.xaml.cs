// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models.Comic;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Settings;

public sealed partial class ChooseLocationsDialog : BaseContentDialog
{
    public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }

    private int WindowId { get; }

    public ChooseLocationsDialog(int windowId)
    {
        InitializeComponent();
        FolderItemDataSource = new ObservableCollection<FolderItemViewModel>();
        WindowId = windowId;
    }

    // utilities
    private async Task Update()
    {
        FolderItemDataSource.Clear();

        FolderItemDataSource.Add(new FolderItemViewModel
        {
            IsAddNew = true
        });

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
        ComicModel.UpdateAllComics("ContentDialogPrimaryButtonClick", lazy: true);
    }

    private void ListViewLoaded(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            await Update();
        });
    }

    private void AddNewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Add a folder.
        C0.Run(async delegate
        {
            if (!IsPrimaryButtonEnabled)
            {
                return;
            }

            IsPrimaryButtonEnabled = false;

            try
            {
                if (!await SettingDataManager.AddComicFolderUsingPicker(WindowId))
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
        C0.Run(async delegate
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
