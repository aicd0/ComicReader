using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
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
    }

    public sealed partial class HomePage : Page
    {
        public static HomePage Current;
        public HomePageShared Shared { get; set; }
        public Utils.TrulyObservableCollection<ComicItemModel> ComicItemSource { get; set; }
        public ObservableCollection<FolderItemModel> FolderItemDataSource { get; set; }

        private readonly Utils.Tab.TabManager m_tab_manager;
        private Utils.CancellationLock m_update_folder_lock;
        private Utils.CancellationLock m_update_library_lock;
        private Utils.TaskQueue.TaskQueue m_update_queue;

        public HomePage()
        {
            Current = this;
            Shared = new HomePageShared();
            ComicItemSource = new Utils.TrulyObservableCollection<ComicItemModel>();
            FolderItemDataSource = new ObservableCollection<FolderItemModel>();

            m_tab_manager = new Utils.Tab.TabManager();
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnUpdate = OnUpdate;
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

        private void OnSetShared(object shared)
        {
            Shared.NavigationPageShared = (NavigationPageShared)shared;
        }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await Update();
            });
        }

        private void OnUpdate(Utils.Tab.TabIdentifier tab_id)
        {
            Shared.NavigationPageShared.CurrentPageType = Utils.Tab.PageType.Home;
            tab_id.Tab.Header = "New tab";
            tab_id.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            NavigationPage.Current.SetSearchBox("");
        }

        public static string PageUniqueString(object args) => "blank";

        // utilities
        public async Task Update()
        {
            await UpdateFolders();
            await UpdateLibrary();
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

        public async Task UpdateLibrary()
        {
            await m_update_library_lock.WaitAsync();

            try
            {
                if (m_update_library_lock.CancellationRequested)
                {
                    return;
                }

                // get recent visited comics
                await DatabaseManager.WaitLock();
                const int result_count = 12;
                int cmp_func(ComicItemData x, ComicItemData y) => x.LastVisit > y.LastVisit ? 1 : -1;
                Utils.MinHeap<ComicItemData> min_heap = new Utils.MinHeap<ComicItemData>(result_count, cmp_func);

                foreach (ComicItemData comic in Database.Comics.Items)
                {
                    if (comic.Hidden)
                    {
                        continue;
                    }

                    min_heap.Add(comic);
                }

                DatabaseManager.ReleaseLock();
                IEnumerable<ComicItemData> sorted = min_heap.OrderBy((ComicItemData x) => x.LastVisit).Reverse();

                // add to comic item source
                ComicItemSource.Clear();

                foreach (ComicItemData comic in sorted)
                {
                    ComicItemModel data = new ComicItemModel
                    {
                        OnItemPressed = GridPointerPressed,
                        OnHideClicked = HideClick,
                        Comic = comic,
                        Title = comic.Title,
                        Id = comic.Id,
                        IsFavorite = await FavoritesDataManager.FromId(comic.Id) != null
                    };

                    RecentReadItemData comic_record = await RecentReadDataManager.FromId(comic.Id);

                    if (comic_record != null)
                    {
                        data.Rating = comic_record.Rating;
                        data.Progress = comic_record.Progress >= 100 ? "Finished" : comic_record.Progress.ToString() + "%";
                    }
                    else
                    {
                        data.Progress = "Unread";
                    }

                    ComicItemSource.Add(data);
                }

                // load images
                List<ImageLoaderToken> image_loader_tokens = new List<ImageLoaderToken>();

                foreach (ComicItemModel item in ComicItemSource)
                {
                    image_loader_tokens.Add(new ImageLoaderToken
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

                double image_height = (double)Resources["ComicItemVerticalImageHeight"];

                await Task.Run(delegate
                {
                    ComicDataManager.LoadImages(image_loader_tokens, image_height * 1.4, image_height * 1.4, m_update_library_lock).Wait();
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
                // add to folder item source
                Collection<FolderItemModel> new_folder_source = new Collection<FolderItemModel>();

                new_folder_source.Add(new FolderItemModel
                {
                    OnItemPressed = FolderItemPressed,
                    IsAddNew = true
                });

                await DatabaseManager.WaitLock();

                foreach (string folder in Database.AppSettings.ComicFolders)
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

                DatabaseManager.ReleaseLock();
                Utils.Methods1<FolderItemModel>.UpdateCollection(FolderItemDataSource, new_folder_source, FolderItemModel.ContentEquals);
            }
            finally
            {
                m_update_folder_lock.Release();
            }
        }

        // events
        private void ShowAllClick(object sender, RoutedEventArgs e)
        {
            MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<all>");
        }

        private void ShowHiddensClick(object sender, RoutedEventArgs e)
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
                ComicItemModel ctx = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                await ComicDataManager.Hide(ctx.Comic);
                await UpdateLibrary();
            });
        }

        private void FolderItemPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

                if (!pt.Properties.IsLeftButtonPressed)
                {
                    return;
                }

                FolderItemModel ctx = (FolderItemModel)((Grid)sender).DataContext;

                if (ctx.IsAddNew)
                {
                    if (!await AppSettingsDataManager.AddComicFolderUsingPicker())
                    {
                        return;
                    }

                    await UpdateFolders();
                    Utils.TaskQueue.TaskQueueManager.AppendTask(ComicDataManager.UpdateSealed(), "", m_update_queue);
                    Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
                }
                else
                {
                    MainPage.Current.LoadTab(m_tab_manager.TabId, Utils.Tab.PageType.Search, "<dir:" + ctx.Folder + ">");
                }
            });
        }

        private void FolderItemRemoveClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FolderItemModel ctx = (FolderItemModel)((MenuFlyoutItem)sender).DataContext;
                await AppSettingsDataManager.RemoveComicFolder(ctx.Folder, final: true);
                await UpdateFolders();
                Utils.TaskQueue.TaskQueueManager.AppendTask(ComicDataManager.UpdateSealed(), "", m_update_queue);
                Utils.TaskQueue.TaskQueueManager.AppendTask(UpdateSealed(), "", m_update_queue);
            });
        }
    }
}
