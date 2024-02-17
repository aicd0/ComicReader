using Microsoft.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    public sealed partial class ReaderPreviewImage : UserControl
    {
        public ReaderImagePreviewViewModel Ctx => DataContext as ReaderImagePreviewViewModel;

        public ReaderPreviewImage()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}
