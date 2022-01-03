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

        private void OnFrameDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Notify binding changes.
            Bindings.Update();

            m_loaded = false;
        }

        private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Ctx == null) return;
            if (m_loaded) return;

            m_loaded = true;
            Ctx.Container = sender as Grid;
            Ctx.OnContainerLoadedAsync?.Invoke(Ctx);
        }
    }
}
