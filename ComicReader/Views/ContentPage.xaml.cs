using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ComicReader.Data;

namespace ComicReader.Views
{
    public class ContentPageShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ReaderPageShared m_SharedReaderPage;
        public ReaderPageShared SharedReaderPage
        {
            get => m_SharedReaderPage;
            set
            {
                m_SharedReaderPage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SharedReaderPage"));
            }
        }

        private bool m_IsReaderPage;
        public bool IsReaderPage
        {
            get => m_IsReaderPage;
            set
            {
                m_IsReaderPage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsReaderPage"));
            }
        }

        private bool m_IsFullscreen;
        public bool IsFullscreen
        {
            get => m_IsFullscreen;
            set
            {
                m_IsFullscreen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFullscreen"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFullscreenN"));
            }
        }
        public bool IsFullscreenN => !IsFullscreen;
    }

    public sealed partial class ContentPage : Page
    {
        public static ContentPage Current;
        private TabId m_tab_id;

        private object m_subpage;

        public ContentPageShared Shared { get; set; }

        public ContentPage()
        {
            Current = this;

            Shared = new ContentPageShared();
            Shared.SharedReaderPage = new ReaderPageShared();
            Shared.IsReaderPage = false;
            Shared.IsFullscreen = false;

            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_tab_id = (TabId)e.Parameter;
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {

        }

        public void SetSearchBox(string keywords)
        {
            SearchBox.Focus(FocusState.Programmatic);
            SearchBox.Text = keywords;
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            Utils.Methods.Run(async delegate
            {
                await RootPage.Current.LoadTab(m_tab_id, PageType.Search, args.QueryText);
            });
        }

        public async Task LoadPage(PageType page_type, object param = null)
        {
            if (ContentFrame == null)
            {
                return;
            }

            Shared.IsReaderPage = page_type == PageType.Reader;

            switch (page_type)
            {
                case PageType.Reader:
                    _ = ContentFrame.Navigate(typeof(ReaderPage), m_tab_id);
                    m_subpage = ContentFrame.Content;
                    Shared.SharedReaderPage = ((ReaderPage)m_subpage).Shared;
                    await ((ReaderPage)m_subpage).LoadComic((ComicData)param);
                    break;
                case PageType.Blank:
                    _ = ContentFrame.Navigate(typeof(BlankPage), m_tab_id);
                    m_subpage = ContentFrame.Content;
                    await ((BlankPage)m_subpage).UpdateInfo();
                    break;
                case PageType.Search:
                    _ = ContentFrame.Navigate(typeof(SearchResultsPage), m_tab_id);
                    m_subpage = ContentFrame.Content;
                    await ((SearchResultsPage)m_subpage).StartSearch((string)param);
                    break;
                default:
                    break;
            }
        }

        private void SettingBt_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await RootPage.Current.LoadTab(null, PageType.Settings);
            });
        }

        private void FavoriteBt_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                _UtilityPane.IsPaneOpen = !_UtilityPane.IsPaneOpen;

                if (_UtilityPane.IsPaneOpen)
                {
                    if (FavoritesPage.Current != null)
                    {
                        await FavoritesPage.Current.UpdateTreeExplorer();
                    }

                    if (HistoryPage.Current != null)
                    {
                        await HistoryPage.Current.UpdateHistory();
                    }
                }
            });
        }

        private void HomeClick(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                await RootPage.Current.LoadTab(m_tab_id, PageType.Blank);
            });
        }

        private void GoBackClick(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void GoForwardClick(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoForward)
            {
                ContentFrame.GoForward();
            }
        }

        private void TwoPagesModeBt_Changed(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                if (ReaderPage.Current == null)
                {
                    return;
                }

                bool? is_checked = ((AppBarToggleButton)sender).IsChecked;

                if (is_checked == null)
                {
                    return;
                }

                await ReaderPage.Current.SetOnePageMode(!(bool)is_checked);
            });
        }

        private void AddToFavoriteBt_Click(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                ReaderPage page = m_subpage as ReaderPage;
                await page.SetIsFavorite(!page.Shared.IsFavorite);
            });
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ReaderPage.Current.SetZoom(1);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ReaderPage.Current.SetZoom(-1);
        }

        private void ComicInfoBt_Click(object sender, RoutedEventArgs e)
        {
            ReaderPage.Current.ExpandInfoPane();
        }
    }
}
