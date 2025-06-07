﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.ObjectModel;

namespace ComicReader.ViewModels;

public class HistoryItemViewModel
{
    public long Id { get; set; }
    public string Time { get; set; }
    public string Title { get; set; }
}

public class HistoryGroupViewModel : ObservableCollection<HistoryItemViewModel>
{
    public HistoryGroupViewModel(string key) : base()
    {
        Key = key;
    }

    public string Key { get; set; }
}
