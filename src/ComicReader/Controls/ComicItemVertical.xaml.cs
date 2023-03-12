using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using ComicReader.DesignData;
using ComicReader.Utils;

namespace ComicReader.Controls
{
    internal sealed partial class ComicItemVertical : UserControl
    {
        public ComicItemViewModel Ctx => DataContext as ComicItemViewModel;
        public ComicItemViewModel Item { get; private set; }

        public ComicItemVertical()
        {
            InitializeComponent();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Bindings.Update();
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "PointerOver", true);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Normal", true);
        }

        public void Bind(ComicItemViewModel item)
        {
            Item = item;
            CompareAndBind(item);
        }

        public void CompareAndBind(ComicItemViewModel item)
        {
            if (item != Item)
            {
                return;
            }
            if (Item == null)
            {
                ImageHolder1.Source = null;
                ImageHolder2.Source = null;
            }
            ImageHolder1.Source = Item.Image.Image;
            ImageHolder2.Source = Item.Image.Image;
        }
    }
}
