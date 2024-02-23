using ComicReader.Common.Router;
using ComicReader.Views;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ComicReader.Controls
{
    sealed internal partial class SidePane : UserControl
    {
        public NavigationPage Ctx => DataContext as NavigationPage;

        public SidePane()
        {
            InitializeComponent();
        }

        private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            string item = (string)((NavigationViewItem)args.SelectedItem).Content;

            var nav_params = new NavigationParams
            {
                TabId = Ctx.TabId,
                Params = Ctx.Shared,
            };
            Type page_type = item switch
            {
                "Favorites" => typeof(FavoritePage),
                "History" => typeof(HistoryPage),
                _ => throw new Exception(),
            };
            _ = ContentFrame.Navigate(page_type, nav_params);
        }
    }
}
