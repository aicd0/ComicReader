#define DEBUG_1

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Views
{
    public class ReaderControl
    {
        public ReaderControl(bool vertical, ScrollViewer scroll_viewer, ListView list_view)
        {
            DisplayInformation display_info = DisplayInformation.GetForCurrentView();

            m_vertical = vertical;
            m_default_value_initialized = false;
            ThisScrollViewer = scroll_viewer;
            ThisListView = list_view;
            LastViewportPerpendicularLength = display_info.ScreenWidthInRawPixels / display_info.RawPixelsPerViewPixel;
            IsAllImagesLoaded = false;
            IsScrollViewerInitialized = false;
            ImageSource = new ObservableCollection<ReaderFrameModel>();
        }

        private bool m_vertical;
        private bool m_use_absolute_value;
        private bool m_default_value_initialized;
        private double m_margin_start_final;
        private double m_margin_end_final;
        private double m_default_parallel_ratio;
        private double m_default_horizontal_offset;
        private double m_default_vertical_offset;
        private float m_default_zoom_factor;
        private bool m_default_disable_animation;

        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;
        public double LastViewportPerpendicularLength;

        // TRUE if ScrollViewer and ListView were set, and the first container was loaded
        public bool IsLayoutReady => ThisScrollViewer != null
            && ThisListView != null
            && ImageSource.Count > 0
            && ImageSource[0].Container != null;

        // TRUE if IsLayoutReady is true, and ScrollViewer were initialized
        public bool IsScrollViewerInitialized { get; set; }

        // TRUE if IsScrollViewerInitialized is true, and all containers were loaded
        public bool IsAllImagesLoaded { get; set; }

        public ObservableCollection<ReaderFrameModel> ImageSource { get; set; }

        // scroll viewer
        public float ZoomFactor => ThisScrollViewer.ZoomFactor;
        public float ZoomFactorFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_default_zoom_factor;
            }
            set
            {
                m_default_zoom_factor = value;
            }
        }
        public double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
        public double HorizontalOffsetFinal
        {
            get
            {
                if (m_use_absolute_value || m_vertical)
                {
                    InitializeDefaultValue();
                    return m_default_horizontal_offset;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = true;
                m_default_horizontal_offset = value;
            }
        }
        public double VerticalOffset => ThisScrollViewer.VerticalOffset;
        public double VerticalOffsetFinal
        {
            get
            {
                if (m_use_absolute_value || !m_vertical)
                {
                    InitializeDefaultValue();
                    return m_default_vertical_offset;
                }
                else
                {
                    return ParallelOffsetFinal;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = true;
                m_default_vertical_offset = value;
            }
        }
        public double ParallelRatioFinal
        {
            get
            {
                if (m_use_absolute_value)
                {
                    return (ParallelOffsetFinal + ViewportParallelLength * 0.5 - MarginStartFinal)
                        / (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal);
                }
                else
                {
                    InitializeDefaultValue();
                    return m_default_parallel_ratio;
                }
            }
            set
            {
                InitializeDefaultValue();
                m_use_absolute_value = false;
                m_default_parallel_ratio = value;
            }
        }
        public double ParallelOffset => m_vertical ? VerticalOffset : HorizontalOffset;
        public double ParallelOffsetFinal
        {
            get
            {
                if (m_use_absolute_value)
                {
                    return m_vertical ? VerticalOffsetFinal : HorizontalOffsetFinal;
                }
                else
                {
                    return ParallelRatioFinal * (ExtentParallelLengthFinal - MarginStartFinal - MarginEndFinal)
                        + MarginStartFinal - ViewportParallelLength * 0.5;
                }
            }
        }
        public double PerpendicularOffset => m_vertical ? ThisScrollViewer.HorizontalOffset : ThisScrollViewer.VerticalOffset;
        public double ViewportParallelLength => m_vertical ? ThisScrollViewer.ViewportHeight : ThisScrollViewer.ViewportWidth;
        public double ViewportPerpendicularLength => m_vertical ? ThisScrollViewer.ViewportWidth : ThisScrollViewer.ViewportHeight;
        public double ExtentParallelLength => m_vertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;
        public double ExtentParallelLengthFinal => FinalVal(ExtentParallelLength);

        // list view
        public double MarginStartFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_margin_start_final * ZoomFactorFinal;
            }
        }
        public double MarginEndFinal
        {
            get
            {
                InitializeDefaultValue();
                return m_margin_end_final * ZoomFactorFinal;
            }
        }
        public void SetMargin(double start, double end)
        {
            InitializeDefaultValue();
            m_margin_start_final = start;
            m_margin_end_final = end;
            if (m_vertical)
            {
                ThisListView.Margin = new Thickness(0.0, start, 0.0, end);
            }
            else
            {
                ThisListView.Margin = new Thickness(start, 0.0, end, 0.0);
            }
        }

        // grids
        public double GridParallelLength(int i) => m_vertical ? ImageSource[i].Container.ActualHeight : ImageSource[i].Container.ActualWidth;
        public double GridPerpendicularLength(int i) => m_vertical ? ImageSource[i].Container.ActualWidth : ImageSource[i].Container.ActualHeight;

        // helper functions
        public void SetOffset(ref double? horizontal_offset, ref double? vertical_offset, double? parallel_offset, double? perpendicular_offset)
        {
            if (m_vertical)
            {
                if (parallel_offset != null)
                {
                    vertical_offset = parallel_offset;
                }

                if (perpendicular_offset != null)
                {
                    horizontal_offset = perpendicular_offset;
                }
            }
            else
            {
                if (parallel_offset != null)
                {
                    horizontal_offset = parallel_offset;
                }

                if (perpendicular_offset != null)
                {
                    vertical_offset = perpendicular_offset;
                }
            }
        }
        public double? ParallelVal(double? horizontal_val, double? vertical_val) => m_vertical ? vertical_val : horizontal_val;
        public double? PerpendicularVal(double? horizontal_val, double? vertical_val) => m_vertical ? horizontal_val : vertical_val;
        public double? HorizontalVal(double? parallel_val, double? perpendicular_val) => m_vertical ? perpendicular_val : parallel_val;
        public double? VerticalVal(double? parallel_val, double? perpendicular_val) => m_vertical ? parallel_val : perpendicular_val;
        private double FinalVal(double val) => val / ZoomFactor * ZoomFactorFinal;

        public void Clear()
        {
            ImageSource.Clear();
        }
        public bool ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            if (horizontal_offset == null && vertical_offset == null && zoom_factor == null)
            {
                return true;
            }

            if (horizontal_offset != null)
            {
                HorizontalOffsetFinal = (double)horizontal_offset;
            }

            if (vertical_offset != null)
            {
                VerticalOffsetFinal = (double)vertical_offset;
            }

            if (zoom_factor != null)
            {
                ZoomFactorFinal = (float)zoom_factor;
            }

            if (disable_animation)
            {
                m_default_disable_animation = true;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("Commit:"
                + " Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_default_disable_animation
                + "\n");
#endif
            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_default_disable_animation);
            return true;
        }
        public bool ChangeView(float? zoom_factor, double parallel_ratio, bool disable_animation)
        {
            if (!IsLayoutReady)
            {
                return false;
            }

            ParallelRatioFinal = parallel_ratio;

            if (zoom_factor != null)
            {
                ZoomFactorFinal = (float)zoom_factor;
            }

            if (disable_animation)
            {
                m_default_disable_animation = true;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("Commit:"
                + " P=" + ParallelRatioFinal.ToString()
                + ",Z=" + ZoomFactorFinal.ToString()
                + ",H=" + HorizontalOffsetFinal.ToString()
                + ",V=" + VerticalOffsetFinal.ToString()
                + ",D=" + m_default_disable_animation
                + "\n");
#endif
            ThisScrollViewer.ChangeView(HorizontalOffsetFinal, VerticalOffsetFinal, ZoomFactorFinal, m_default_disable_animation);
            return true;
        }
        public void FinalValueExpired()
        {
            m_default_value_initialized = false;
        }

        // internal
        private void InitializeDefaultValue()
        {
            if (!IsLayoutReady)
            {
                return;
            }

            if (m_default_value_initialized)
            {
                return;
            }

            m_margin_start_final = m_vertical ? ThisListView.Margin.Top : ThisListView.Margin.Left;
            m_margin_end_final = m_vertical ? ThisListView.Margin.Bottom : ThisListView.Margin.Right;

            double margin_start = m_margin_start_final * ZoomFactor;
            double margin_end = m_margin_end_final * ZoomFactor;
            m_default_parallel_ratio = (ParallelOffset + ViewportParallelLength * 0.5 - margin_start)
                / (ExtentParallelLength - margin_start - margin_end);
            m_default_horizontal_offset = HorizontalOffset;
            m_default_vertical_offset = VerticalOffset;
            m_default_zoom_factor = ZoomFactor;
            m_default_disable_animation = false;

            m_default_value_initialized = true;
        }
    }

    public class ReaderPageShared : INotifyPropertyChanged
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

        // comic basic info
        private string m_ComicTitle1;
        public string ComicTitle1
        {
            get => m_ComicTitle1;
            set
            {
                m_ComicTitle1 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicTitle1"));
            }
        }

        private string m_ComicTitle2;
        public string ComicTitle2
        {
            get => m_ComicTitle2;
            set
            {
                m_ComicTitle2 = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicTitle2"));
            }
        }

        private string m_ComicPrimaryTitle;
        public string ComicPrimaryTitle
        {
            get => m_ComicPrimaryTitle;
            set
            {
                m_ComicPrimaryTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicPrimaryTitle"));
            }
        }

        private string m_ComicDir;
        public string ComicDir
        {
            get => m_ComicDir;
            set
            {
                m_ComicDir = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ComicDir"));
            }
        }

        private bool m_IsEditable;
        public bool IsEditable
        {
            get => m_IsEditable;
            set
            {
                m_IsEditable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditable"));
            }
        }

        // read record
        private double m_Rating;
        public double Rating
        {
            get => m_Rating;
            set
            {
                m_Rating = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Rating"));
            }
        }

        private string m_Progress;
        public string Progress
        {
            get => m_Progress;
            set
            {
                m_Progress = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Progress"));
                }
            }
        }

        // reader properties
        private bool m_IsOnePageReaderVisible;
        public bool IsOnePageReaderVisible
        {
            get => m_IsOnePageReaderVisible;
            set
            {
                m_IsOnePageReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsOnePageReaderVisible"));
            }
        }

        private bool m_IsTwoPagesReaderVisible;
        public bool IsTwoPagesReaderVisible
        {
            get => m_IsTwoPagesReaderVisible;
            set
            {
                m_IsTwoPagesReaderVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTwoPagesReaderVisible"));
            }
        }

        private bool m_IsGridViewVisible;
        public bool IsGridViewVisible
        {
            get => m_IsGridViewVisible;
            set
            {
                m_IsGridViewVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGridViewVisible"));
            }
        }

        public void UpdateReaderVisibility()
        {
            IsGridViewVisible = ContentPageShared.IsGridViewMode;
            IsOnePageReaderVisible = !ContentPageShared.IsGridViewMode && !ContentPageShared.IsTwoPagesMode;
            IsTwoPagesReaderVisible = !ContentPageShared.IsGridViewMode && ContentPageShared.IsTwoPagesMode;
        }
    }

    public sealed partial class ReaderPage : Page
    {
        public static ReaderPage Current;
        private bool m_page_initialized = false;
        private TabId m_tab_id;

        private ComicData m_comic;
        private ReadRecordData m_comic_record;

        private Utils.BackgroundTaskQueue m_load_image_queue = Utils.BackgroundTasks.EmptyQueue();
        private int m_page;
        private int m_zoom;
        private const int max_zoom = 200;
        private const int min_zoom = 90;
        private double m_position;
        private PointerPoint m_drag_pointer;
        private bool m_bottom_grid_showed;
        private bool m_bottom_grid_pinned;
        private Utils.CancellationLock m_reader_img_loader_lock = new Utils.CancellationLock();
        private Utils.CancellationLock m_preview_img_loader_lock = new Utils.CancellationLock();

        public ReaderPageShared Shared { get; set; }
        private ReaderControl OnePageReader { get; set; }
        private ReaderControl TwoPagesReader { get; set; }
        private ObservableCollection<ReaderFrameModel> GridViewDataSource { get; set; }
        private ObservableCollection<TagsModel> ComicTagSource { get; set; }

        public ReaderPage()
        {
            Current = this;
            m_comic = null;
            m_comic_record = null;
            m_page = -1;
            m_zoom = 90;
            m_position = 0.0;
            m_drag_pointer = null;
            m_bottom_grid_showed = false;
            m_bottom_grid_pinned = false;

            Shared = new ReaderPageShared();
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicPrimaryTitle = "";
            Shared.ComicDir = "";
            Shared.IsEditable = false;
            OnePageReader = new ReaderControl(true, OnePageVerticalScrollViewer, OnePageImageListView);
            TwoPagesReader = new ReaderControl(false, TwoPagesHorizontalScrollViewer, TwoPagesImageListView);
            GridViewDataSource = new ObservableCollection<ReaderFrameModel>();
            ComicTagSource = new ObservableCollection<TagsModel>();
            
            InitializeComponent();
        }

        // tab related

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!m_page_initialized)
            {
                m_page_initialized = true;
                NavigationParams p = (NavigationParams)e.Parameter;
                m_tab_id = p.TabId;
                Shared.ContentPageShared = (ContentPageShared)p.Shared;
                Shared.ContentPageShared.OnFavoritesButtonClicked += OnFavoritesBtClicked;
                Shared.ContentPageShared.RootPageShared.OnExitFullscreenMode += HideBottomGrid;
                Shared.ContentPageShared.OnTwoPagesModeChanged += Shared.UpdateReaderVisibility;
                Shared.ContentPageShared.OnTwoPagesModeChanged += OnTwoPagesModeChanged;
                Shared.ContentPageShared.OnGridViewModeChanged += Shared.UpdateReaderVisibility;
            }

            UpdateTabId();
            Shared.ContentPageShared.RootPageShared.CurrentPageType = PageType.Reader;
            Shared.ContentPageShared.IsGridViewMode = false;
            Shared.ContentPageShared.IsTwoPagesMode = false;
        }

        public static string GetPageUniqueString(object args)
        {
            ComicData comic = (ComicData)args;
            return "Reader/" + comic.Directory;
        }

        private void UpdateTabId()
        {
            if (m_comic == null)
            {
                return;
            }

            m_tab_id.Tab.Header = m_comic.Title;
            m_tab_id.Tab.IconSource = new muxc.SymbolIconSource() { Symbol = Symbol.Document };
            m_tab_id.UniqueString = GetPageUniqueString(m_comic);
            m_tab_id.Type = PageType.Reader;
        }

        // load comic
        public async Task LoadComic(ComicData comic)
        {
            if (comic == null)
            {
                return;
            }

            m_comic = comic;

            // additional procedures for internal comics
            if (!m_comic.IsExternal)
            {
                // fetch the read record. Create one if not exists
                m_comic_record = await DataManager.GetReadRecordWithId(m_comic.Id);
                if (m_comic_record == null)
                {
                    m_comic_record = new ReadRecordData
                    {
                        Id = m_comic.Id
                    };
                    Database.ComicRecords.Add(m_comic_record);
                }

                // add to history
                await DataManager.AddToHistory(m_comic.Id, m_comic.Title, true);

                // update "last visit"
                await DataManager.WaitLock();
                m_comic.LastVisit = DateTimeOffset.Now;
                DataManager.ReleaseLock();
                Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Comics));
            }

            UpdateTabId();
            LoadImages();
            await LoadComicInformation();
        }

        private void LoadImages()
        {
            if (!m_comic.IsExternal)
            {
                Utils.BackgroundTasks.AppendTask(DataManager.CompleteComicImagesSealed(m_comic), "Retriving images...", m_load_image_queue);
            }

            Utils.BackgroundTasks.AppendTask(LoadImagesAsyncSealed(), "Loading images...", m_load_image_queue);
        }

        private Func<Task<Utils.BackgroundTaskResult>, Utils.BackgroundTaskResult> LoadImagesAsyncSealed()
        {
            return delegate (Task<Utils.BackgroundTaskResult> _t) {
                var task = LoadImagesAsync();
                task.Wait();
                return task.Result;
            };
        }

        private async Task<Utils.BackgroundTaskResult> LoadImagesAsync()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                OnePageReader.Clear();
                TwoPagesReader.Clear();
                GridViewDataSource.Clear();
            });

            List<DataManager.ImageLoaderToken> reader_img_loader_tokens = new List<DataManager.ImageLoaderToken>();
            List<DataManager.ImageLoaderToken> preview_img_loader_tokens = new List<DataManager.ImageLoaderToken>();

            for (int i = 0; i < m_comic.ImageFiles.Count; ++i)
            {
                int page = i + 1;
                reader_img_loader_tokens.Add(new DataManager.ImageLoaderToken
                {
                    Comic = m_comic,
                    Index = i,
                    Callback = (BitmapImage img) =>
                    {
                        OnePageReader.ImageSource.Add(new ReaderFrameModel
                        {
                            OnContainerSet = OnImageContainerSet,
                            Image = img,
                            Page = page
                        });

                        TwoPagesReader.ImageSource.Add(new ReaderFrameModel
                        {
                            OnContainerSet = OnImageContainerSet,
                            Image = img,
                            Page = page
                        });
                    }
                });

                preview_img_loader_tokens.Add(new DataManager.ImageLoaderToken
                {
                    Comic = m_comic,
                    Index = i,
                    Callback = (BitmapImage img) =>
                    {
                        GridViewDataSource.Add(new ReaderFrameModel
                        {
                            Image = img,
                            Page = page
                        });
                    }
                });
            }

            Task reader_loader_task = null;
            Task preview_loader_task = null;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                double preview_width = (double)Resources["ReaderPreviewImageWidth"];
                double preview_height = (double)Resources["ReaderPreviewImageHeight"];
                reader_loader_task = DataManager.UtilsLoadImages(reader_img_loader_tokens, double.PositiveInfinity, double.PositiveInfinity, m_reader_img_loader_lock);
                preview_loader_task = DataManager.UtilsLoadImages(preview_img_loader_tokens, preview_width, preview_height, m_preview_img_loader_lock);
            });

            await reader_loader_task.AsAsyncAction();
            await preview_loader_task.AsAsyncAction();
            return new Utils.BackgroundTaskResult();
        }

        private async Task LoadComicInformation()
        {
            if (m_comic == null)
            {
                throw new Exception();
            }

            Shared.ContentPageShared.IsInLib = !m_comic.IsExternal;
            Shared.ComicTitle1 = m_comic.Title;
            Shared.ComicTitle2 = m_comic.Title2;
            Shared.ComicPrimaryTitle = Shared.ComicTitle2.Length == 0 ? Shared.ComicTitle1 : Shared.ComicTitle2;
            Shared.ComicDir = m_comic.Directory;
            Shared.IsEditable = !(m_comic.IsExternal && m_comic.InfoFile == null);
            Shared.Progress = "";
            LoadComicTag();

            if (!m_comic.IsExternal)
            {
                Shared.ContentPageShared.IsFavorite = await DataManager.GetFavoriteWithId(m_comic.Id) != null;
                Shared.Rating = m_comic_record.Rating;
            }
        }

        private void LoadComicTag()
        {
            if (m_comic == null)
            {
                return;
            }

            ComicTagSource.Clear();
            for (int i = 0; i < m_comic.Tags.Count; ++i)
            {
                TagData tags = m_comic.Tags[i];
                TagsModel tag_data = new TagsModel(tags.Name);

                foreach (string tag in tags.Tags)
                {
                    TagModel singleTagData = new TagModel(tag);
                    tag_data.Tags.Add(singleTagData);
                }

                ComicTagSource.Add(tag_data);
            }
        }

        // buttons
        public void ExpandInfoPane()
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        }

        private void OnInformationBtClicked(object sender, RoutedEventArgs e)
        {
            ExpandInfoPane();
        }

        private void OnInformationPaneVersionMenuItemSelected(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                MenuFlyoutItem item = (MenuFlyoutItem)sender;
                if (item.Text != m_comic.Id)
                {
                    ComicData comic = await DataManager.GetComicWithId(item.Text);
                    await RootPage.Current.LoadTab(null, PageType.Reader, comic);
                }
            });
        }

        private void OnSplitViewEditBtClicked(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (m_comic == null)
                {
                    return;
                }

                EditComicInfoDialog dialog = new EditComicInfoDialog(m_comic);
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    await LoadComicInformation();
                }
            });
        }

        private void OnFavoritesBtClicked()
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(!Shared.ContentPageShared.IsFavorite);
            });
        }

        public async Task SetIsFavorite(bool is_favorite)
        {
            if (Shared.ContentPageShared.IsFavorite == is_favorite)
            {
                return;
            }

            Shared.ContentPageShared.IsFavorite = is_favorite;

            if (is_favorite)
            {
                await DataManager.AddToFavorites(m_comic.Id, m_comic.Title, true);
            }
            else
            {
                await DataManager.RemoveFromFavoritesWithId(m_comic.Id, true);
            }
        }

        private void FavoriteBt_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(true);
            });
        }

        private void FavoriteBt_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await SetIsFavorite(false);
            });
        }

        // scroll viewer
        private void SetScrollViewerPage(ReaderControl control, int? page, ref double? horizontal_offset, ref double? vertical_offset)
        {
            if (page == null)
            {
                return;
            }

            if (page < 1)
            {
                page = 1;
            }
            else if (page > m_comic.ImageFiles.Count)
            {
                page = m_comic.ImageFiles.Count;
            }

            if (m_page == page)
            {
                return;
            }

            double page_parallel_offset = 0.0;

            for (int i = 0; i < page - 1; ++i)
            {
                if (control.ImageSource[i].Container == null)
                {
                    return;
                }
            }
            for (int i = 0; i < page - 1; ++i)
            {
                page_parallel_offset += control.GridParallelLength(i);
            }

            page_parallel_offset += control.GridParallelLength((int)page - 1) * 0.5;
            double parallel_offset = page_parallel_offset * control.ThisScrollViewer.ZoomFactor
                + control.MarginStartFinal - control.ViewportParallelLength * 0.5;
            control.SetOffset(ref horizontal_offset, ref vertical_offset, parallel_offset, null);
        }

        private double GetZoomCoefficient(ReaderControl control)
        {
            double viewport_ratio = control.ThisScrollViewer.ViewportWidth / control.ThisScrollViewer.ViewportHeight;
            double image_ratio = control.ImageSource[0].Container.ActualWidth / control.ImageSource[0].Container.ActualHeight;
            return 0.01 * (viewport_ratio > image_ratio
                ? control.ThisScrollViewer.ViewportHeight / control.ImageSource[0].Container.ActualHeight
                : control.ThisScrollViewer.ViewportWidth / control.ImageSource[0].Container.ActualWidth);
        }

        private void SetScrollViewerZoom(ReaderControl control, ref double? horizontal_offset, ref double? vertical_offset,
            ref int? zoom, out float? zoom_out)
        {
            if (control.ImageSource.Count == 0 || control.ImageSource[0].Container == null
                || control.ImageSource[0].Container.ActualWidth < 0.1
                || control.ImageSource[0].Container.ActualHeight < 0.1)
            {
                zoom = m_zoom;
                zoom_out = null;
                return;
            }

            double zoom_coefficient = GetZoomCoefficient(control);
            bool zoom_sat = false;
            bool zoom_null = zoom == null;
            int zoom_cpy = zoom_null ? (int)(control.ZoomFactorFinal / zoom_coefficient) : (int)zoom;

            // an error less than one is acceptable
            if (zoom_cpy - max_zoom > 1)
            {
                zoom_sat = true;
                zoom_cpy = max_zoom;
            }
            else if (min_zoom - zoom_cpy > 1)
            {
                zoom_sat = true;
                zoom_cpy = min_zoom;
            }

            if (zoom_null && !zoom_sat)
            {
                zoom = Math.Abs(zoom_cpy - m_zoom) <= 1 ? m_zoom : zoom_cpy;
                zoom_out = null;
                return;
            }

            zoom = zoom_cpy;
            zoom_out = (float)(zoom * zoom_coefficient);

            if (horizontal_offset == null)
            {
                horizontal_offset = control.HorizontalOffsetFinal;
            }

            if (vertical_offset == null)
            {
                vertical_offset = control.VerticalOffsetFinal;
            }

            horizontal_offset += control.ThisScrollViewer.ViewportWidth * 0.5;
            horizontal_offset *= (float)zoom_out / control.ZoomFactorFinal;
            horizontal_offset -= control.ThisScrollViewer.ViewportWidth * 0.5;

            vertical_offset += control.ThisScrollViewer.ViewportHeight * 0.5;
            vertical_offset *= (float)zoom_out / control.ZoomFactorFinal;
            vertical_offset -= control.ThisScrollViewer.ViewportHeight * 0.5;

            if (horizontal_offset < 0)
            {
                horizontal_offset = 0;
            }

            if (vertical_offset < 0)
            {
                vertical_offset = 0;
            }
        }

        private void _SetScrollViewer(ReaderControl control, int? zoom, bool disable_animation,
            int? page, double? parallel_ratio, double? horizontal_offset, double? vertical_offset)
        {
            if (!control.IsLayoutReady)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("ParamIn:"
                + " Z=" + zoom.ToString()
                + ",D=" + disable_animation.ToString()
                + ",Pg=" + page.ToString()
                + ",P=" + parallel_ratio.ToString()
                + ",H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + "\n");
#endif
            SetScrollViewerPage(control, page, ref horizontal_offset, ref vertical_offset);
            SetScrollViewerZoom(control, ref horizontal_offset, ref vertical_offset, ref zoom, out float? zoom_out);
#if DEBUG_1
            System.Diagnostics.Debug.Print("ParamOut:"
                + " H=" + horizontal_offset.ToString()
                + ",V=" + vertical_offset.ToString()
                + ",Z=" + zoom.ToString()
                + ",Zo=" + zoom_out.ToString()
                + ",D=" + disable_animation.ToString()
                + "\n");
#endif
            if (parallel_ratio != null)
            {
                control.ChangeView(zoom_out, (double)parallel_ratio, disable_animation);
            }
            else
            {
                control.ChangeView(zoom_out, horizontal_offset, vertical_offset, disable_animation);
            }

            m_zoom = (int)zoom;
            //ZoomTextBlock.Text = m_zoom.ToString() + "%";
            Shared.ContentPageShared.CanZoomIn = m_zoom < max_zoom;
            Shared.ContentPageShared.CanZoomOut = m_zoom > min_zoom;

            CorrectParallelOffset(control);
        }

        private void SetScrollViewer(ReaderControl control, int? zoom, bool disable_animation,
            double? horizontal_offset, double? vertical_offset)
        {
            _SetScrollViewer(control, zoom, disable_animation, null, null, horizontal_offset, vertical_offset);
        }

        private void SetScrollViewer(ReaderControl control, int? page, bool disable_animation)
        {
            _SetScrollViewer(control, null, disable_animation, page, null, null, null);
        }

        private void SetScrollViewer(ReaderControl control, int? zoom, bool disable_animation, double parallel_ratio)
        {
            _SetScrollViewer(control, zoom, disable_animation, null, parallel_ratio, null, null);
        }

        private bool CorrectParallelOffset(ReaderControl control)
        {
            if (control.ImageSource.Count == 0 || control.ImageSource[0].Container == null)
            {
                return false;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("CorrectOffset\n");
#endif
            double space = control.MarginStartFinal - control.ParallelOffsetFinal;
            double screen_center_offset = control.ViewportParallelLength * 0.5 + control.ParallelOffsetFinal;
            double image_center_offset = control.MarginStartFinal
                + control.GridParallelLength(0) * 0.5 * control.ZoomFactorFinal;
            double image_center_to_screen_center = image_center_offset - screen_center_offset;
            double movement_forward = Math.Min(space, image_center_to_screen_center);

            if (movement_forward > 0)
            {
                double parallel_offset = control.ParallelOffsetFinal + movement_forward;
                return control.ChangeView(null, control.HorizontalVal(parallel_offset, null),
                    control.VerticalVal(parallel_offset, null), false);
            }

            if (control.ImageSource[control.ImageSource.Count - 1].Container == null)
            {
                return false;
            }

            space = control.MarginEndFinal - (control.ExtentParallelLengthFinal
                - control.ParallelOffsetFinal - control.ViewportParallelLength);
            image_center_offset = control.ExtentParallelLengthFinal - control.MarginEndFinal
                - control.GridParallelLength(control.ImageSource.Count - 1) * 0.5 * control.ZoomFactorFinal;
            image_center_to_screen_center = screen_center_offset - image_center_offset;
            double movement_backward = Math.Min(space, image_center_to_screen_center);

            if (movement_backward > 0)
            {
                double parallel_offset = control.ParallelOffsetFinal - movement_backward;
                return control.ChangeView(null, control.HorizontalVal(parallel_offset, null),
                    control.VerticalVal(parallel_offset, null), false);
            }

            return false;
        }

        private ReaderControl GetCurrentReaderControl()
        {
            if (Shared.IsOnePageReaderVisible)
            {
                return OnePageReader;
            }
            else
            {
                return TwoPagesReader;
            }
        }

        private void ReaderControlSizeChanged(ReaderControl control, SizeChangedEventArgs e)
        {
            if (!control.IsLayoutReady)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("SizeChanged\n");
#endif
            UpdateReaderControlMargin(control);
            int zoom = (int)(m_zoom / control.ViewportPerpendicularLength * control.LastViewportPerpendicularLength);

            if (zoom > 100)
            {
                zoom = 100;
            }

            SetScrollViewer(control, zoom, true, m_position);
            control.LastViewportPerpendicularLength = control.ViewportPerpendicularLength;
        }

        private void ReaderControlViewChanged(ReaderControl control, ScrollViewerViewChangedEventArgs e)
        {
            if (!control.IsScrollViewerInitialized || e.IsIntermediate)
            {
                return;
            }

            control.FinalValueExpired();

            if (control.IsAllImagesLoaded)
            {
                m_position = control.ParallelRatioFinal;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("ViewChanged:"
                + " H=" + control.HorizontalOffsetFinal.ToString()
                + ",V=" + control.VerticalOffsetFinal.ToString()
                + ",Z=" + control.ZoomFactorFinal.ToString()
                + ",P=" + control.ParallelRatioFinal.ToString()
                + "\n");
#endif
            SetScrollViewer(control, null, false, null, null);
            UpdatePageFromOffset(control);
            UpdateProgress();
            SetBottomGridPin(false);
        }

        private void UpdateReaderControlMargin(ReaderControl control)
        {
            if (!control.IsLayoutReady)
            {
                return;
            }

            if (control.ImageSource.Count == 0)
            {
                return;
            }
#if DEBUG_1
            System.Diagnostics.Debug.Print("MarginUpdated\n");
#endif
            double zoom_coefficient = GetZoomCoefficient(control);
            double zoom_factor = min_zoom * zoom_coefficient;
            double inner_length = control.ViewportParallelLength / zoom_factor;
            double new_start = 0.0;

            if (control.ImageSource[0].Container != null)
            {
                new_start = (inner_length - control.GridParallelLength(0)) / 2;

                if (new_start < 0)
                {
                    new_start = 0;
                }
            }

            double new_end = 0.0;

            if (control.ImageSource[control.ImageSource.Count - 1].Container != null)
            {
                new_end = (inner_length - control.GridParallelLength(control.ImageSource.Count - 1)) / 2;

                if (new_end < 0)
                {
                    new_end = 0;
                }
            }

            control.SetMargin(new_start, new_end);
        }

        private void OnImageContainerSet(ReaderFrameModel ctx)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderControl control = GetCurrentReaderControl();
                bool all_loaded = false;

                if (control.ImageSource.Count == m_comic.ImageFiles.Count)
                {
                    all_loaded = true;
                    foreach (ReaderFrameModel d in control.ImageSource)
                    {
                        if (d.Container == null)
                        {
                            all_loaded = false;
                            break;
                        }
                    }
                }

                if (ctx.Page == 1)
                {
                    await Utils.Methods.WaitFor(() => control.IsLayoutReady);
#if DEBUG_1
                    System.Diagnostics.Debug.Print("FirstContainerSet\n");
#endif
                    UpdateReaderControlMargin(control);
                    SetScrollViewer(control, m_zoom, true, null, null);
                    control.IsScrollViewerInitialized = true;
                }

                if (all_loaded)
                {
                    await Utils.Methods.WaitFor(() => control.IsScrollViewerInitialized);
#if DEBUG_1
                    System.Diagnostics.Debug.Print("AllLoaded\n");
#endif
                    UpdateReaderControlMargin(control);
                    SetScrollViewer(control, null, true, null, null);
                    control.IsAllImagesLoaded = true;
                }
            });
        }

        private void OverwritePage(int page)
        {
            m_page = page;

            if (PageIndicator != null)
            {
                string image_count = "?";

                if (m_comic != null)
                {
                    image_count = m_comic.ImageFiles.Count.ToString();
                }

                PageIndicator.Text = m_page.ToString() + " of " + image_count;
            }
        }

        private void UpdatePageFromOffset(ReaderControl control)
        {
            if (!control.IsLayoutReady)
            {
                return;
            }

            double current_offset = (control.ParallelOffsetFinal - control.MarginStartFinal
                + control.ViewportParallelLength * 0.5) / control.ZoomFactorFinal;
            int page = 0;

            for (int i = 0; i < control.ImageSource.Count; ++i)
            {
                Grid grid = control.ImageSource[i].Container;

                if (grid == null)
                {
                    return;
                }

                current_offset -= control.GridParallelLength(i);

                if (current_offset < 0.0)
                {
                    page = i + 1;
                    break;
                }
            }

            OverwritePage(page);
        }

        // reader controls
        public void SetZoom(int level)
        {
            int zoom = m_zoom + 10 * level;
            ReaderControl control = GetCurrentReaderControl();
            SetScrollViewer(control, zoom, false, null, null);
        }

        private void Zoom_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(null);
            int delta = pt.Properties.MouseWheelDelta;
            SetZoom(delta / 120);
            e.Handled = true;
        }

        public void OnTwoPagesModeChanged()
        {
            Utils.Methods.Run(async delegate
            {
                double position = m_position;
                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsAllImagesLoaded);
                UpdateReaderControlMargin(control);

                int zoom = m_zoom;
                if (zoom > 100)
                {
                    zoom = 100;
                }

                SetScrollViewer(control, zoom, true, position);
            });
        }

        private void Page_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (OnePageVerticalScrollViewer == null)
            {
                return;
            }

            if (m_page <= 0)
            {
                return;
            }

            PointerPoint pt = e.GetCurrentPoint(null);
            int delta = pt.Properties.MouseWheelDelta;
            int page_backward = delta / 120;

            ReaderControl control = GetCurrentReaderControl();
            int new_page = m_page - page_backward;
            int? zoom = null;

            if (m_zoom > 100)
            {
                zoom = 100;
            }

            SetScrollViewer(control, new_page, false);
            SetScrollViewer(control, zoom, false, null, null);
            OverwritePage(new_page);
        }

        // reader events
        private void OnePageVerticalScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ReaderControlViewChanged(OnePageReader, e);
        }

        private void OnePageVerticalScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReaderControlSizeChanged(OnePageReader, e);
        }

        private void TwoPagesHorizontalScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReaderControlSizeChanged(TwoPagesReader, e);
        }

        private void TwoPagesHorizontalScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ReaderControlViewChanged(TwoPagesReader, e);
        }

        // other logics
        private void UpdateProgress()
        {
            int progress;

            if (m_comic.ImageFiles.Count == 0)
            {
                progress = 0;
            }
            else if (m_page == m_comic.ImageFiles.Count)
            {
                progress = 100;
            }
            else
            {
                progress = (int)((float)m_page / m_comic.ImageFiles.Count * 100);
            }

            if (progress > 100)
            {
                progress = 100;
            }

            Shared.Progress = progress.ToString() + "%";

            if (m_comic_record != null)
            {
                m_comic_record.Progress = progress;
                Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.ReadRecords));
            }
        }

        private void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            m_comic_record.Rating = (int)sender.Value;
            Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(Data.DatabaseItem.ReadRecords));
        }

        private void OnePageVerticalScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisScrollViewer = OnePageVerticalScrollViewer;
        }

        private void OnePageImageListView_Loaded(object sender, RoutedEventArgs e)
        {
            OnePageReader.ThisListView = OnePageImageListView;
        }

        private void TwoPagesHorizontalScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisScrollViewer = TwoPagesHorizontalScrollViewer;
        }

        private void TwoPagesImageListView_Loaded(object sender, RoutedEventArgs e)
        {
            TwoPagesReader.ThisListView = TwoPagesImageListView;
        }

        // panning
        private void ScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pt = e.GetCurrentPoint(PanningReference);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                return;
            }

            m_drag_pointer = pt;
            e.Handled = true;
        }

        private void ScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (m_drag_pointer == null)
            {
                return;
            }

            PointerPoint pt = e.GetCurrentPoint(PanningReference);

            if (!pt.Properties.IsLeftButtonPressed)
            {
                m_drag_pointer = null;
                return;
            }

            double dx = m_drag_pointer.Position.X - pt.Position.X;
            double dy = m_drag_pointer.Position.Y - pt.Position.Y;
            ReaderControl control = GetCurrentReaderControl();
            SetScrollViewer(control, null, false, control.HorizontalOffsetFinal + dx, control.VerticalOffsetFinal + dy);
            m_drag_pointer = pt;
            e.Handled = true;
        }

        private void ShowBottomGrid()
        {
            if (m_bottom_grid_showed)
            {
                return;
            }

            if (Shared.ContentPageShared.RootPageShared.IsFullscreenN)
            {
                return;
            }

            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 1.0);
            BottomGridStoryboard.Begin();
            m_bottom_grid_showed = true;
        }

        private void HideBottomGrid()
        {
            if (!m_bottom_grid_showed)
            {
                return;
            }

            BottomGridStoryboard.Children[0].SetValue(DoubleAnimation.ToProperty, 0.0);
            BottomGridStoryboard.Begin();
            m_bottom_grid_showed = false;
        }

        private void SetBottomGridPin(bool pinned)
        {
            m_bottom_grid_pinned = pinned;
            if (pinned)
            {
                ShowBottomGrid();
            }
            else
            {
                HideBottomGrid();
            }
        }

        private void BottomGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ShowBottomGrid();
        }

        private void BottomGrid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (m_bottom_grid_pinned)
            {
                return;
            }
            HideBottomGrid();
        }

        private void Reader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SetBottomGridPin(!m_bottom_grid_pinned);
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderFrameModel ctx = (ReaderFrameModel)e.ClickedItem;
                Shared.ContentPageShared.IsGridViewMode = false;
                ReaderControl control = GetCurrentReaderControl();
                await Utils.Methods.WaitFor(() => control.IsScrollViewerInitialized);
                SetScrollViewer(control, ctx.Page, true);
            });
        }
    }
}
