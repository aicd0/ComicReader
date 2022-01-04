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
        public static HomePage Current;
        public HomePageShared Shared { get; set; }

        private Utils.ObservableCollectionPlus<ComicItemViewModel> ComicItemSource
            = new Utils.ObservableCollectionPlus<ComicItemViewModel>();
        public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;
        private Utils.CancellationLock m_update_folder_lock;
        private Utils.CancellationLock m_update_library_lock;
        private Utils.TaskQueue m_update_queue;

        public HomePage()
        {
            Current = this;
            Shared = new HomePageShared();
            FolderItemDataSource = new ObservableCollection<FolderItemViewModel>();

            m_tab_manager = new Utils.Tab.TabManager(this)
            {
                OnTabRegister = OnTabRegister,
                OnTabUnregister = OnTabUnregister,
                OnTabUpdate = OnTabUpdate,
                OnTabStart = OnTabStart
            };

            m_update_folder_lock = new Utils.CancellationLock();
            m_update_library_lock = new Utils.CancellationLock();
            m_update_queue = Utils.TaskQueueManager.EmptyQueue();

            InitializeComponent();
        }

        // navigation
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
        }

        private void OnTabUnregister() { }

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
            tab_id.Tab.Header = "New tab";
            tab_id.Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            NavigationPage.Current.SetSearchBox("");
        }

        public static string PageUniqueString(object args) => "blank";

        // utilities
        public async Task Update()
        {
            LockContext db = new LockContext();

            await UpdateFolders();
            await UpdateLibrary(db);
        }

        public SealedTask UpdateSealed() => delegate (RawTask _)
        {
            Task update_task = null;

            Utils.C0.Sync(delegate
            {
                update_task = Update();
            }).Wait();

            update_task.Wait();
            return new TaskResult();
        };

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
                const int result_count = 12;

                SqliteCommand command = SqliteDatabaseManager.NewCommand();
                command.CommandText = "SELECT * FROM " + SqliteDatabaseManager.ComicTable +
                    " ORDER BY " + ComicData.FieldLastVisit + " DESC LIMIT " +
                    result_count.ToString();

                await ComicDataManager.WaitLock(db); // Lock on.
                SqliteDataReader query = await command.ExecuteReaderAsync();
                var comic_items = new Utils.ObservableCollectionPlus<ComicItemViewModel>();

                while (query.Read())
                {
                    ComicData comic = await ComicDataManager.From(db, query);
                    if (comic == null) continue;
                    if (comic.Hidden) continue;

                    ComicItemViewModel data = new ComicItemViewModel
                    {
                        Comic = comic,
                        Title = comic.Title,
                        Rating = comic.Rating,
                        Progress = comic.Progress < 0 ? "Unread" :
                            (comic.Progress >= 100 ? "Finished" : comic.Progress.ToString() + "%"),
                        IsFavorite = FavoriteDataManager.FromIdNoLock(comic.Id) != null,

                        OnItemPressed = OnComicItemPressed,
                        OnOpenInNewTabClicked = OnOpenInNewTabClicked,
                        OnAddToFavoritesClicked = OnAddToFavoritesClicked,
                        OnRemoveFromFavoritesClicked = OnRemoveFromFavoritesClicked,
                        OnHideClicked = OnHideComicClicked,
                    };

                    comic_items.Add(data);
                }
                ComicDataManager.ReleaseLock(db); // Lock off.

                // Save results.
                Utils.C1<ComicItemViewModel>.UpdateCollection(ComicItemSource, comic_items,
                    (ComicItemViewModel x, ComicItemViewModel y) => x.Comic.Id == y.Comic.Id);
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
                        OnItemPressed = FolderItemPressed,
                        IsAddNew = true
                    }
                };

                await XmlDatabaseManager.WaitLock();

                foreach (string folder in XmlDatabase.Settings.ComicFolders)
                {
                    FolderItemViewModel item = new FolderItemViewModel
                    {
                        OnItemPressed = FolderItemPressed,
                        OnRemoveClicked = FolderItemRemoveClick,
                        Folder = folder,
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

        private void OnComicItemPressed(object sender, PointerRoutedEventArgs e)
        {
            ComicItemViewModel item = (ComicItemViewModel)((Grid)sender).DataContext;
            PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }

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
                await ComicDataManager.Hide(db, item.Comic);
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
                    ComicDataManager.UpdateSealed(lazy_load: true), "", m_update_queue);
                Utils.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
            });
        }

        private void FolderItemPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

            if (!pt.Properties.IsLeftButtonPressed)
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
                MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<dir:" + item.Folder + ">");
            }
        }

        private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
        {
            Utils.C0.Run(async delegate
            {
                FolderItemViewModel item = (FolderItemViewModel)((MenuFlyoutItem)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(item.Folder, final: true);
                await UpdateFolders();
                Utils.TaskQueueManager.AppendTask(
                    ComicDataManager.UpdateSealed(lazy_load: true), "", m_update_queue);
                Utils.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
            });
        }

        private void OnTryAddFolderBtClicked(object sender, RoutedEventArgs e)
        {
            AddNewFolder();
        }

        private void OnRefreshBtClicked(object sender, RoutedEventArgs e)
        {
            Shared.NavigationPageShared.RefreshPage();
        }
    }
}
