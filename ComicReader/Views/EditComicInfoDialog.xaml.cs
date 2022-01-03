using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ComicReader.Database;

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
            Utils.C0.Run(async delegate
            {
                LockContext db = new LockContext();

                string text = MainEditBox.Text;
                ComicDataManager.ParseInfo(text, m_comic);
                Utils.TaskQueueManager.AppendTask(
                    ComicDataManager.SaveInfoFileSealed(m_comic), "Saving...");
                await m_comic.SaveBasic(db);
            });
        }

        private void ContentDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // cancel
        }

        private void MainEditBoxLoaded(object sender, RoutedEventArgs e)
        {
            MainEditBox.Text = ComicDataManager.InfoString(m_comic);
        }
    }
}
