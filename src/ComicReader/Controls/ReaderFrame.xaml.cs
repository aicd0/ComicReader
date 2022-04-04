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

        private void NotifyReady()
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
            Ctx.NotifyReady();
        }

        private void OnFrameDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            // Notify binding changes.
            Bindings.Update();

            NotifyReady();
        }

        private void OnFrameLoaded(object sender, RoutedEventArgs e)
        {
            NotifyReady();
        }

        private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            NotifyReady();
        }
    }
}
