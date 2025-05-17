// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;

using ComicReader.Common;

namespace ComicReader.ViewModels;

internal class SimpleGroupViewModel<T> where T : INotifyPropertyChanged
{
    public string GroupName { get; private set; }
    public ObservableCollectionPlus<T> Items { get; } = [];

    public SimpleGroupViewModel(string groupName, IEnumerable<T> items)
    {
        GroupName = groupName;

        foreach (T item in items)
        {
            Items.Add(item);
        }
    }
}
