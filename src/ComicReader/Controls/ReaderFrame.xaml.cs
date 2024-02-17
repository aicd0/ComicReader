using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            if (Container == null)
            {
                return;
            }
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

        public Grid Container => MainFrame;

        public void Bind(ReaderFrameViewModel item)
        {
            if (Item != null)
            {
                Item.ItemContainer = null;
            }
            Item = item;
            Item.ItemContainer = this;
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
