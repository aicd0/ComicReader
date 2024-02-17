using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Converters
{
    public class SearchResultItemContainerStyleSelector : StyleSelector
    {
        public Style NormalStyle { get; set; }
        public Style ExpandedStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            ComicItemViewModel model = item as ComicItemViewModel;
            return model.IsSelectMode ? ExpandedStyle : NormalStyle;
        }
    }
}
