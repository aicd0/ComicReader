using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Converters
{
    public class FolderItemTemplateSelector : DataTemplateSelector
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