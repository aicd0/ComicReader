using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    public sealed partial class ReadeFrame : UserControl
    {
        public ReaderFrameViewModel Ctx => DataContext as ReaderFrameViewModel;

        private bool m_loaded = false;

        public ReadeFrame()
        {
            InitializeComponent();
        }

        private void TryLoad(Grid container)
        {
            if (m_loaded) return;
            if (Ctx == null) return;
            if (!Ctx.IsReady) return;
            if (container == null) return;
            if (container.ActualWidth < 0.1 || container.ActualHeight < 0.1) return;

            m_loaded = true;
            Ctx.Container = container;
            Ctx.OnContainerLoadedAsync?.Invoke(Ctx);
        }

        private void OnFrameDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Notify binding changes.
            Bindings.Update();

            m_loaded = false;
            TryLoad(sender as Grid);
        }

        private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            TryLoad(sender as Grid);
        }
    }
}
