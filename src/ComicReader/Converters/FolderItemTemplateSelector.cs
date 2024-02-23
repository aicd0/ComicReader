using ComicReader.DesignData;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Converters
{
    public class FolderItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalTemplate { get; set; }
        public DataTemplate AddNewTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var folder_item = (FolderItemViewModel)item;
            return folder_item.IsAddNew ? AddNewTemplate : NormalTemplate;
        }
    };
}