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
using ComicReader.Data;

namespace ComicReader.Views
{
    using RawTask = Task<Utils.TaskQueue.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskQueue.TaskResult>, Utils.TaskQueue.TaskResult>;
    using TaskResult = Utils.TaskQueue.TaskResult;

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

        private Utils.ObservableCollectionPlus<ComicItemModel> m_ComicItemSource = new Utils.ObservableCollectionPlus<ComicItemModel>();
        public Utils.ObservableCollectionPlus<ComicItemModel> ComicItemSource
        {
            get => m_ComicItemSource;
            set
            {
                m_ComicItemSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicItemSource"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLibraryEmpty"));
            }
        }

        public bool IsLibraryEmpty => ComicItemSource.Count == 0;
    }

    public sealed partial class HomePage : Page
    {
        public static HomePage Current;
        public HomePageShared Shared { get; set; }
        public ObservableCollection<FolderItemModel> FolderItemDataSource { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;
        private Utils.CancellationLock m_update_folder_lock;
        private Utils.CancellationLock m_update_library_lock;
        private Utils.TaskQueue.TaskQueue m_update_queue;

        public HomePage()
        {
            Current = this;
            Shared = new HomePageShared();
            FolderItemDataSource = new ObservableCollection<FolderItemModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            Unloaded += m_tab_manager.OnTabUnloaded;
            m_tab_manager.OnTabRegister = OnTabRegister;
            m_tab_manager.OnTabUnregister = OnTabUnregister;
            m_tab_manager.OnTabUpdate = OnTabUpdate;
            m_tab_manager.OnTabStart = OnTabStart;

            m_update_folder_lock = new Utils.CancellationLock();
            m_update_library_lock = new Utils.CancellationLock();
            m_update_queue = Utils.TaskQueue.TaskQueueManager.EmptyQueue();

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
            Utils.Methods.Run(async delegate
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

            Utils.Methods.Sync(delegate
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

                SqliteCommand command = DatabaseManager.Connection.CreateCommand();
                command.CommandText = "SELECT * FROM " + DatabaseManager.ComicTable +
                    " ORDER BY " + ComicData.FieldLastVisit + " DESC LIMIT " +
                    result_count.ToString();

                await ComicDataManager.WaitLock(db); // Lock on.
                SqliteDataReader query = await command.ExecuteReaderAsync();
                var comic_items = new Utils.ObservableCollectionPlus<ComicItemModel>();

                while (query.Read())
                {
                    ComicData comic = await ComicDataManager.From(db, query);
                    if (comic == null) continue;
                    if (comic.Hidden) continue;

                    ComicItemModel data = new ComicItemModel
                    {
                        OnItemPressed = GridPointerPressed,
                        OnHideClicked = HideClick,
                        Comic = comic,
                        Title = comic.Title,
                        Id = comic.Id,
                        IsFavorite = FavoriteDataManager.FromIdNoLock(comic.Id) != null,
                        Rating = comic.Rating,
                        Progress = comic.Progress < 0 ? "Unread" :
                            (comic.Progress >= 100 ? "Finished" : comic.Progress.ToString() + "%")
                    };

                    comic_items.Add(data);
                }
                ComicDataManager.ReleaseLock(db); // Lock off.

                // Save results.
                Shared.ComicItemSource = comic_items;

                // Load images.
                var image_loader_tokens = new List<Utils.ImageLoaderToken>();

                foreach (ComicItemModel item in Shared.ComicItemSource)
                {
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
                var new_folder_source = new Collection<FolderItemModel>
                {
                    new FolderItemModel
                    {
                        OnItemPressed = FolderItemPressed,
                        IsAddNew = true
                    }
                };

                await XmlDatabaseManager.WaitLock();

                foreach (string folder in XmlDatabase.Settings.ComicFolders)
                {
                    FolderItemModel item = new FolderItemModel
                    {
                        OnItemPressed = FolderItemPressed,
                        OnRemoveClicked = FolderItemRemoveClick,
                        Folder = folder,
                        IsAddNew = false
                    };

                    new_folder_source.Add(item);
                }

                XmlDatabaseManager.ReleaseLock();
                Utils.Methods1<FolderItemModel>.UpdateCollection(FolderItemDataSource, new_folder_source, FolderItemModel.ContentEquals);
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

        private void GridPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ComicItemModel ctx = (ComicItemModel)((Grid)sender).DataContext;
            PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }

            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Reader, ctx.Comic);
        }

        private void HideClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                LockContext db = new LockContext();
                ComicItemModel ctx = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Hide(db, ctx.Comic);
                await UpdateLibrary(db);
            });
        }

        private void AddNewFolder()
        {
            Utils.Methods.Run(async delegate
            {
                if (!await SettingDataManager.AddComicFolderUsingPicker())
                {
                    return;
                }

                await UpdateFolders();
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.UpdateSealed(), "", m_update_queue);
                Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
            });
        }

        private void FolderItemPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }

            FolderItemModel ctx = (FolderItemModel)((Grid)sender).DataContext;

            if (ctx.IsAddNew)
            {
                AddNewFolder();
            }
            else
            {
                MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<dir:" + ctx.Folder + ">");
            }
        }

        private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FolderItemModel ctx = (FolderItemModel)((MenuFlyoutItem)sender).DataContext;
                await SettingDataManager.RemoveComicFolder(ctx.Folder, final: true);
                await UpdateFolders();
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.UpdateSealed(), "", m_update_queue);
                Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
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
