// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common.Imaging;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Reader;

internal class ReaderFrameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private double _frameWidth = 0.0;
    public double FrameWidth
    {
        get => _frameWidth;
        set
        {
            _frameWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameWidth)));
        }
    }

    private double _frameHeight = 0.0;
    public double FrameHeight
    {
        get => _frameHeight;
        set
        {
            _frameHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameHeight)));
        }
    }

    private Thickness _frameMargin = new(0.0, 0.0, 0.0, 0.0);
    public Thickness FrameMargin
    {
        get => _frameMargin;
        set
        {
            _frameMargin = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameMargin)));
        }
    }

    public IImageSource ImageSourceLeft { get; set; }

    private BitmapImage _imageLeft;
    public BitmapImage ImageLeft
    {
        get => _imageLeft;
        set
        {
            if (_imageLeft != value)
            {
                _imageLeft = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageLeft)));
            }
        }
    }

    public IImageSource ImageSourceRight { get; set; }

    private BitmapImage _imageRight;
    public BitmapImage ImageRight
    {
        get => _imageRight;
        set
        {
            if (_imageRight != value)
            {
                _imageRight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageRight)));
            }
        }
    }

    public int PageL { get; set; } = -1;
    public int PageR { get; set; } = -1;
    public double Page => PageL != -1 && PageR != -1 ? (PageL + PageR) * 0.5 : PageL == -1 ? PageR : PageL;

    public void RebindEntireViewModel()
    {
        PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReaderFrameViewModel)));
    }
};
