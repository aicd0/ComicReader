using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class ComicItemVertical : UserControl
    {
        public ComicItemModel Ctx => DataContext as ComicItemModel;

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
    }
}