// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Imaging;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Comic;
using ComicReader.Data.SqlHelpers;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.Navigation;
using ComicReader.ViewModels;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Home;

internal sealed partial class HomePage : BasePage
{
    private readonly ComicItemViewModel.IItemHandler _comicItemHandler;
    private ObservableCollectionPlus<ComicItemViewModel> ComicItemSource { get; set; }
        = new ObservableCollectionPlus<ComicItemViewModel>();
    public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }
        = new ObservableCollection<FolderItemViewModel>();

    private readonly CancellationLock m_update_folder_lock = new();
    private readonly CancellationLock _updateLibraryLock = new();
    private readonly CancellationSession _updateLibrarySession = new();

    public HomePage()
    {
        _comicItemHandler = new ComicItemHandler(this);
        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ComicData.OnUpdated += OnComicDataUpdated;
        GetMainPageAbility().SetTitle(StringResourceProvider.GetResourceString("NewTab"));
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Document });

        C0.Run(async delegate
        {
            await Update();
        });
    }

    protected override void OnPause()
    {
        base.OnPause();
        ComicData.OnUpdated -= OnComicDataUpdated;
        _updateLibrarySession.Next();
    }

    // Utilities
    private async Task Update()
    {
        await UpdateFolders();
        await UpdateLibrary();
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private void BindComicData(ComicItemViewModel model, ComicData comic)
    {
        model.Comic = comic;
        model.Title = comic.Title;
        model.Rating = comic.Rating;
        model.UpdateProgress(true);
        model.IsFavorite = FavoriteDataManager.FromIdNoLock(comic.Id) != null;
        model.ItemHandler = _comicItemHandler;
    }

    private void OnComicDataUpdated()
    {
        MainThreadUtils.RunInMainThreadAsync(UpdateLibrary).Wait();
    }

    public async Task UpdateLibrary()
    {
        await _updateLibraryLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            if (token.CancellationRequested)
            {
                return;
            }

            // Get recent visited comics.
            var records = new FixedHeap<Tuple<long, DateTimeOffset>>(100,
                (Tuple<long, DateTimeOffset> x, Tuple<long, DateTimeOffset> y) => { return x.Item2.CompareTo(y.Item2); });

            await ComicData.EnqueueCommand(delegate
            {
                // Use ORDER BY here will cause a crash (especially for a large result set)
                // due to https://github.com/dotnet/efcore/issues/20044.
                // Switch from Microsoft.Data.Sqlite to SQLitePCLRaw.bundle_winsqlite3 will
                // solve the issue but the app then cannot not be built in Release mode.
                // (See https://github.com/ericsink/SQLitePCL.raw/issues/346)
                // A workaround here is to sort the data manually.

                // command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                //     " ORDER BY " + ComicData.Field.LastVisit + " DESC";

                var command = new SelectCommand<ComicTable>(ComicTable.Instance);
                SelectCommand<ComicTable>.IToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
                SelectCommand<ComicTable>.IToken<bool> hiddenToken = command.PutQueryBoolean(ComicTable.ColumnHidden);
                SelectCommand<ComicTable>.IToken<DateTimeOffset> lastVisitToken = command.PutQueryDateTimeOffset(ComicTable.ColumnLastVisit);
                using SelectCommand<ComicTable>.IReader reader = command.Execute();

                while (reader.Read())
                {
                    bool hidden = hiddenToken.GetValue();

                    if (!hidden)
                    {
                        records.Add(new Tuple<long, DateTimeOffset>
                        (
                            idToken.GetValue(),
                            lastVisitToken.GetValue()
                        ));
                    }
                }
            }, "HomeLoadLibrary");

            // Convert to view models.
            var comic_items = new List<ComicItemViewModel>();

            foreach (Tuple<long, DateTimeOffset> record in records.GetSorted())
            {
                ComicData comic = await ComicData.FromId(record.Item1, "HomeLoadComic");

                if (comic == null)
                {
                    continue;
                }

                var model = new ComicItemViewModel();
                BindComicData(model, comic);
                comic_items.Add(model);
            }

            // Save results.
            C1<ComicItemViewModel>.UpdateCollection(ComicItemSource, comic_items,
                (ComicItemViewModel x, ComicItemViewModel y) =>
                x.Comic.Title == y.Comic.Title &&
                x.Rating == y.Rating &&
                x.Progress == y.Progress &&
                x.IsFavorite == y.IsFavorite);
            bool isEmpty = ComicItemSource.Count == 0;
            HbShowAllButton.IsEnabled = !isEmpty;
            SpLibraryEmpty.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void LoadImage(ComicItemVertical viewHolder, ComicItemViewModel item)
    {
        double image_width = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"] - 40.0;
        double image_height = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>();

        if (item.Image.ImageSet)
        {
            return;
        }

        item.Image.ImageSet = true;
        tokens.Add(new SimpleImageLoader.Token
        {
            Width = image_width,
            Height = image_height,
            Multiplication = 1.4,
            Source = new ComicCoverImageSource(item.Comic),
            ImageResultHandler = new LoadImageCallback(viewHolder, item)
        });

        new SimpleImageLoader.Transaction(_updateLibrarySession.Token, tokens).Commit();
    }

    private class LoadImageCallback : IImageResultHandler
    {
        private readonly ComicItemVertical _viewHolder;
        private readonly ComicItemViewModel _viewModel;

        public LoadImageCallback(ComicItemVertical viewHolder, ComicItemViewModel viewModel)
        {
            _viewHolder = viewHolder;
            _viewModel = viewModel;
        }

        public void OnSuccess(BitmapImage image)
        {
            _viewModel.Image.Image = image;
            _viewHolder.CompareAndBind(_viewModel);
        }
    }

    public async Task UpdateFolders()
    {
        await m_update_folder_lock.LockAsync(async delegate (CancellationLock.Token token)
        {
            // Add to folder item source.
            var new_folder_source = new Collection<FolderItemViewModel>
            {
                new() {
                    OnItemTapped = OnFolderItemTapped,
                    IsAddNew = true
                }
            };

            await XmlDatabaseManager.WaitLock();

            foreach (string path in XmlDatabase.Settings.ComicFolders)
            {
                var item = new FolderItemViewModel
                {
                    OnItemTapped = OnFolderItemTapped,
                    OnRemoveClicked = FolderItemRemoveClick,
                    Folder = StringUtils.ItemNameFromPath(path),
                    Path = path,
                    IsAddNew = false
                };

                new_folder_source.Add(item);
            }

            XmlDatabaseManager.ReleaseLock();
            C1<FolderItemViewModel>.UpdateCollection(FolderItemDataSource, new_folder_source, FolderItemViewModel.ContentEquals);
        });
    }

    // Events
    private void OnAdaptiveGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ComicItemViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ComicItemVertical;
        if (args.InRecycleQueue)
        {
            item.Image.ImageSet = false;
        }

        viewHolder.Bind(item);
        if (!args.InRecycleQueue)
        {
            LoadImage(viewHolder, item);
        }
    }

    private void OnSeeAllBtClicked(object sender, RoutedEventArgs e)
    {
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, "<all>");
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void OnSeeHiddenBtClick(object sender, RoutedEventArgs e)
    {
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, "<hidden>");
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        MainPage.Current.OpenInNewTab(route);
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        var item = (ComicItemViewModel)((Grid)sender).DataContext;
        Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void MarkAsReadOrUnread(ComicItemViewModel item, bool read)
    {
        if (read)
        {
            item.Comic.SetAsRead();
        }
        else
        {
            item.Comic.SetAsUnread();
        }
        BindComicData(item, item.Comic);
    }

    private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            item.IsFavorite = true;
            await FavoriteDataManager.Add(item.Comic.Id, item.Title, true);
        });
    }

    private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            item.IsFavorite = false;
            await FavoriteDataManager.RemoveWithId(item.Comic.Id, true);
        });
    }

    private void OnHideComicClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await item.Comic.SaveHiddenAsync(true);
            ComicItemSource.Remove(item);
            await UpdateLibrary();
        });
    }

    private void AddNewFolder()
    {
        C0.Run(async delegate
        {
            if (!await SettingDataManager.AddComicFolderUsingPicker())
            {
                return;
            }

            await UpdateFolders();
            ComicData.UpdateAllComics("HomePage#AddNewFolder", lazy: true);
        });
    }

    private void OnFolderItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        var item = (FolderItemViewModel)((Grid)sender).DataContext;
        if (item.IsAddNew)
        {
            AddNewFolder();
        }
        else
        {
            Route route = new Route(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, "<dir: " + item.Path + ">");
            GetMainPageAbility().OpenInCurrentTab(route);
        }
    }

    private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var item = (FolderItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await SettingDataManager.RemoveComicFolder(item.Path, final: true);
            await UpdateFolders();
            ComicData.UpdateAllComics("FolderItemRemoveClick", lazy: true);
        });
    }

    private void OnTryAddFolderBtClicked(object sender, RoutedEventArgs e)
    {
        AddNewFolder();
    }

    private void OnRefreshBtClicked(object sender, RoutedEventArgs e)
    {
        RefreshPage();
    }

    public static void RefreshPage()
    {
        ComicData.UpdateAllComics("RefreshPage", lazy: true);
    }

    private class ComicItemHandler : ComicItemViewModel.IItemHandler
    {
        private readonly HomePage _page;

        public ComicItemHandler(HomePage page)
        {
            _page = page;
        }

        public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            _page.OnAddToFavoritesClicked(sender, e);
        }

        public void OnHideClicked(object sender, RoutedEventArgs e)
        {
            _page.OnHideComicClicked(sender, e);
        }

        public void OnItemTapped(object sender, TappedRoutedEventArgs e)
        {
            _page.OnComicItemTapped(sender, e);
        }

        public void OnMarkAsReadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.MarkAsReadOrUnread(item, true);
        }

        public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.MarkAsReadOrUnread(item, false);
        }

        public void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            _page.OnOpenInNewTabClicked(sender, e);
        }

        public void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            _page.OnRemoveFromFavoritesClicked(sender, e);
        }

        public void OnSelectClicked(object sender, RoutedEventArgs e)
        {
        }

        public void OnUnhideClicked(object sender, RoutedEventArgs e)
        {
        }
    }
}
