// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text;

using ComicReader.Common;
using ComicReader.Common.AppEnvironment;
using ComicReader.Common.PageBase;
using ComicReader.Common.Threading;
using ComicReader.Data;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Data.SqlHelpers;
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
        UpdateStatistis();
        UpdateFeedback();
        UpdateAbout();
        UpdateDebugInformation();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ComicData.OnUpdated += OnComicDataUpdated;
    }

    protected override void OnPause()
    {
        base.OnPause();
        ComicData.OnUpdated -= OnComicDataUpdated;
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
            await C0.ShowDialogAsync(dialog, XamlRoot);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            await HistoryDataManager.Clear(true);
            ViewModel.IsClearHistoryEnabled = false;
        });
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
        ViewModel.SelectLanguage(((ComboBox)sender).SelectedIndex);
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
        ComicModel.UpdateAllComics("OnRescanFilesClicked", lazy: false);
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCache();
    }

    private void OnComicDataUpdated()
    {
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            ViewModel.UpdateRescanStatus();
            UpdateStatistis();
        });
    }

    //
    // UI
    //

    private void UpdateStatistis()
    {
        C0.Run(async delegate
        {
            long comicCount = 0;
            SelectCommand command = new(ComicTable.Instance);
            IReaderToken<long> comicCountToken = command.PutQueryCountAll();
            using SelectCommand.IReader reader = await command.ExecuteAsync(SqlDatabaseManager.MainDatabase);
            if (await reader.ReadAsync())
            {
                comicCount = comicCountToken.GetValue();
            }
            string total_comic_string = StringResourceProvider.Instance.TotalComics;
            StatisticsTextBlock.Text = total_comic_string +
                comicCount.ToString("#,#0", CultureInfo.InvariantCulture);
        });
    }

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
        AboutBuildVersionControl.Text = appName + " " + EnvironmentProvider.Instance.GetVersionName();

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
