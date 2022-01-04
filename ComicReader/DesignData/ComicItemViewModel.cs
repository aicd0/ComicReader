using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Database;

namespace ComicReader.DesignData
{
    public class ComicItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ComicItemViewModel()
        {
            Title = "";
            Detail = "";
            Rating = -1;
            Progress = "";
            IsFavorite = false;
            Image = new BitmapImage();
            m_IsImageLoaded = false;
        }

        public ComicData Comic;
        public string Title { get; set; }
        public string Detail { get; set; }
        public int Rating { get; set; }
        public string Progress { get; set; }

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

        public BitmapImage Image { get; set; }

        public bool IsRatingVisible => Rating != -1;
        public bool IsHide => Comic.Hidden;

        private bool m_IsImageLoaded;
        public bool IsImageLoaded
        {
            get => m_IsImageLoaded;
            set
            {
                m_IsImageLoaded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsImageLoaded"));
            }
        }

        // events
        public PointerEventHandler OnItemPressed { get; set; }
        public RoutedEventHandler OnOpenInNewTabClicked { get; set; }
        public RoutedEventHandler OnAddToFavoritesClicked { get; set; }
        public RoutedEventHandler OnRemoveFromFavoritesClicked { get; set; }
        public RoutedEventHandler OnHideClicked { get; set; }
        public RoutedEventHandler OnUnhideClicked { get; set; }
    };
}
