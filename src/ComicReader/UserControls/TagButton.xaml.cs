// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.UserControls;

public sealed partial class TagButton : UserControl
{
    public TagViewModel ViewModel => DataContext as TagViewModel;

    public TagButton()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
