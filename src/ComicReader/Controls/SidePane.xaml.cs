using System;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Views;
using ComicReader.Common.Router;

namespace ComicReader.Controls
{
    sealed internal partial class SidePane : UserControl
    {
        public NavigationPage Ctx => DataContext as NavigationPage;

        public SidePane()
        {
            InitializeComponent();
        }

        private void OnNavPaneSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            string item = (string)((muxc.NavigationViewItem)args.SelectedItem).Content;
            
            NavigationParams nav_params = new NavigationParams
            {
                tabId = Ctx.TabId,
                shared = Ctx.Shared,
            };

            Type page_type;
            switch (item)
            {
                case "Favorites":
                    page_type = typeof(FavoritePage);
                    break;
                case "History":
                    page_type = typeof(HistoryPage);
                    break;
                default:
                    throw new Exception();
            }

            _ = ContentFrame.Navigate(page_type, nav_params);
        }
    }
}
