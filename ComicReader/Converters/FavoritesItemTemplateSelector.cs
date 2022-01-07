using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Converters
{
    public class FavoritesItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalTemplate { get; set; }
        public DataTemplate RenamingTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FavoriteItemViewModel model = item as FavoriteItemViewModel;
            return model.IsRenaming ? RenamingTemplate : NormalTemplate;
        }
    };
}