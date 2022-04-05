using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    public sealed partial class ReadeFrame : UserControl
    {
        public ReaderFrameViewModel Ctx => DataContext as ReaderFrameViewModel;

        public ReadeFrame()
        {
            InitializeComponent();
        }

        private void Notify()
        {
            if (Ctx == null)
            {
                return;
            }

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
    }
}
