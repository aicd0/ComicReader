// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.PageBase;

internal interface ICommonPageAbility : IPageAbility
{
    public delegate void PageStoppedEventHandler();

    void RegisterPageStoppedHandler(Page owner, PageStoppedEventHandler handler);
}
