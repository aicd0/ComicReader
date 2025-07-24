// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Settings;

internal sealed partial class SettingPage : BasePage
{
    private SettingPageViewModel ViewModel { get; } = new();

    public SettingPage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.Settings);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Setting });

        ViewModel.Initialize();
        UpdateFeedback();
        UpdateAbout();
        UpdateDebugInformation();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ViewModel.OnPageResume();
    }

    protected override void OnPause()
    {
        base.OnPause();
        ViewModel.OnPagePause();
    }

    //
    // Events
    //

    private void OnDebugModeToggled(object sender, RoutedEventArgs e)
    {
        bool debugMode = TsDebugMode.IsOn;
        if (ViewModel.DebugMode == debugMode)
        {
            return;
        }

        C0.Run(async delegate
        {
            if (debugMode)
            {
                var dialog = new ContentDialog
                {
                    Title = StringResourceProvider.Instance.Warning,
                    Content = StringResourceProvider.Instance.DebugModeWarning,
                    PrimaryButtonText = StringResourceProvider.Instance.Proceed,
                    CloseButtonText = StringResourceProvider.Instance.Cancel,
                    XamlRoot = XamlRoot
                };
                ContentDialogResult result = await dialog.ShowAsync();

                if (result == ContentDialogResult.None)
                {
                    ViewModel.DebugMode = false;
                    return;
                }
            }

            ViewModel.DebugMode = debugMode;
            DebugSwitchModel.DebugMode = debugMode;
        });
    }

    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var dialog = new ChooseLocationsDialog(WindowId);
            await dialog.ShowAsync(XamlRoot);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        HistoryModel.Instance.Clear(true);
        ViewModel.IsClearHistoryEnabled = false;
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            var uri = new Uri(@"https://github.com/aicd0/ComicReader/issues/new/choose");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        });
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetAppLanguage(((ComboBox)sender).SelectedIndex);
    }

    private void ShowHiddenComicButton_Click(object sender, RoutedEventArgs e)
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, "<hidden>");
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void RemoveUnreachableCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var checkbox = (CheckBox)sender;
        bool isChecked = checkbox.IsChecked ?? false;
        ViewModel.SetRemoveUnreachableComics(isChecked);
    }

    private void OnRescanFilesClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsRescanning = true;
        ComicModel.UpdateAllComics("OnRescanFilesClicked");
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCache();
    }

    //
    // UI
    //

    private void UpdateFeedback()
    {
        string appName = StringResourceProvider.Instance.AppDisplayName;
        string contributionBeforeLink = StringResourceProvider.Instance.ContributionRunBeforeLink;
        contributionBeforeLink = contributionBeforeLink.Replace("$appname", appName);
        ContributionRunBeforeLink.Text = contributionBeforeLink;
        ContributionRunAfterLink.Text = StringResourceProvider.Instance.ContributionRunAfterLink;
    }

    private void UpdateAbout()
    {
        string appName = StringResourceProvider.Instance.AppDisplayName;
        AboutBuildVersionControl.Text = appName + " " + EnvironmentProvider.GetVersionName();

        string author = "aicd0";
        string aboutCopyright = StringResourceProvider.Instance.AboutCopyright;
        aboutCopyright = aboutCopyright.Replace("$author", author);
        AboutCopyrightControl.Text = aboutCopyright;
    }

    private void UpdateDebugInformation()
    {
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        TbDebugInformation.Text = sb.ToString();
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }
}
