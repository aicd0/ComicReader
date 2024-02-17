using Microsoft.UI.Xaml.Controls;
using ComicReader.DesignData;

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
