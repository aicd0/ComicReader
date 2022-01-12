using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData
{
    public enum FavoriteNodeType
    {
        Item,
        Filter
    };

    public class FavoriteItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public FavoriteItemViewModel(string name, FavoriteNodeType type, FavoriteItemViewModel parent)
        {
            Name = name;
            EditingName = name;
            Parent = parent;
            Type = type;
            Children = new ObservableCollection<FavoriteItemViewModel>();
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

        public ObservableCollection<FavoriteItemViewModel> Children { get; set; }
        public FavoriteItemViewModel Parent { get; set; }
        public FavoriteNodeType Type { get; set; }
        public bool AllowDrop
        {
            get => Type == FavoriteNodeType.Filter;
            set { Type = value ? FavoriteNodeType.Filter : FavoriteNodeType.Item; }
        }
        public bool IsItem
        {
            get => Type == FavoriteNodeType.Item;
            set { Type = value ? FavoriteNodeType.Item : FavoriteNodeType.Filter; }
        }
    };
}
