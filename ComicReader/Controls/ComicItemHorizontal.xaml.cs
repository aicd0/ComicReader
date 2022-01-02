using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class ComicItemHorizontal : UserControl
    {
        public ComicItemModel Ctx => DataContext as ComicItemModel;

        public ComicItemHorizontal()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}