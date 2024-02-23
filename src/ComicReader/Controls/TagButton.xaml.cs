using ComicReader.DesignData;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Controls
{
    public sealed partial class TagButton : UserControl
    {
        public TagViewModel Ctx => DataContext as TagViewModel;

        public TagButton()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}
