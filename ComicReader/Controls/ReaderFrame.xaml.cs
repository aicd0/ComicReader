using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class ReadeFrame : UserControl
    {
        public ReaderFrameModel Ctx => DataContext as ReaderFrameModel;

        public ReadeFrame()
        {
            InitializeComponent();
        }

        private void OnFrameDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Notify binding changes.
            Bindings.Update();
        }

        private void OnFrameLoaded(object sender, RoutedEventArgs e)
        {
            Ctx.Container = sender as Grid;
            Ctx.OnContainerLoaded?.Invoke(Ctx);
        }
    }
}
