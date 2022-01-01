using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Views
{
    public sealed partial class EditComicInfoDialog : ContentDialog
    {
        private ComicData m_comic;

        public EditComicInfoDialog(ComicData comic)
        {
            m_comic = comic;

            InitializeComponent();
        }

        // events
        private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // done
            Utils.Methods.Run(async delegate
            {
                string text = MainEditBox.Text;
                await ComicDataManager.ParseInfo(text, m_comic);
                Utils.TaskQueue.TaskQueueManager.AppendTask(
                    ComicDataManager.SaveInfoFileSealed(m_comic), "Saving...");
                await m_comic.SaveBasic();
            });
        }

        private void ContentDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // cancel
        }

        private void MainEditBoxLoaded(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                MainEditBox.Text = await ComicDataManager.InfoString(m_comic);
            });
        }
    }
}
