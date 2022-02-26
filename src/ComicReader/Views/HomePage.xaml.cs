using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Database;
using ComicReader.DesignData;

namespace ComicReader.Views
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;

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

    public sealed partial class HomePage : Page
    {
        public HomePageShared Shared { get; set; } = new HomePageShared();

        // Resources.
        private string m_string_finished = null;
        private string StringFinished
        {
            get
            {
                if (m_string_finished == null)
                {
                    m_string_finished = Utils.C0.TryGetResourceString("Finished");
                }

                return m_string_finished;
            }
        }

        private string m_string_unread = null;
        private string StringUnread
        {
            get
            {
                if (m_string_unread == null)
                {
                    m_string_unread = Utils.C0.TryGetResourceString("Unread");
                }

                return m_string_unread;
            }
        }

        private Utils.ObservableCollectionPlus<ComicItemViewModel> ComicItemSource
            = new Utils.ObservableCollectionPlus<ComicItemViewModel>();
        public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }
            = new ObservableCollection<FolderItemViewModel>();

        private readonly Utils.Tab.TabManager m_tab_manager;
        private Utils.CancellationLock m_update_folder_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_update_library_lock = new Utils.CancellationLock();

        public HomePage()
        {
            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabUpdate = OnTabUpdate,
                OnTabStart = OnTabStart
            };

            InitializeComponent();
        }

        // Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_manager.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            m_tab_manager.OnNavigatedFrom(e);
        }

        private void OnTabRegister(object shared)
        {
            Shared.NavigationPageShared = (NavigationPageShared)shared;

            ComicData.Manager.OnUpdated += OnComicDataUpdated;
        }

        private void OnTabUnregister()
        {
            ComicData.Manager.OnUpdated -= OnComicDataUpdated;
        }

        private void OnTabUpdate()
        {
            Utils.C0.Run(async delegate
            {
                await Update();
            });
        }

        private void OnTabStart(Utils.Tab.TabIdentifier tab_id)
        {
            Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Home;
            tab_id.Tab.Header = Utils.C0.TryGetResourceString("NewTab");
            tab_id.Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            Shared.NavigationPageShared.SetSearchBox("");
        }

        public static string PageUniqueString(object args) => "blank";

        // utilities
        public async Task Update()
        {
            LockContext db = new LockContext();

            await UpdateFolders();
            await UpdateLibrary(db);
        }

        public SealedTask UpdateSealed()
        {
            return delegate (RawTask _)
            {
                // IMPORTANT: Use TaskCompletionSource to guarantee all async tasks
                // in Sync block has completed.
                TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

                Utils.C0.Sync(async delegate
                {
                    await Update();
                    completion_src.SetResult(true);
                }).Wait();

                completion_src.Task.Wait();
                return new TaskResult();
            };
        }

        private void ComicDataToViewModel(ComicData comic, ComicItemViewModel model)
        {
            model.Comic = comic;
            model.Title = comic.Title;
            model.Rating = comic.Rating;

            if (comic.Progress < 0)
            {
                model.Progress = StringUnread;
            }
            else if (comic.Progress >= 100)
            {
                model.Progress = StringFinished;
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

        class _Record
        {
            public long Id;
            public DateTimeOffset LastVisit;
        }

        private void OnComicDataUpdated(LockContext db)
        {
            // IMPORTANT: Use TaskCompletionSource to guarantee all async tasks
            // in Sync block has completed.
            TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

            Utils.C0.Sync(async delegate
            {
                await UpdateLibrary(db);
                completion_src.SetResult(true);
            }).Wait();

            completion_src.Task.Wait();
        }

        public async Task UpdateLibrary(LockContext db)
        {
            await m_update_library_lock.WaitAsync();
            try
            {
                if (m_update_library_lock.CancellationRequested)
                {
                    return;
                }

                // Get recent visited comics.
                SqliteCommand command = SqliteDatabaseManager.NewCommand();

                // Use ORDER BY here will cause a crush (especially for a large result set)
                // due to https://github.com/dotnet/efcore/issues/20044.
                // Switch from Microsoft.Data.Sqlite to SQLitePCLRaw.bundle_winsqlite3 will
                // solve the issue but the app then cannot not be built in Release mode.
                // (See https://github.com/ericsink/SQLitePCL.raw/issues/346)
                // A workaround here is to sort the data manually.

                // command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                //     " ORDER BY " + ComicData.Field.LastVisit + " DESC";
                command.CommandText = "SELECT " + ComicData.Field.Id + "," + ComicData.Field.LastVisit +
                    " FROM " + SqliteDatabaseManager.ComicTable;

                await ComicData.Manager.WaitLock(db); // Lock on.
                SqliteDataReader query = await command.ExecuteReaderAsync();
                var records = new List<_Record>();

                while (query.Read())
                {
                    records.Add(new _Record
                    {
                        Id = query.GetInt64(0),
                        LastVisit = query.GetDateTime(1),
                    });
                }

                // Sort the data using LastVisit descending.
                records = records.OrderByDescending((_Record x) => x.LastVisit).ToList();
                
                // Convert to view models.
                var comic_items = new List<ComicItemViewModel>();

                for (int i = 0; comic_items.Count < 16 && i < records.Count; ++i)
                {
                    ComicData comic = await ComicData.Manager.FromId(db, records[i].Id);
                    
                    if (comic == null)
                    {
                        continue;
                    }

                    if (comic.Hidden)
                    {
                        continue;
                    }

                    ComicItemViewModel model = new ComicItemViewModel();
                    ComicDataToViewModel(comic, model);

                    comic_items.Add(model);
                }
                ComicData.Manager.ReleaseLock(db); // Lock off.

                // Save results.
                Utils.C1<ComicItemViewModel>.UpdateCollection(ComicItemSource, comic_items,
                    (ComicItemViewModel x, ComicItemViewModel y) =>
                    x.Comic.Title == y.Comic.Title &&
                    x.Rating == y.Rating &&
                    x.Progress == y.Progress &&
                    x.IsFavorite == y.IsFavorite);
                Shared.IsLibraryEmpty = ComicItemSource.Count == 0;

                // Load images.
                var image_loader_tokens = new List<Utils.ImageLoaderToken>();

                foreach (ComicItemViewModel item in ComicItemSource)
                {
                    if (item.IsImageLoaded)
                    {
                        continue;
                    }

                    image_loader_tokens.Add(new Utils.ImageLoaderToken
                    {
                        Comic = item.Comic,
                        Index = 0,
                        Callback = (BitmapImage img) =>
                        {
                            item.Image = img;
                            item.IsImageLoaded = true;
                        }
                    });
                }

                double image_height = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];

                await Task.Run(delegate
                {
                    Utils.ImageLoader.Load(db, image_loader_tokens,
                        image_height * 1.4, image_height * 1.4,
                        m_update_library_lock).Wait();
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
                var new_folder_source = new Collection<FolderItemViewModel>
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

        // events
        private void OnSeeAllBtClicked(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<all>");
        }

        private void OnSeeHiddenBtClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<hidden>");
        }

        private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            MainPage.Current.LoadTab(null, Utils.Tab.PageType.Reader, item.Comic);
        }

        private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((Grid)sender).DataContext;
            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Reader, item.Comic);
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
                LockContext db = new LockContext();
                ComicItemViewModel item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await ComicData.Manager.Hide(db, item.Comic);
                await UpdateLibrary(db);
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
                    ComicData.Manager.UpdateSealed(lazy_load: true), "");
            });
        }

        private void OnFolderItemTapped(object sender, TappedRoutedEventArgs e)
        {
            FolderItemViewModel item = (FolderItemViewModel)((Grid)sender).DataContext;

            if (item.IsAddNew)
            {
                AddNewFolder();
            }
            else
            {
                MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<dir: " + item.Path + ">");
            }
        }

        private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                FolderItemViewModel item = (FolderItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(item.Path, final: true);
                await UpdateFolders();
                Utils.TaskQueueManager.NewTask(ComicData.Manager.UpdateSealed(lazy_load: true));
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
            Utils.TaskQueueManager.NewTask(ComicData.Manager.UpdateSealed(lazy_load: true));
        }
    }
}
