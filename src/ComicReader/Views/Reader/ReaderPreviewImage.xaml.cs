// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.DesignData;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

internal sealed partial class ReaderPreviewImage : UserControl
{
    public ReaderImagePreviewViewModel Model { get; set; }

    public ReaderPreviewImage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    public void SetModel(ReaderImagePreviewViewModel model, bool inRecycleQueue)
    {
        if (inRecycleQueue)
        {
            ImageHolder.UnsetModel();
        }

        Model = model;

        if (!inRecycleQueue)
        {
            ImageHolder.SetModel(model.Image);
        }
    }
}
