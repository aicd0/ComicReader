using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    internal sealed partial class ReaderFrame : UserControl
    {
        public ReaderFrameViewModel Ctx => DataContext as ReaderFrameViewModel;
        public ReaderFrameViewModel Item { get; private set; }

        public ReaderFrame()
        {
            InitializeComponent();
        }

        private void Notify()
        {
            if (Ctx == null)
            {
                return;
            }

            Ctx.ItemContainer = this;

            if (MainFrame == null)
            {
                return;
            }

            Ctx.Container = MainFrame;
            Ctx.Notify();
        }

        private void OnFrameDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Notify binding changes.
            Bindings.Update();

            Notify();
        }

        private void OnFrameLoaded(object sender, RoutedEventArgs e)
        {
            Notify();
        }

        private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Notify();
        }

        public void Bind(ReaderFrameViewModel item)
        {
            Item = item;
            CompareAndBind(item);
        }

        public void CompareAndBind(ReaderFrameViewModel item)
        {
            if (item != Item)
            {
                return;
            }
            if (Item == null)
            {
                ImageLeft.Source = null;
                ImageRight.Source = null;
            }
            ImageLeft.Source = Item.ImageL.Image;
            ImageRight.Source = Item.ImageR.Image;
        }
    }
}
