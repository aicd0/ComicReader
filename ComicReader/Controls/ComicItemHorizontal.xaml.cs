using Windows.UI.Xaml.Controls;
using ComicReader.DesignData;

namespace ComicReader.Controls
{
    public sealed partial class ComicItemHorizontal : UserControl
    {
        public ComicItemViewModel Ctx => DataContext as ComicItemViewModel;

        public ComicItemHorizontal()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}