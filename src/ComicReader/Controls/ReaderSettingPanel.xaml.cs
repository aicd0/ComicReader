using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Controls
{
    public sealed partial class ReaderSettingPanel : UserControl
    {
        public DesignData.ReaderSettingViewModel Ctx => DataContext as DesignData.ReaderSettingViewModel;

        public ReaderSettingPanel()
        {
            this.InitializeComponent();
        }
    }
}
