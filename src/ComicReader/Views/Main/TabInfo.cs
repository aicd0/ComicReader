// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Router;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Main;
internal class TabInfo
{
    public TabInfo(int id, TabViewItem item)
    {
        Id = id;
        Item = item;
    }

    public TabViewItem Item { get; }
    public int Id { get; }
    public NavigationBundle CurrentBundle { get; set; }
}
