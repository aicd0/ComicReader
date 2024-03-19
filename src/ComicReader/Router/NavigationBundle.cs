using ComicReader.Views.Base;
using System;
using System.Collections.Generic;

namespace ComicReader.Router;

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
