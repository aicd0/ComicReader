﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.SDK.Common.Algorithm;

namespace ComicReader.ViewModels;

internal partial class SimpleGroupViewModel<T> : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string GroupName { get; private set; }

    /// <summary>
    /// Items in the group.
    /// </summary>
    /// <remarks>
    /// Do not modify this collection directly. Use <see cref="UpdateItems"/> to set the items.
    /// </remarks>
    public List<T> Items { get; } = [];

    /// <summary>
    /// Items to display in the group.
    /// </summary>
    /// <remarks>
    /// Do not modify this collection directly. Use <see cref="UpdateItems"/> to set the items.
    /// </remarks>
    public ObservableCollection<T> DisplayItems { get; } = [];

    private bool _collaped = false;
    public bool Collapsed
    {
        get => _collaped;
        set
        {
            _collaped = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Collapsed)));

            if (value)
            {
                DisplayItems.Clear();
            }
            else
            {
                DisplayItems.Clear();
                foreach (T item in Items)
                {
                    DisplayItems.Add(item);
                }
            }
        }
    }

    public SimpleGroupViewModel(string groupName, IEnumerable<T> items, bool collapsed)
    {
        GroupName = groupName;

        Items.AddRange(items);
        _collaped = collapsed;
        if (!collapsed)
        {
            foreach (T item in items)
            {
                DisplayItems.Add(item);
            }
        }
    }

    public void UpdateItems(IReadOnlyList<T> items, Func<T, T, bool> comparer)
    {
        Items.Clear();
        Items.AddRange(items);

        if (!_collaped)
        {
            DiffUtils.UpdateCollection(DisplayItems, items, comparer);
        }
    }

    public void UpdateItems(Func<T, T?> replacement)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            T oldItem = Items[i];
            T? newItem = replacement(oldItem);
            if (newItem is not null)
            {
                Items[i] = newItem;
            }
        }
        for (int i = 0; i < DisplayItems.Count; i++)
        {
            T oldItem = DisplayItems[i];
            T? newItem = replacement(oldItem);
            if (newItem is not null)
            {
                DisplayItems[i] = newItem;
            }
        }
    }
}
