﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Home;

public sealed partial class FolderItemDetailed : UserControl
{
    public FolderItemViewModel Ctx => DataContext as FolderItemViewModel;
    public string Title { get; set; }
    public string Detail { get; set; }
    public string Glyph { get; set; }

    public FolderItemDetailed()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
