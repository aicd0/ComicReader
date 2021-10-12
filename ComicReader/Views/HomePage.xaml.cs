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
using ComicReader.Data;

namespace ComicReader.Views
{
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
        private bool m_page_initialized = false;
        private TabId m_tab_id;

        Utils.CancellationLock m_update_folder_lock;
        Utils.CancellationLock m_update_library_lock;

        public Utils.TrulyObservableCollection<ComicItemModel> ComicItemSource { get; set; }
        public ObservableCollection<FolderItemModel> FolderItemDataSource { get; set; }

        public HomePage()
        {
            Current = this;
            Shared = new HomePageShared();
            m_update_folder_lock = new Utils.CancellationLock();
            m_update_library_lock = new Utils.CancellationLock();
            ComicItemSource = new Utils.TrulyObservableCollection<ComicItemModel>();
            FolderItemDataSource = new ObservableCollection<FolderItemModel>();

            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!m_page_initialized)
            {
                m_page_initialized = true;
                NavigationParams p = (NavigationParams)e.Parameter;
                m_tab_id = p.TabId;
                m_tab_id.OnTabSelected += OnPageEntered;
                Shared.ContentPageShared = (ContentPageShared)p.Shared;
            }

            OnPageEntered();
            UpdateTabId();
            Shared.ContentPageShared.RootPageShared.CurrentPageType = PageType.Blank;
        }

        public static string C_PageUniqueString(object args) => "blank";

        private void OnPageEntered()
        {
            Utils.Methods.Run(async delegate
            {
                await UpdateInfo();
            });
        }

        private void UpdateTabId()
        {
            m_tab_id.Tab.Header = "New tab";
            m_tab_id.Tab.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Document };
            m_tab_id.UniqueString = C_PageUniqueString(null);
            m_tab_id.Type = PageType.Blank;
        }

        // user-defined functions
        // update
        public async Task UpdateInfo()
        {
            await UpdateLibrary();
            await UpdateFolders();
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

                await DataManager.WaitLock();
                // get recent visited comics
                const int result_count = 12;
                int cmp_func(ComicData x, ComicData y) => x.LastVisit > y.LastVisit ? 1 : -1;
                Utils.MinHeap<ComicData> min_heap = new Utils.MinHeap<ComicData>(result_count, cmp_func);
                foreach (ComicData comic in Database.Comics)
                {
                    if (comic.Hidden)
                    {
                        continue;
                    }
                    min_heap.Add(comic);
                }
                DataManager.ReleaseLock();
                IEnumerable<ComicData> sorted = min_heap.OrderBy((ComicData x) => x.LastVisit).Reverse();

                // add to comic item source
                ComicItemSource.Clear();
                foreach (ComicData comic in sorted)
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
            Utils.Methods.Run(async delegate
            {
                await RootPage.Current.LoadTab(m_tab_id, PageType.Search, "<all>");
            });
        }

        private void ShowHiddens_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await RootPage.Current.LoadTab(m_tab_id, PageType.Search, "<hidden>");
            });
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ComicItemModel ctx = (ComicItemModel)((Grid)sender).DataContext;
                PointerPoint pt = e.GetCurrentPoint((UIElement)sender);
                if (!pt.Properties.IsLeftButtonPressed)
                {
                    return;
                }
                await RootPage.Current.LoadTab(m_tab_id, PageType.Reader, ctx.Comic);
            });
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
                }
                else
                {
                    await RootPage.Current.LoadTab(m_tab_id, PageType.Search, "<dir:" + ctx.Folder + ">");
                }
            });
        }

        private void FolderItemRemove_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                FolderItemModel ctx = (FolderItemModel)((MenuFlyoutItem)sender).DataContext;
                await DataManager.UtilsRemoveFromFolders(ctx.Folder);
                await UpdateFolders();
            });
        }
    }
}
