// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.BaseUI;

public delegate void PageStopEventHandler();

internal interface ICommonPageAbility : IPageAbility
{
    void RegisterPageStopHandler(PageStopEventHandler handler);

    void UnregisterPageStopHandler(PageStopEventHandler handler);
}
