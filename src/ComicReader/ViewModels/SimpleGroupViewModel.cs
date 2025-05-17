// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ComicReader.ViewModels;

internal class SimpleGroupViewModel<T>
{
    public string GroupName { get; private set; }
    public ObservableCollection<T> Items { get; } = [];

    public SimpleGroupViewModel(string groupName, IEnumerable<T> items)
    {
        GroupName = groupName;

        foreach (T item in items)
        {
            Items.Add(item);
        }
    }
}
