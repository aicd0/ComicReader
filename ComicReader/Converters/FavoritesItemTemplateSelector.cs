using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Converters
{
    public class FavoritesItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CommonTemplate { get; set; }
        public DataTemplate RenamingTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FavoriteItemViewModel favoriteItem = (FavoriteItemViewModel)item;
            return favoriteItem.IsRenaming ? RenamingTemplate : CommonTemplate;
        }
    };
}