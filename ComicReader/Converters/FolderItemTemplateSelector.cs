using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Converters
{
    public class FolderItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalTemplate { get; set; }
        public DataTemplate AddNewTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FolderItemViewModel folder_item = (FolderItemViewModel)item;
            return folder_item.IsAddNew ? AddNewTemplate : NormalTemplate;
        }
    };
}