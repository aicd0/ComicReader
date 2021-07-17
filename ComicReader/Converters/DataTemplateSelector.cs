using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Converters
{
    public class FavoritesItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CommonTemplate { get; set; }
        public DataTemplate RenamingTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FavoritesItemModel favoriteItem = (FavoritesItemModel)item;
            return favoriteItem.IsRenaming ? RenamingTemplate : CommonTemplate;
        }
    };

    public class FolderItemModelTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CommonTemplate { get; set; }
        public DataTemplate AddNewTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FolderItemModel folder_item = (FolderItemModel)item;
            return folder_item.IsAddNew ? AddNewTemplate : CommonTemplate;
        }
    };
}
