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

    public class HomePageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ContentPageShared m_ContentPageShared;
        public ContentPageShared ContentPageShared
        {
            get => m_ContentPageShared;
            set
            {
                m_ContentPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ContentPageShared"));
            }
        }
    }

    public sealed partial class HomePage : Page
    {
        public static HomePage Current;
        public HomePageShared Shared { get; set; }
        public Utils.TrulyObservableCollection<ComicItemModel> ComicItemSource { get; set; }
        public ObservableCollection<FolderItemModel> FolderItemDataSource { get; set; }

        private TabManager m_tab_manager;
        private Utils.CancellationLock m_update_folder_lock;
        private Utils.CancellationLock m_update_library_lock;

        public HomePage()
        {
            Current = this;
            Shared = new HomePageShared();
            ComicItemSource = new Utils.TrulyObservableCollection<ComicItemModel>();
            FolderItemDataSource = new ObservableCollection<FolderItemModel>();

            m_tab_manager = new TabManager();
            m_tab_manager.OnSetShared = OnSetShared;
            m_tab_manager.OnPageEntered = OnPageEntered;
            m_tab_manager.OnUpdate = OnUpdate;
            m_update_folder_lock = new Utils.CancellationLock();
            m_update_library_lock = new Utils.CancellationLock();

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
            Shared.ContentPageShared = (ContentPageShared)shared;
        }

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await UpdateInfo();
            });
        }

        private void OnUpdate(TabIdentifier tab_id)
        {
            Shared.ContentPageShared.RootPageShared.CurrentPageType = PageType.Home;
            tab_id.Tab.Header = "New tab";
            tab_id.Tab.IconSource =
                new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            ContentPage.Current.SetSearchBox("");
        }

        public static string GetPageUniqueString(object args) => "blank";

        // user-defined functions
        // update
        public async Task UpdateInfo()
        {
            await UpdateFolders();
            await UpdateLibrary();
        }

        public async Task UpdateLibrary()
        {
            await DataManager.WaitForDatabaseReady();
            await m_update_library_lock.WaitAsync();

            try
            {
                if (m_update_library_lock.CancellationRequested)
                {
                    return;
                }

                // get recent visited comics
                await DataManager.WaitLock();
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

                DataManager.ReleaseLock();
                IEnumerable<ComicItemData> sorted = min_heap.OrderBy((ComicItemData x) => x.LastVisit).Reverse();

                // add to comic item source
                ComicItemSource.Clear();

                foreach (ComicItemData comic in sorted)
                {
                    ComicItemModel data = new ComicItemModel
                    {
                        OnItemPressed = Grid_PointerPressed,
                        OnHideClicked = Hide_Click,
                        Comic = comic,
                        Title = comic.Title2.Length == 0 ? comic.Title : comic.Title2 + "-" + comic.Title,
                        Id = comic.Id,
                        IsFavorite = await DataManager.GetFavoriteWithId(comic.Id) != null
                    };

                    ReadRecordData comic_record = await DataManager.GetReadRecordWithId(comic.Id);

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
                List<DataManager.ImageLoaderToken> image_loader_tokens = new List<DataManager.ImageLoaderToken>();

                foreach (ComicItemModel item in ComicItemSource)
                {
                    image_loader_tokens.Add(new DataManager.ImageLoaderToken
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
                    DataManager.UtilsLoadImages(image_loader_tokens, image_height * 1.4, image_height * 1.4, m_update_library_lock).Wait();
                });
            }
            finally
            {
                m_update_library_lock.Release();
            }
        }

        public async Task UpdateFolders()
        {
            await DataManager.WaitForDatabaseReady();
            await m_update_folder_lock.WaitAsync();

            try
            {
                // add to folder item source
                Collection<FolderItemModel> new_folder_source = new Collection<FolderItemModel>();

                new_folder_source.Add(new FolderItemModel
                {
                    OnItemPressed = FolderItem_Pressed,
                    IsAddNew = true
                });

                await DataManager.WaitLock();

                foreach (string folder in Database.AppSettings.ComicFolders)
                {
                    FolderItemModel item = new FolderItemModel
                    {
                        OnItemPressed = FolderItem_Pressed,
                        OnRemoveClicked = FolderItemRemove_Click,
                        Folder = folder,
                        IsAddNew = false
                    };

                    new_folder_source.Add(item);
                }

                DataManager.ReleaseLock();
                Utils.Methods_1<FolderItemModel>.UpdateCollection(FolderItemDataSource, new_folder_source, FolderItemModel.ContentEquals);
            }
            finally
            {
                m_update_folder_lock.Release();
            }
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(m_tab_manager.TabId, PageType.Search, "<all>");
        }

        private void ShowHiddens_Click(object sender, RoutedEventArgs e)
        {
            RootPage.Current.LoadTab(m_tab_manager.TabId, PageType.Search, "<hidden>");
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ComicItemModel ctx = (ComicItemModel)((Grid)sender).DataContext;
            PointerPoint pt = e.GetCurrentPoint((UIElement)sender);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }
                
            RootPage.Current.LoadTab(m_tab_manager.TabId, PageType.Reader, ctx.Comic);
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel ctx = (ComicItemModel)((MenuFlyoutItem)sender).DataContext;
                await DataManager.HideComic(ctx.Comic);
                await UpdateLibrary();
            });
        }

        private void FolderItem_Pressed(object sender, PointerRoutedEventArgs e)
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
                    if (!await DataManager.UtilsAddToComicFoldersUsingPicker())
                    {
                        return;
                    }

                    await UpdateFolders();

                    Utils.TaskQueue.TaskQueue update_queue = Utils.TaskQueue.TaskQueueManager.EmptyQueue();
                    Utils.TaskQueue.TaskQueueManager.AppendTask(DataManager.UpdateComicDataSealed(), "", update_queue);
                    Utils.TaskQueue.TaskQueueManager.AppendTask(delegate (RawTask _t) {
                        _ = Utils.Methods.Sync(async delegate
                        {
                            await UpdateInfo();
                        });
                        return new Utils.TaskQueue.TaskResult();
                    }, "", update_queue);
                }
                else
                {
                    RootPage.Current.LoadTab(m_tab_manager.TabId, PageType.Search, "<dir:" + ctx.Folder + ">");
                }
            });
        }

        private void FolderItemRemove_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FolderItemModel ctx = (FolderItemModel)((MenuFlyoutItem)sender).DataContext;
                await DataManager.RemoveFromComicFolders(ctx.Folder, final: true);
                await UpdateFolders();
                Utils.TaskQueue.TaskQueueManager.AppendTask(DataManager.UpdateComicDataSealed(),
                    "", Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            });
        }
    }
}
