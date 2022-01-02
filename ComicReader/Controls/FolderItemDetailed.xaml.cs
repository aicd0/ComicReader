using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class FolderItemDetailed : UserControl
    {
        public FolderItemModel Ctx => DataContext as FolderItemModel;
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Glyph { get; set; }

        public FolderItemDetailed()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}
