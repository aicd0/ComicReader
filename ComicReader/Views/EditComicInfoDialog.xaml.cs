using System.Threading.Tasks;
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

        // done
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Utils.Methods.Run(async delegate
            {
                MainEditBox.TextDocument.GetText(Windows.UI.Text.TextGetOptions.None, out string text);
                await DataManager.IntepretComicInfoString(text, m_comic);
                Utils.BackgroundTasks.AppendTask(DataManager.SaveComicInfoFileSealed(m_comic), "Saving...");

                if (!m_comic.IsExternal)
                {
                    Utils.BackgroundTasks.AppendTask(DataManager.SaveDatabaseSealed(DatabaseItem.Comics), "Saving...");
                }
            });
        }

        // cancel
        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void MainEditBox_Loaded(object sender, RoutedEventArgs e)
        {
            Utils.Methods.Run(async delegate
            {
                string text = await DataManager.GenerateComicInfoString(m_comic);
                MainEditBox.TextDocument.SetText(Windows.UI.Text.TextSetOptions.None, text);
            });
        }
    }
}
