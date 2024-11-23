// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.SimpleImageView;
using ComicReader.DesignData;

using Microsoft.UI.Xaml;

namespace ComicReader.Views.Reader;

internal class ReaderFrameUIModel
{
    public IImageSource ImageSource { get; set; }

    private readonly ReaderImageViewModel _imageL = new();
    public ReaderImageViewModel ImageL => _imageL;

    private readonly ReaderImageViewModel _imageR = new();
    public ReaderImageViewModel ImageR => _imageR;

    public double FrameWidth { get; set; } = 0.0;
    public double FrameHeight { get; set; } = 0.0;

    public Thickness FrameMargin { get; set; } = new(0.0, 0.0, 0.0, 0.0);

    public int PageL { get; set; } = -1;
    public int PageR { get; set; } = -1;
    public double Page => PageL != -1 && PageR != -1 ? (PageL + PageR) * 0.5 : PageL == -1 ? PageR : PageL;
};
