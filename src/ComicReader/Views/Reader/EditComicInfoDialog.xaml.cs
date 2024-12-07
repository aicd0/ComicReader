// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common.Threading;
using ComicReader.Data;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

internal class EditComicInfoDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private bool _isTagInfoBarOpen = false;
    public bool IsTagInfoBarOpen
    {
        get => _isTagInfoBarOpen;
        set
        {
            _isTagInfoBarOpen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTagInfoBarOpen)));
        }
    }
}

internal sealed partial class EditComicInfoDialog : ContentDialog
{
    public EditComicInfoDialogViewModel ViewModel = new();

    private readonly ComicData _comic;

    public EditComicInfoDialog(ComicData comic)
    {
        _comic = comic;
        InitializeComponent();
    }

    //
    // Events
    //

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Title1TextBox.Text = _comic.Title1;
        Title2TextBox.Text = _comic.Title2;
        DescriptionTextBox.Text = _comic.Description;
        TagTextBox.Text = _comic.TagString();
    }

    private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        string infoText = ComicData.InfoString(Title1TextBox.Text, Title2TextBox.Text, DescriptionTextBox.Text, TagTextBox.Text);
        _comic.ParseInfo(infoText);
        _comic.SaveBasic();

        TaskDispatcher.DefaultQueue.Submit("ContentDialogPrimaryButtonClick", _comic.SaveToInfoFileSealed());
    }

    private void ContentDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
    }

    private void OnShowTagInfoButtonClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsTagInfoBarOpen = !ViewModel.IsTagInfoBarOpen;
    }

    private void OnTagInfoBarCloseButtonClicked(Microsoft.UI.Xaml.Controls.InfoBar sender, object args)
    {
        ViewModel.IsTagInfoBarOpen = false;
    }
}
