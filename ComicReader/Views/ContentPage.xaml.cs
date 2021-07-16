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

        private RootPageShared m_RootPageShared;
        public RootPageShared RootPageShared
        {
            get => m_RootPageShared;
            set
            {
                m_RootPageShared = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RootPageShared"));
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

        private bool m_IsInLib;
        public bool IsInLib
        {
            get => m_IsInLib;
            set
            {
                m_IsInLib = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsInLib"));
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

        public Action OnFavoritesButtonClicked;
    }

    public sealed partial class ContentPage : Page
    {
        public static ContentPage Current;
        private TabId m_tab_id;

        public ContentPageShared Shared { get; set; }

        public ContentPage()
        {
            Current = this;
            Shared = new ContentPageShared();

            InitializeComponent();
        }

        // navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            NavigationParams p = (NavigationParams)e.Parameter;
            m_tab_id = p.TabId;
            Shared.RootPageShared = (RootPageShared)p.Shared;
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

            NavigationParams nav_params = new NavigationParams
            {
                Shared = Shared,
                TabId = m_tab_id
            };

            _ = ContentFrame.Navigate(PageUtils.GetPageType(page_type), nav_params);
            object subpage = ContentFrame.Content;

            switch (page_type)
            {
                case PageType.Reader:
                    await ((ReaderPage)subpage).LoadComic((ComicData)param);
                    break;
                case PageType.Blank:
                    await ((BlankPage)subpage).UpdateInfo();
                    break;
                case PageType.Search:
                    await ((SearchResultsPage)subpage).StartSearch((string)param);
                    break;
                default:
                    throw new Exception();
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
            Shared.OnFavoritesButtonClicked?.Invoke();
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
