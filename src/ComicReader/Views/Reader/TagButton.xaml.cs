using ComicReader.DesignData;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

public sealed partial class TagButton : UserControl
{
    public TagViewModel Ctx => DataContext as TagViewModel;

    public TagButton()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
