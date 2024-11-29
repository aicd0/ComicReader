// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common.BasePage;

namespace ComicReader.Helpers.Navigation;

internal class NavigationBundle
{
    public IPageTrait PageTrait { get; }
    public PageBundle Bundle { get; }
    public string Url { get; }
    public Dictionary<Type, IPageAbility> Abilities { get; } = new();

    public NavigationBundle(IPageTrait pageTrait, Dictionary<string, string> parameters, string url)
    {
        PageTrait = pageTrait;
        Bundle = new PageBundle(parameters);
        Url = url;
    }
}
