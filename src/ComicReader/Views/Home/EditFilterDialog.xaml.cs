// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Home;

internal sealed partial class EditFilterDialog : ContentDialog
{
    public EditFilterDialogViewModel ViewModel = new();

    public EditFilterDialog(ComicFilterModel.ExternalFilterModel filter)
    {
        InitializeComponent();
        ViewModel.Initialize(filter);
    }

    private void ContentDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ObserveData();
    }

    private void ObserveData()
    {
        ViewModel.NameLiveData.ObserveSticky(this, delegate (string text)
        {
            NameTextBox.Text = text ?? "";
        });
        ViewModel.ExpressionLiveData.ObserveSticky(this, delegate (string text)
        {
            ExpressionTextBox.Text = text ?? "";
        });
        ViewModel.ParseResultLiveData.ObserveSticky(this, delegate (string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                ParseResultTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                ParseResultTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                ParseResultTextBlock.Text = text ?? "";
            }
        });
        ViewModel.SaveEnableLiveData.ObserveSticky(this, delegate (bool enabled)
        {
            SaveButton.IsEnabled = enabled;
        });
        ViewModel.SaveAsNewEnableLiveData.ObserveSticky(this, delegate (bool enabled)
        {
            SaveAsNewButton.IsEnabled = enabled;
        });
    }

    //
    // Events
    //

    private void CancelButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Hide();
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Delete();
        Hide();
    }

    private void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Save();
        Hide();
    }

    private void SaveAsNewButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.SaveAsNew();
        Hide();
    }

    private void ExpressionTipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("expression_reference.txt", "12345\n67890");
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateName(NameTextBox.Text ?? "");
    }

    private void ExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateExpression(ExpressionTextBox.Text ?? "");
    }
}
