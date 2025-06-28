// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.BaseUI;

internal class NavigationBundle
{
    public IPageTrait PageTrait { get; }
    public PageBundle Bundle { get; }
    public string Url { get; }
    public PageCommunicator Communicator { get; } = new();

    public NavigationBundle(IPageTrait pageTrait, Dictionary<string, string> parameters, string url)
    {
        PageTrait = pageTrait;
        Bundle = new PageBundle(parameters);
        Url = url;
    }
}
