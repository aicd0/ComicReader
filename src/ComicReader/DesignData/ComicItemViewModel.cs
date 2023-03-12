using ComicReader.Database;
using ComicReader.Utils;
using System;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData
{
    internal class ComicItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ComicData Comic;
        public string Title { get; set; }
        public string Detail { get; set; }
        public int Rating { get; set; } = -1;
        public string Progress { get; set; }

        private bool m_IsFavorite = false;
        public bool IsFavorite
        {
            get => m_IsFavorite;
            set
            {
                m_IsFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFavorite"));
            }
        }

        private ReaderImageViewModel _image = new ReaderImageViewModel();
        public ReaderImageViewModel Image => _image;

        public bool IsRatingVisible => Rating != -1;
        public bool IsHide => Comic.Hidden;

        private bool m_IsSelectMode = false;
        public bool IsSelectMode
        {
            get => m_IsSelectMode;
            set
            {
                m_IsSelectMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelectMode"));
            }
        }

        // handlers
        private WeakReference<IItemHandler> _itemHandler;
        public IItemHandler ItemHandler
        {
            get
            {
                return _itemHandler?.Get() ?? EmptyItemHandler.Instance;
            }
            set
            {
                _itemHandler = new WeakReference<IItemHandler>(value);
            }
        }

        public interface IItemHandler
        {
            void OnItemTapped(object sender, TappedRoutedEventArgs e);

            void OnOpenInNewTabClicked(object sender, RoutedEventArgs e);

            void OnAddToFavoritesClicked(object sender, RoutedEventArgs e);

            void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e);

            void OnHideClicked(object sender, RoutedEventArgs e);

            void OnUnhideClicked(object sender, RoutedEventArgs e);

            void OnSelectClicked(object sender, RoutedEventArgs e);
        }

        private class EmptyItemHandler : IItemHandler
        {
            private EmptyItemHandler()
            {
            }

            private static IItemHandler _instance;
            public static IItemHandler Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new EmptyItemHandler();
                    }
                    return _instance;
                }
            }

            public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnHideClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnItemTapped(object sender, TappedRoutedEventArgs e)
            {
            }

            public void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnSelectClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnUnhideClicked(object sender, RoutedEventArgs e)
            {
            }
        }
    };
}
