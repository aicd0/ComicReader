using ComicReader.Database;
using ComicReader.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.ComponentModel;

namespace ComicReader.DesignData
{
    internal class ComicItemViewModel : INotifyPropertyChanged
    {
        //
        // events
        //

        public event PropertyChangedEventHandler PropertyChanged;

        //
        // properties
        //

        public ComicData Comic;
        public string Title { get; set; }
        public string Detail { get; set; }
        public int Rating { get; set; } = -1;

        private string _progress = string.Empty;
        public string Progress
        {
            get => _progress;
            private set
            {
                _progress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Progress"));
            }
        }

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
        public bool IsRead => Comic.IsRead;
        public bool IsUnread => Comic.IsUnread;

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

        //
        // functions
        //

        public void UpdateProgress(bool compat)
        {
            if (Comic.IsUnread)
            {
                Progress = StringResourceProvider.GetResourceString("Unread");
            }
            else if (Comic.IsRead)
            {
                Progress = StringResourceProvider.GetResourceString("Finished");
            }
            else
            {
                if (compat)
                {
                    Progress = Comic.Progress.ToString() + "%";
                }
                else
                {
                    Progress = StringResourceProvider.GetResourceString("FinishPercentage")
                        .Replace("$percentage", Comic.Progress.ToString());
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRead"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsUnread"));
        }

        //
        // handler
        //

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

            void OnMarkAsReadClicked(object sender, RoutedEventArgs e);

            void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e);

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
                    _instance ??= new EmptyItemHandler();
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

            public void OnMarkAsReadClicked(object sender, RoutedEventArgs e)
            {
            }

            public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
            {
            }
        }
    };
}
