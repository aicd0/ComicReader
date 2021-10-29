﻿using System;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;
using ComicReader.Views;

namespace ComicReader.Controls
{
    public sealed partial class UtilityPane : UserControl
    {
        public static UtilityPane Current = null;
        private bool m_first_page_loaded = false;

        public ContentPage Ctx => DataContext as ContentPage;

        public UtilityPane()
        {
            Current = this;
            InitializeComponent();
        }

        private void ContentFrame_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (!m_first_page_loaded)
            {
                ContentFrame.Navigate(typeof(FavoritesPage), null);
                m_first_page_loaded = true;
            }
        }

        private void OnNavPaneSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            string item = (string)((muxc.NavigationViewItem)args.SelectedItem).Content;

            if (item == "Favorites")
            {
                ContentFrame.Navigate(typeof(FavoritesPage), Ctx);
            }
            else if (item == "History")
            {
                ContentFrame.Navigate(typeof(HistoryPage), Ctx);
            }
            else
            {
                throw new Exception();
            }
        }
    }
}