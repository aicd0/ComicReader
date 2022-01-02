using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class TagButton : UserControl
    {
        public TagModel Ctx => DataContext as TagModel;

        public TagButton()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}
