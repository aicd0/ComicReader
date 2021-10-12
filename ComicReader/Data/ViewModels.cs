using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            Id = "";
            Rating = -1;
            Progress = "";
            m_IsFavorite = false;
            m_IsImageLoaded = false;
        }

        public ComicData Comic;
        public BitmapImage Image { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Id { get; set; }
        public int Rating { get; set; }
        public string Progress { get; set; }
        public bool IsRatingVisible => Rating != -1;

        private bool m_IsFavorite;
        public bool IsFavorite
        {
            get => m_IsFavorite;
            set { m_IsFavorite = value; }
        }
        public bool IsFavoriteN => !m_IsFavorite;

        public bool IsHide => Comic.Hidden;
        public bool IsHideN => !IsHide;

        private bool m_IsImageLoaded;
        public bool IsImageLoaded
        {
            get => m_IsImageLoaded;
            set
            {
                m_IsImageLoaded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsImageLoaded"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsImageLoadedN"));
            }
        }
        public bool IsImageLoadedN => !m_IsImageLoaded;

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
            m_IsExpanded = false;
        }

        public string Name { get; set; }
        public string EditingName { get; set; }
        public string Id { get; set; }
        public bool IsRenaming { get; set; }

        private bool m_IsExpanded;
        public bool IsExpanded
        {
            get => m_IsExpanded;
            set
            {
                m_IsExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpandedN"));
            }
        }
        public bool IsExpandedN => !IsExpanded;

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
        public bool IsItemN => !IsItem;
    };

    public class HistoryItemModel
    {
        public string Id { get; set; }
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

        public int Page { get; set; }
        public bool TopPadding { get; set; }
        public bool BottomPadding { get; set; }
        public bool LeftPadding { get; set; }
        public bool RightPadding { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        private BitmapImage m_Image;
        public BitmapImage Image {
            get => m_Image;
            set
            {
                m_Image = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Image"));
            }
        }

        public Grid Container;

        // events
        public Action<ReaderFrameModel> OnContainerSet;
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
        public RoutedEventHandler E_Clicked { get; set; }
    };

    public class FolderItemModel
    {
        public string Folder { get; set; }
        public bool IsAddNew { get; set; }
        public bool IsAddNewN { get => !IsAddNew; }

        // events
        public PointerEventHandler OnItemPressed { get; set; }
        public RoutedEventHandler OnRemoveClicked { get; set; }

        // methods
        public static Func<FolderItemModel, FolderItemModel, bool> ContentEquals = delegate (FolderItemModel a, FolderItemModel b)
        {
            return a.Folder == b.Folder && a.IsAddNew == b.IsAddNew;
        };
    }
}