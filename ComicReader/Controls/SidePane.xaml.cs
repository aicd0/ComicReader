using System;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Views;

namespace ComicReader.Controls
{
    public sealed partial class SidePane : UserControl
    {
        public static SidePane Current = null;
        public ContentPage Ctx => DataContext as ContentPage;

        public SidePane()
        {
            Current = this;

            InitializeComponent();
        }

        private void ContentFrame_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //ContentFrame.Navigate(typeof(FavoritesPage), null);
        }

        private void OnNavPaneSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            string item = (string)((muxc.NavigationViewItem)args.SelectedItem).Content;
            
            Utils.Tab.NavigationParams nav_params = new Utils.Tab.NavigationParams
            {
                TabId = Ctx.TabId,
                Shared = Ctx.Shared,
            };

            Type page_type;

            switch (item)
            {
                case "Favorites":
                    page_type = typeof(FavoritesPage);
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
