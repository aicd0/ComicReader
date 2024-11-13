// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.DesignData;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

public sealed partial class ReaderPreviewImage : UserControl
{
    public ReaderImagePreviewViewModel Ctx => DataContext as ReaderImagePreviewViewModel;

    public ReaderPreviewImage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
