using ComicReader.Database;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace ComicReader.Views
{
    public class EditComicInfoDialogShared : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool m_IsTagInfoBarOpen = false;
        public bool IsTagInfoBarOpen
        {
            get => m_IsTagInfoBarOpen;
            set
            {
                m_IsTagInfoBarOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTagInfoBarOpen"));
            }
        }
    }

    public sealed partial class EditComicInfoDialog : ContentDialog
    {
        public EditComicInfoDialogShared Shared = new EditComicInfoDialogShared();

        private readonly ComicData m_comic;

        public EditComicInfoDialog(ComicData comic)
        {
            m_comic = comic;

            InitializeComponent();
        }

        // events
        private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Done
            Utils.C0.Run(delegate
            {
                string text = "";
                text += "Title1: " + Title1TextBox.Text + "\n";
                text += "Title2: " + Title2TextBox.Text + "\n";
                text += TagTextBox.Text;

                m_comic.ParseInfo(text);
                m_comic.SaveBasic();

                Utils.TaskQueue.DefaultQueue.Enqueue(m_comic.SaveToInfoFileSealed());
            });
        }

        private void ContentDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Cancel
        }

        private void MainEditBoxLoaded(object sender, RoutedEventArgs e)
        {
            Title1TextBox.Text = m_comic.Title1;
            Title2TextBox.Text = m_comic.Title2;
            TagTextBox.Text = m_comic.TagString();
        }

        private void OnShowTagInfoButtonClicked(object sender, RoutedEventArgs e)
        {
            Shared.IsTagInfoBarOpen = !Shared.IsTagInfoBarOpen;
        }

        private void OnTagInfoBarCloseButtonClicked(Microsoft.UI.Xaml.Controls.InfoBar sender, object args)
        {
            Shared.IsTagInfoBarOpen = false;
        }
    }
}
