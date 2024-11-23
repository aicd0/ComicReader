// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common.SimpleImageView;

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

    private bool _imageLeftSet;
    public bool ImageLeftSet
    {
        get => _imageLeftSet;
        set
        {
            _imageLeftSet = value;
            if (!value)
            {
                ImageLeft = null;
            }
        }
    }

    private BitmapImage _imageLeft;
    public BitmapImage ImageLeft
    {
        get => _imageLeft;
        set
        {
            BitmapImage newValue;
            if (ImageLeftSet)
            {
                newValue = value;
            }
            else
            {
                newValue = null;
            }

            if (_imageLeft != newValue)
            {
                _imageLeft = newValue;
                if (newValue == null)
                {
                    ImageLeftSet = false;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageLeft)));
            }
        }
    }

    public IImageSource ImageSourceRight { get; set; }

    private bool _imageRightSet;
    public bool ImageRightSet
    {
        get => _imageRightSet;
        set
        {
            _imageRightSet = value;
            if (!value)
            {
                ImageRight = null;
            }
        }
    }

    private BitmapImage _imageRight;
    public BitmapImage ImageRight
    {
        get => _imageRight;
        set
        {
            BitmapImage newValue;
            if (ImageRightSet)
            {
                newValue = value;
            }
            else
            {
                newValue = null;
            }

            if (_imageRight != newValue)
            {
                _imageRight = newValue;
                if (newValue == null)
                {
                    ImageRightSet = false;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageRight)));
            }
        }
    }

    public int PageL { get; set; } = -1;
    public int PageR { get; set; } = -1;
    public double Page => PageL != -1 && PageR != -1 ? (PageL + PageR) * 0.5 : PageL == -1 ? PageR : PageL;
};
