//#define DEBUG_1

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
        public ReaderControl(bool vertical, bool one_page, ScrollViewer scroll_viewer, ListView list_view)
        {
            DisplayInformation display_info = DisplayInformation.GetForCurrentView();

            m_vertical = vertical;
            m_one_page = one_page;
            m_default_value_initialized = false;
            PageCount = 0;
            ThisScrollViewer = scroll_viewer;
            ThisListView = list_view;
            LastViewportPerpendicularLength = display_info.ScreenWidthInRawPixels / display_info.RawPixelsPerViewPixel;
            ImageSource = new ObservableCollection<ReaderImageData>();
        }

        private bool m_vertical;
        private bool m_one_page;
        private bool m_use_absolute_value;
        private bool m_default_value_initialized;
        private double m_margin_start_final;
        private double m_margin_end_final;
        private double m_default_parallel_ratio;
        private double m_default_horizontal_offset;
        private double m_default_vertical_offset;
        private float m_default_zoom_factor;
        private bool m_default_disable_animation;

        public int PageCount;
        public ScrollViewer ThisScrollViewer;
        public ListView ThisListView;
        public double LastViewportPerpendicularLength;

        public bool IsReady => ThisScrollViewer != null
            && ThisListView != null
            && ImageSource.Count > 0
            && ImageSource[0].Container != null;
        public ObservableCollection<ReaderImageData> ImageSource { get; set; }

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

        // other properties
        private int IndexFromPageRaw(int page) => m_one_page ? page - 1 : page / 2;
        public int IndexFromPage(int page) => IndexFromPageRaw(PageFromIndex(IndexFromPageRaw(page)));
        private int PageFromIndexRaw(int index) => m_one_page ? index + 1 : (index == 0 ? 1 : index * 2);
        public int PageFromIndex(int index)
        {
            int page = PageFromIndexRaw(index);

            if (page < 1)
            {
                page = 1;
            }
            else if (page > PageCount)
            {
                page = PageCount;
            }

            return page;
        }

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
        public async Task WaitForReady()
        {
            await Task.Run(delegate
            {
                SpinWait sw = new SpinWait();
                while (!IsReady)
                {
                    sw.SpinOnce();
                }
            });
        }
        public bool ChangeView(float? zoom_factor, double? horizontal_offset, double? vertical_offset, bool disable_animation)
        {
            if (!IsReady)
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
            if (!IsReady)
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
            if (!IsReady)
            {
                throw new Exception();
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

        private bool m_IsInternal;
        public bool IsInternal
        {
            get => m_IsInternal;
            set
            {
                m_IsInternal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsInternal"));
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

        private bool m_IsFavorite;
        public bool IsFavorite
        {
            get => m_IsFavorite;
            set
            {
                m_IsFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFavorite"));
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
        private bool m_IsOnePageMode;
        public bool IsOnePageMode
        {
            get => m_IsOnePageMode;
            set
            {
                m_IsOnePageMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsOnePageMode"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTwoPagesMode"));
            }
        }
        public bool IsTwoPagesMode
        {
            get => !m_IsOnePageMode;
            set
            {
                m_IsOnePageMode = !value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsOnePageMode"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTwoPagesMode"));
            }
        }

        private bool m_CanZoomIn;
        public bool CanZoomIn
        {
            get => m_CanZoomIn;
            set
            {
                m_CanZoomIn = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanZoomIn"));
            }
        }

        private bool m_CanZoomOut;
        public bool CanZoomOut
        {
            get => m_CanZoomOut;
            set
            {
                m_CanZoomOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanZoomOut"));
            }
        }
    }

    public sealed partial class ReaderPage : Page
    {
        public static ReaderPage Current;
        TabId m_tab_id;

        private ComicData m_comic;
        private ComicRecordData m_comic_record;

        private Utils.BackgroundTaskQueue m_load_image_queue = Utils.BackgroundTasks.EmptyQueue();
        private int m_page;
        private int m_zoom;
        private const int max_zoom = 200;
        private const int min_zoom = 90;
        private double m_position;
        PointerPoint m_drag_pointer;

        public ReaderPageShared Shared { get; set; }
        private ReaderControl OnePageReader { get; set; }
        private ReaderControl TwoPagesReader { get; set; }
        private ObservableCollection<ReaderTagData> ComicTagSource { get; set; }

        public ReaderPage()
        {
            Current = this;
            m_comic = null;
            m_comic_record = null;
            m_page = -1;
            m_zoom = 90;
            m_position = 0.0;
            m_drag_pointer = null;

            Shared = new ReaderPageShared();
            Shared.ComicTitle1 = "";
            Shared.ComicTitle2 = "";
            Shared.ComicPrimaryTitle = "";
            Shared.ComicDir = "";
            Shared.IsInternal = false;
            Shared.IsEditable = false;
            Shared.IsOnePageMode = true;
            OnePageReader = new ReaderControl(true, true, OnePageVerticalScrollViewer, OnePageImageListView);
            TwoPagesReader = new ReaderControl(false, false, TwoPagesHorizontalScrollViewer, TwoPagesImageListView);
            ComicTagSource = new ObservableCollection<ReaderTagData>();
            
            InitializeComponent();
        }

        // tab related
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
            m_tab_id.Tab.IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource() { Symbol = Symbol.Document };
            m_tab_id.UniqueString = GetPageUniqueString(m_comic);
            m_tab_id.Type = PageType.Reader;
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_id = (TabId)e.Parameter;
            UpdateTabId();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
        }

        // load comic
        public async Task LoadComic(ComicData comic)
        {
            if (comic == null)
            {
                return;
            }

            m_comic = comic;

            // additional actions for internal comics
            if (!m_comic.IsExternal)
            {
                m_comic_record = await DataManager.GetComicRecordWithId(m_comic.Id);

                if (m_comic_record == null)
                {
                    m_comic_record = new ComicRecordData
                    {
                        Id = m_comic.Id
                    };
                    Database.ComicRecords.Add(m_comic_record);
                }

                await DataManager.AddToHistory(m_comic.Id, true);
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

        private Func<Task<int>, int> LoadImagesAsyncSealed()
        {
            return delegate (Task<int> _t) {
                var task = LoadImagesAsync();
                task.Wait();
                return task.Result;
            };
        }

        private async Task<int> LoadImagesAsync()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            delegate
            {
                OnePageReader.Clear();
                TwoPagesReader.Clear();
            });

            int page_count = m_comic.ImageFiles.Count;
            OnePageReader.PageCount = page_count;
            TwoPagesReader.PageCount = page_count;

            for (int i = 0; i < page_count; ++i)
            {
                StorageFile img_file = m_comic.ImageFiles[i];
                IRandomAccessStream stream = await img_file.OpenAsync(FileAccessMode.Read);
                int index = i;
                BitmapImage image = null;
                Task task = null;

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                delegate
                {
                    image = new BitmapImage();
                    task = image.SetSourceAsync(stream).AsTask();
                });

                await task;

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                delegate
                {
                    OnePageReader.ImageSource.Add(new ReaderImageData
                    {
                        OnContainerSet = OnImageContainerSet,
                        Image1 = image,
                        Index = index
                    });

                    if (i == 0 || (i % 2 == 1 && i == m_comic.ImageFiles.Count - 1))
                    {
                        TwoPagesReader.ImageSource.Add(new ReaderImageData
                        {
                            OnContainerSet = OnImageContainerSet,
                            Image1 = image,
                            Index = TwoPagesReader.ImageSource.Count
                        });
                    }
                    else if (i % 2 == 0)
                    {
                        TwoPagesReader.ImageSource.Add(new ReaderImageData
                        {
                            OnContainerSet = OnImageContainerSet,
                            Image1 = OnePageReader.ImageSource[OnePageReader.ImageSource.Count - 2].Image1,
                            Image2 = image,
                            Index = TwoPagesReader.ImageSource.Count
                        });
                    }
                });
            }

            return 0;
        }

        private async Task LoadComicInformation()
        {
            if (m_comic == null)
            {
                throw new Exception();
            }

            Shared.ComicTitle1 = m_comic.Title;
            Shared.ComicTitle2 = m_comic.Title2;
            Shared.ComicPrimaryTitle = Shared.ComicTitle2.Length == 0 ? Shared.ComicTitle1 : Shared.ComicTitle2;
            Shared.ComicDir = m_comic.Directory;
            Shared.IsInternal = !m_comic.IsExternal;
            Shared.IsEditable = !(m_comic.IsExternal && m_comic.InfoFile == null);
            Shared.Progress = "";
            LoadComicTag();

            if (!m_comic.IsExternal)
            {
                // load versions
                if (m_comic.ComicCollectionId != null)
                {
                    ComicCollectionData collection = await DataManager.GetComicCollectionWithId(m_comic.ComicCollectionId);

                    if (collection == null)
                    {
                        throw new Exception();
                    }

                    List<string> ids = collection.ComicIds;
                    InformationPaneVersionMenuFlyout.Items.Clear();

                    for (int i = 0; i < ids.Count; ++i)
                    {
                        string id = ids[i];
                        if (id == m_comic.Id)
                        {
                            continue;
                        }

                        MenuFlyoutItem item = new MenuFlyoutItem
                        {
                            Text = id
                        };

                        item.Click += OnInformationPaneVersionMenuItemSelected;
                        InformationPaneVersionMenuFlyout.Items.Add(item);
                    }

                    InformationPaneVersionMenuFlyout.Items.Add(new MenuFlyoutSeparator());

                    MenuFlyoutItem editBt = new MenuFlyoutItem
                    {
                        Text = "Edit"
                    };

                    InformationPaneVersionMenuFlyout.Items.Add(editBt);

                    if (ids.Count == 2)
                    {
                        InformationPaneVersionPromptTextBlock.Text = "(1 other version)";
                    }
                    else
                    {
                        string versions_count = (ids.Count - 1).ToString();
                        InformationPaneVersionPromptTextBlock.Text = "(" + versions_count + " other versions)";
                    }
                    InformationPaneMoreVersionGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    InformationPaneMoreVersionGrid.Visibility = Visibility.Collapsed;
                }

                Shared.IsFavorite = await DataManager.GetFavoriteWithId(m_comic.Id) != null;
                Shared.Rating = m_comic_record.Rating;
            }
            else
            {
                // external comics
                InformationPaneMoreVersionGrid.Visibility = Visibility.Collapsed;
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
                ReaderTagData tag_data = new ReaderTagData(tags.Name);

                foreach (string tag in tags.Tags)
                {
                    ReaderSingleTagData singleTagData = new ReaderSingleTagData(tag);
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

        public async Task SetIsFavorite(bool is_favorite)
        {
            if (Shared.IsFavorite == is_favorite)
            {
                return;
            }

            Shared.IsFavorite = is_favorite;

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
            int index_final = control.IndexFromPage((int)page);

            for (int i = 0; i <= index_final; ++i)
            {
                if (control.ImageSource[i].Container == null)
                {
                    return;
                }
            }

            for (int i = 0; i < index_final; ++i)
            {
                page_parallel_offset += control.GridParallelLength(i);
            }

            page_parallel_offset += control.GridParallelLength(index_final) * 0.5;
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
            if (!control.IsReady)
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
            ZoomTextBlock.Text = m_zoom.ToString() + "%";
            Shared.CanZoomIn = m_zoom < max_zoom;
            Shared.CanZoomOut = m_zoom > min_zoom;

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
            if (Shared.IsOnePageMode)
            {
                return OnePageReader;
            }
            else if (Shared.IsTwoPagesMode)
            {
                return TwoPagesReader;
            }

            return null;
        }

        private void ReaderControlSizeChanged(ReaderControl control, SizeChangedEventArgs e)
        {
            if (!control.IsReady)
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
            if (!control.IsReady)
            {
                return;
            }

            control.FinalValueExpired();
            m_position = control.ParallelRatioFinal;

            if (e.IsIntermediate)
            {
                return;
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
        }

        private void UpdateReaderControlMargin(ReaderControl control)
        {
            if (!control.IsReady)
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

        private void OnImageContainerSet(ReaderImageData ctx)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderControl control = GetCurrentReaderControl();
                bool all_loaded = false;

                if (control.ImageSource.Count == m_comic.ImageFiles.Count)
                {
                    all_loaded = true;
                    foreach (ReaderImageData d in control.ImageSource)
                    {
                        if (d.Container == null)
                        {
                            all_loaded = false;
                            break;
                        }
                    }
                }

                if (ctx.Index == 0)
                {
#if DEBUG_1
                    System.Diagnostics.Debug.Print("FirstContainerSet\n");
#endif
                    await control.WaitForReady();
                    UpdateReaderControlMargin(control);
                    SetScrollViewer(control, m_zoom, true, null, null);
                }

                if (all_loaded)
                {
                    UpdateReaderControlMargin(control);
                    SetScrollViewer(control, null, true, null, null);
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

                PageIndicator.Text = m_page.ToString() + "/" + image_count;
            }
        }

        private void UpdatePageFromOffset(ReaderControl control)
        {
            if (!control.IsReady)
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
                    page = control.PageFromIndex(i);
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

        public async Task SetOnePageMode(bool val)
        {
            if (Shared.IsOnePageMode == val)
            {
                return;
            }

            double position = m_position;
            Shared.IsOnePageMode = val;
            ReaderControl control = GetCurrentReaderControl();

            await control.WaitForReady();
            UpdateReaderControlMargin(control);

            int zoom = m_zoom;
            if (zoom > 100)
            {
                zoom = 100;
            }

            SetScrollViewer(control, zoom, true, position);
        }

        private void BottomControl_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BottomControlStoryboard2.Children[0].SetValue(DoubleAnimation.ToProperty, 1.0);
            BottomControlStoryboard2.Begin();
        }

        private void BottomControl_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BottomControlStoryboard2.Children[0].SetValue(DoubleAnimation.ToProperty, 0.0);
            BottomControlStoryboard2.Begin();
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
            int index_backward = delta / 120;

            ReaderControl control = GetCurrentReaderControl();
            int new_page = control.PageFromIndex(control.IndexFromPage(m_page) - index_backward);
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

            if (m_comic_record != null && progress > m_comic_record.Progress)
            {
                m_comic_record.Progress = progress;
                Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.ComicRecords));
            }
        }

        private void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            m_comic_record.Rating = (int)sender.Value;
            Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(Data.DatabaseItem.ComicRecords));
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
    }
}
