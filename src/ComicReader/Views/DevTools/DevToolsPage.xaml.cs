// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReader.Common;
using ComicReader.Common.PageBase;
using ComicReader.Data.Models;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml.Controls;

using Windows.Storage;
using Windows.System;

namespace ComicReader.Views.DevTools;

internal sealed partial class DevToolsPage : BasePage
{
    private const string TAG = nameof(DevToolsPage);

    public DevToolsPage()
    {
        InitializeComponent();
    }

    //
    // Page Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle("Dev tools");
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Repair });
    }

    protected override void OnResume()
    {
        base.OnResume();
        SetResult(null);
        RestoreConfig();
    }

    //
    // Events
    //

    private void OnOpenAppFolderClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
    }

    private void CrashAppButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        throw new InvalidOperationException();
    }

    private void OnCommonConfigsApplyClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        C0.Run(async () =>
        {
            string configs = TbCommonConfigs.Text;
            try
            {
                await DebugSwitchModel.Instance.SaveConfig(configs);
            }
            catch (Exception ex)
            {
                SetResult(ex.ToString());
                return;
            }
            SetResult("Successfully applied");
            RestoreConfig();
        });
    }

    private void OnCommonConfigsRestoreClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RestoreConfig();
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private void SetResult(string result)
    {
        if (result == null || result.Length == 0)
        {
            result = "Operation result shows here";
        }

        TbOperationResult.Text = result;
    }

    private void RestoreConfig()
    {
        TbCommonConfigs.Text = DebugSwitchModel.Instance.SerializeToJson();
    }
}
