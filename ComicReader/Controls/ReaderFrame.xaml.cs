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

            ReaderFrameModel new_ctx = args.NewValue as ReaderFrameModel;

            if (new_ctx != null)
            {
                new_ctx.Container = sender as Grid;
                new_ctx.OnContainerLoadedAsync?.Invoke(Ctx);
            }
        }
    }
}
