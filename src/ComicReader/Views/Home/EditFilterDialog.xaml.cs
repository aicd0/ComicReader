// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Home;

internal sealed partial class EditFilterDialog : ContentDialog
{
    public EditFilterDialogViewModel ViewModel = new();

    public EditFilterDialog()
    {
        InitializeComponent();
    }

    //
    // Events
    //

    private void CancelButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Hide();
    }
}
