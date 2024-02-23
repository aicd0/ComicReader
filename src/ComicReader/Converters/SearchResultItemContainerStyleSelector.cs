using ComicReader.DesignData;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Converters
{
    public class SearchResultItemContainerStyleSelector : StyleSelector
    {
        public Style NormalStyle { get; set; }
        public Style ExpandedStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var model = item as ComicItemViewModel;
            return model.IsSelectMode ? ExpandedStyle : NormalStyle;
        }
    }
}
