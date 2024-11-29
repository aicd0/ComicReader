// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BasePage;
using ComicReader.Helpers.Navigation;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Help;

internal sealed partial class HelpPage : BasePage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle(StringResourceProvider.GetResourceString("Help"));
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Help });
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }
}
