using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.Data
{
    public class ComicItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ComicItemModel()
        {
            Image = new BitmapImage();
            Title = "";
            Detail = "";
            Id = -1;
            Rating = -1;
            Progress = "";
            IsFavorite = false;
            m_IsImageLoaded = false;
        }

        public ComicData Comic;
        public BitmapImage Image { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public long Id { get; set; }
        public int Rating { get; set; }
        public string Progress { get; set; }
        public bool IsRatingVisible => Rating != -1;
        public bool IsFavorite { get; set; }
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
        public RoutedEventHandler OnHideClicked { get; set; }
        public RoutedEventHandler OnUnhideClicked { get; set; }
        public RoutedEventHandler OnAddToFavoritesClicked { get; set; }
        public RoutedEventHandler OnRemoveFromFavoritesClicked { get; set; }
    };

    public enum TreeItemType { Item, Filter };

    public class FavoritesItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public FavoritesItemModel(string name, TreeItemType type, FavoritesItemModel parent)
        {
            Name = name;
            EditingName = name;
            Parent = parent;
            Type = type;
            Children = new ObservableCollection<FavoritesItemModel>();
            IsRenaming = false;
            m_Expanded = false;
        }

        public string Name { get; set; }
        public string EditingName { get; set; }
        public long Id { get; set; }
        public bool IsRenaming { get; set; }

        private bool m_Expanded;
        public bool Expanded
        {
            get => m_Expanded;
            set
            {
                m_Expanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Expanded"));
            }
        }

        public ObservableCollection<FavoritesItemModel> Children { get; set; }
        public FavoritesItemModel Parent { get; set; }
        public TreeItemType Type { get; set; }
        public bool AllowDrop
        {
            get => Type == TreeItemType.Filter;
            set { Type = value ? TreeItemType.Filter : TreeItemType.Item; }
        }
        public bool IsItem
        {
            get => Type == TreeItemType.Item;
            set { Type = value ? TreeItemType.Item : TreeItemType.Filter; }
        }
    };

    public class HistoryItemModel
    {
        public long Id { get; set; }
        public string Time { get; set; }
        public string Title { get; set; }
    }

    public class HistoryItemGroupModel : ObservableCollection<HistoryItemModel>
    {
        public HistoryItemGroupModel(string key) : base()
        {
            Key = key;
        }

        public string Key { get; set; }
    }

    public class ReaderFrameModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private BitmapImage m_ImageSource = null;
        public BitmapImage ImageSource
        {
            get => m_ImageSource;
            set
            {
                m_ImageSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageSource"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsImageVisible"));
            }
        }

        public bool IsImageVisible => ImageSource != null;

        private double m_ImageWidth = 0.0;
        public double ImageWidth
        {
            get => m_ImageWidth;
            set
            {
                m_ImageWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageWidth"));
            }
        }

        private double m_ImageHeight = 0.0;
        public double ImageHeight
        {
            get => m_ImageHeight;
            set
            {
                m_ImageHeight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageHeight"));
            }
        }

        public int Page { get; set; } = -1;
        public bool TopPadding { get; set; } = false;
        public bool BottomPadding { get; set; } = false;
        public bool LeftPadding { get; set; } = false;
        public bool RightPadding { get; set; } = false;

        private Thickness? m_Margin = null;
        public Thickness Margin
        {
            get
            {
                if (m_Margin == null)
                {
                    m_Margin = new Thickness(
                        LeftPadding ? 200 : 0,
                        TopPadding ? 10 : 0,
                        RightPadding ? 200 : 0,
                        BottomPadding ? 10 : 0);
                }

                return m_Margin.Value;
            }
        }

        public double Width => Container.ActualWidth;
        public double Height => Container.ActualHeight;
        public Grid Container = null;
        public Func<ReaderFrameModel, Task> OnContainerLoadedAsync;
    };

    public class TagsModel
    {
        public TagsModel(string name)
        {
            Name = name;
            Tags = new List<TagModel>();
        }

        public string Name { get; set; }
        public List<TagModel> Tags { get; set; }
    };

    public class TagModel
    {
        public string Tag { get; set; }
        public RoutedEventHandler OnClicked { get; set; }
    };

    public class FolderItemModel
    {
        public string Folder { get; set; }
        public bool IsAddNew { get; set; }

        // events
        public PointerEventHandler OnItemPressed { get; set; }
        public RoutedEventHandler OnRemoveClicked { get; set; }

        // methods
        public static Func<FolderItemModel, FolderItemModel, bool> ContentEquals = delegate (FolderItemModel a, FolderItemModel b)
        {
            if (a.IsAddNew != b.IsAddNew)
            {
                return false;
            }

            if (a.IsAddNew)
            {
                return true;
            }

            return a.Folder == b.Folder;
        };
    }
}