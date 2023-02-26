using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Common.Router;
using ComicReader.Database;
using ComicReader.DesignData;
using ComicReader.Common;

namespace ComicReader.Views
{
    public class HomePageShared : INotifyPropertyChanged
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

        private bool m_IsLibraryEmpty = false;
        public bool IsLibraryEmpty
        {
            get => m_IsLibraryEmpty;
            set
            {
                m_IsLibraryEmpty = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLibraryEmpty"));
            }
        }
    }

    sealed internal partial class HomePage : NavigatablePage
    {
        public HomePageShared Shared { get; set; } = new HomePageShared();

        private Utils.ObservableCollectionPlus<ComicItemViewModel> ComicItemSource { get; set; }
            = new Utils.ObservableCollectionPlus<ComicItemViewModel>();
        public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }
            = new ObservableCollection<FolderItemViewModel>();

        private readonly Utils.CancellationLock m_update_folder_lock = new Utils.CancellationLock();
        private readonly Utils.CancellationLock m_update_library_lock = new Utils.CancellationLock();

        public HomePage()
        {
            InitializeComponent();
        }

        public override void OnStart(NavigationParams p)
        {
            base.OnStart(p);
            Shared.NavigationPageShared = (NavigationPageShared)p.Params;
        }

        public override void OnResume()
        {
            base.OnResume();
            ComicData.OnUpdated += OnComicDataUpdated;

            GetTabId().Tab.Header = Utils.StringResourceProvider.GetResourceString("NewTab");
            GetTabId().Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            
            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        public override void OnPause()
        {
            base.OnPause();
            ComicData.OnUpdated -= OnComicDataUpdated;
        }

        public override void OnSelected()
        {
            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        // Utilities
        public async Task Update()
        {
            await UpdateFolders();
            await UpdateLibrary();
        }

        private void ComicDataToViewModel(ComicData comic, ComicItemViewModel model)
        {
            model.Comic = comic;
            model.Title = comic.Title;
            model.Rating = comic.Rating;

            if (comic.Progress < 0)
            {
                model.Progress = Utils.StringResourceProvider.GetResourceString("Unread");
            }
            else if (comic.Progress >= 100)
            {
                model.Progress = Utils.StringResourceProvider.GetResourceString("Finished");
            }
            else
            {
                model.Progress = comic.Progress.ToString() + "%";
            }

            model.IsFavorite = FavoriteDataManager.FromIdNoLock(comic.Id) != null;

            model.OnItemTapped = OnComicItemTapped;
            model.OnOpenInNewTabClicked = OnOpenInNewTabClicked;
            model.OnAddToFavoritesClicked = OnAddToFavoritesClicked;
            model.OnRemoveFromFavoritesClicked = OnRemoveFromFavoritesClicked;
            model.OnHideClicked = OnHideComicClicked;
        }

        private void OnComicDataUpdated()
        {
            // IMPORTANT: Use TaskCompletionSource to guarantee all async tasks
            // in Sync block has completed.
            TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

            Utils.C0.Sync(async delegate
            {
                await UpdateLibrary();
                completion_src.SetResult(true);
            }).Wait();

            completion_src.Task.Wait();
        }

        public async Task UpdateLibrary()
        {
            await m_update_library_lock.WaitAsync();
            try
            {
                if (m_update_library_lock.CancellationRequested)
                {
                    return;
                }

                // Get recent visited comics.
                Utils.FixedHeap<Tuple<long, DateTimeOffset>> records = new Utils.FixedHeap<Tuple<long, DateTimeOffset>>(16,
                    (Tuple<long, DateTimeOffset> x, Tuple<long, DateTimeOffset> y) => { return x.Item2.CompareTo(y.Item2); });

                await ComicData.CommandBlock(async delegate (SqliteCommand command)
                {
                    // Use ORDER BY here will cause a crush (especially for a large result set)
                    // due to https://github.com/dotnet/efcore/issues/20044.
                    // Switch from Microsoft.Data.Sqlite to SQLitePCLRaw.bundle_winsqlite3 will
                    // solve the issue but the app then cannot not be built in Release mode.
                    // (See https://github.com/ericsink/SQLitePCL.raw/issues/346)
                    // A workaround here is to sort the data manually.

                    // command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                    //     " ORDER BY " + ComicData.Field.LastVisit + " DESC";
                    command.CommandText = "SELECT " + ComicData.Field.Id + "," +
                        ComicData.Field.Hidden + "," + ComicData.Field.LastVisit +
                        " FROM " + SqliteDatabaseManager.ComicTable;

                    using (SqliteDataReader query = await command.ExecuteReaderAsync())
                    {
                        while (query.Read())
                        {
                            bool hidden = query.GetBoolean(1);

                            if (!hidden)
                            {
                                records.Add(new Tuple<long, DateTimeOffset>
                                (
                                    query.GetInt64(0),
                                    query.GetDateTime(2)
                                ));
                            }
                        }
                    }
                });

                // Convert to view models.
                List<ComicItemViewModel> comic_items = new List<ComicItemViewModel>();

                foreach (Tuple<long, DateTimeOffset> record in records.GetSorted())
                {
                    ComicData comic = await ComicData.FromId(record.Item1);
                    
                    if (comic == null)
                    {
                        continue;
                    }

                    ComicItemViewModel model = new ComicItemViewModel();
                    ComicDataToViewModel(comic, model);
                    comic_items.Add(model);
                }

                // Save results.
                Utils.C1<ComicItemViewModel>.UpdateCollection(ComicItemSource, comic_items,
                    (ComicItemViewModel x, ComicItemViewModel y) =>
                    x.Comic.Title == y.Comic.Title &&
                    x.Rating == y.Rating &&
                    x.Progress == y.Progress &&
                    x.IsFavorite == y.IsFavorite);
                Shared.IsLibraryEmpty = ComicItemSource.Count == 0;

                // Load images.
                List<Utils.ImageLoader.Token> image_loader_tokens = new List<Utils.ImageLoader.Token>();

                foreach (ComicItemViewModel item in ComicItemSource)
                {
                    if (item.IsImageLoaded)
                    {
                        continue;
                    }

                    image_loader_tokens.Add(new Utils.ImageLoader.Token
                    {
                        Comic = item.Comic,
                        Index = -1,
                        Callback = (BitmapImage img) =>
                        {
                            item.Image = img;
                            item.IsImageLoaded = true;
                        }
                    });
                }

                double image_width = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"] - 40.0;
                double image_height = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];

                await Task.Run(delegate
                {
                    new Utils.ImageLoader.Builder(image_loader_tokens, m_update_library_lock)
                        .WidthConstrain(image_width).HeightConstrain(image_height).Multiplication(1.4).Commit().Wait();
                });
            }
            finally
            {
                m_update_library_lock.Release();
            }
        }

        public async Task UpdateFolders()
        {
            await m_update_folder_lock.WaitAsync();
            try
            {
                // Add to folder item source.
                Collection<FolderItemViewModel> new_folder_source = new Collection<FolderItemViewModel>
                {
                    new FolderItemViewModel
                    {
                        OnItemTapped = OnFolderItemTapped,
                        IsAddNew = true
                    }
                };

                await XmlDatabaseManager.WaitLock();

                foreach (string path in XmlDatabase.Settings.ComicFolders)
                {
                    FolderItemViewModel item = new FolderItemViewModel
                    {
                        OnItemTapped = OnFolderItemTapped,
                        OnRemoveClicked = FolderItemRemoveClick,
                        Folder = Utils.StringUtils.ItemNameFromPath(path),
                        Path = path,
                        IsAddNew = false
                    };

                    new_folder_source.Add(item);
                }

                XmlDatabaseManager.ReleaseLock();
                Utils.C1<FolderItemViewModel>.UpdateCollection(FolderItemDataSource, new_folder_source, FolderItemViewModel.ContentEquals);
            }
            finally
            {
                m_update_folder_lock.Release();
            }
        }

        // Events
        private void OnSeeAllBtClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(GetTabId(), SearchPageTrait.Instance, "<all>");
        }

        private void OnSeeHiddenBtClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(GetTabId(), SearchPageTrait.Instance, "<hidden>");
        }

        private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            MainPage.Current.LoadTab(null, ReaderPageTrait.Instance, item.Comic);
        }

        private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!CanHandleTapped())
            {
                return;
            }
            ComicItemViewModel item = (ComicItemViewModel)((Grid)sender).DataContext;
            MainPage.Current.LoadTab(GetTabId(), ReaderPageTrait.Instance, item.Comic);
        }

        private void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                item.IsFavorite = true;
                await FavoriteDataManager.Add(item.Comic.Id, item.Title, true);
            });
        }

        private void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                item.IsFavorite = false;
                await FavoriteDataManager.RemoveWithId(item.Comic.Id, true);
            });
        }

        private void OnHideComicClicked(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await item.Comic.SaveHiddenAsync(true);
                ComicItemSource.Remove(item);
                await UpdateLibrary();
            });
        }

        private void AddNewFolder()
        {
            Utils.C0.Run(async delegate
            {
                if (!await SettingDataManager.AddComicFolderUsingPicker())
                {
                    return;
                }

                await UpdateFolders();
                Utils.TaskQueueManager.AppendTask(
                    ComicData.UpdateSealed(lazy_load: true), "");
            });
        }

        private void OnFolderItemTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!CanHandleTapped())
            {
                return;
            }

            FolderItemViewModel item = (FolderItemViewModel)((Grid)sender).DataContext;
            if (item.IsAddNew)
            {
                AddNewFolder();
            }
            else
            {
                MainPage.Current.LoadTab(GetTabId(), SearchPageTrait.Instance, "<dir: " + item.Path + ">");
            }
        }

        private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                FolderItemViewModel item = (FolderItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(item.Path, final: true);
                await UpdateFolders();
                Utils.TaskQueueManager.NewTask(ComicData.UpdateSealed(lazy_load: true));
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
            Utils.TaskQueueManager.NewTask(ComicData.UpdateSealed(lazy_load: true));
        }
    }
}
