using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Converters
{
    public class FavoritesItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CommonTemplate { get; set; }
        public DataTemplate RenamingTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FavoritesItemModel favoriteItem = (FavoritesItemModel)item;
            return favoriteItem.IsRenaming ? RenamingTemplate : CommonTemplate;
        }
    };
}