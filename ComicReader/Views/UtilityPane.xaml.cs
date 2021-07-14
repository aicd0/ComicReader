using System;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Views
{
    public sealed partial class UtilityPane : UserControl
    {
        public static UtilityPane Current = null;
        private bool m_first_load = true;

        public UtilityPane()
        {
            Current = this;
            InitializeComponent();
        }

        private void ContentFrame_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (m_first_load)
            {
                ContentFrame.Navigate(typeof(FavoritesPage), null);
                m_first_load = false;
            }
        }

        private void OnNavPaneSelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender,
            Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            var item = (string)((Microsoft.UI.Xaml.Controls.NavigationViewItem)args.SelectedItem).Content;

            if (item == "Favorites")
            {
                ContentFrame.Navigate(typeof(FavoritesPage), null);
            }
            else if (item == "History")
            {
                ContentFrame.Navigate(typeof(HistoryPage), null);
            }
            else
            {
                throw new Exception();
            }
        }
    }
}
