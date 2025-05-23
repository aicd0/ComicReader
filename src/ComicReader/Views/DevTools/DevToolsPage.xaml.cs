// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.DebugTools;
using ComicReader.Common.PageBase;
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
        TbCommonConfigs.Text = DebugSwitches.Instance.SerializeToJson();
    }

    //
    // Events
    //

    private void OnOpenAppFolderClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
    }

    private void OnCommonConfigsApplyClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string configs = TbCommonConfigs.Text;
        try
        {
            DebugSwitches.Instance.ParseFromJson(configs);
        }
        catch (Exception ex)
        {
            SetResult(ex.ToString());
            return;
        }

        SetResult("Successfully applied");
    }

    private void OnCommonConfigsRestoreClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TbCommonConfigs.Text = DebugSwitches.Instance.SerializeToJson();
    }

    private void OnCommonConfigsTextChanged(object sender, TextChangedEventArgs e)
    {
        SetResult(null);
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
}
