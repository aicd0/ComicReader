using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Data;

namespace ComicReader.Views
{
    public sealed partial class EditComicInfoDialog : ContentDialog
    {
        private ComicItemData m_comic;

        public EditComicInfoDialog(ComicItemData comic)
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

                if (!m_comic.IsExternal)
                {
                    Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.SaveSealed(DatabaseItem.Comic), "Saving...");
                }
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
