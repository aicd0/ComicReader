// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData;

public class ReaderImagePreviewViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private BitmapImage m_ImageSource = null;
    public BitmapImage ImageSource
    {
        get => m_ImageSource;
        set
        {
            m_ImageSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageSource"));
        }
    }

    public int Page { get; set; } = -1;
}
