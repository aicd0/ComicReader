// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;

using ComicReader.DesignData;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

internal class ReaderFrameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private readonly ReaderImageViewModel _imageL = new();
    public ReaderImageViewModel ImageL => _imageL;

    private readonly ReaderImageViewModel _imageR = new();
    public ReaderImageViewModel ImageR => _imageR;

    private double m_FrameWidth = 0.0;
    public double FrameWidth
    {
        get => m_FrameWidth;
        set
        {
            m_FrameWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FrameWidth"));
        }
    }

    private double m_FrameHeight = 0.0;
    public double FrameHeight
    {
        get => m_FrameHeight;
        set
        {
            m_FrameHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FrameHeight"));
        }
    }

    private Thickness m_FrameMargin = new(0.0, 0.0, 0.0, 0.0);
    public Thickness FrameMargin
    {
        get => m_FrameMargin;
        set
        {
            m_FrameMargin = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FrameMargin"));
        }
    }

    public int PageL { get; set; } = -1;
    public int PageR { get; set; } = -1;
    public double Page => PageL != -1 && PageR != -1 ? (PageL + PageR) * 0.5 : PageL == -1 ? PageR : PageL;
};

internal sealed partial class ReaderFrame : UserControl
{
    public delegate void ReadyStateChangeListener(FrameworkElement container, bool isReady);
    private event ReadyStateChangeListener ReadyStateChanged;

    private ReaderFrameViewModel ViewModel { get; } = new();
    private ReaderFrameUIModel UIModel { get; set; }
    private FrameworkElement Container => MainFrame;

    private bool? _isReady = null;

    public ReaderFrame()
    {
        InitializeComponent();
    }

    public void Bind(ReaderFrameUIModel model)
    {
        UIModel = model;

        if (model == null)
        {
            ImageLeft.Source = null;
            ImageRight.Source = null;
        }
        else
        {
            ViewModel.FrameHeight = model.FrameHeight;
            ViewModel.FrameWidth = model.FrameWidth;
            ViewModel.FrameMargin = model.FrameMargin;
            ImageLeft.Source = model.ImageL.Image;
            ImageRight.Source = model.ImageR.Image;
        }

        _isReady = null;
        DispatchReadyStateChangeEvent();
    }

    public void SetReadyStateChangeHandler(ReadyStateChangeListener handler)
    {
        ReadyStateChanged = handler;
    }

    private void OnFrameLoaded(object sender, RoutedEventArgs e)
    {
        DispatchReadyStateChangeEvent();
    }

    private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatchReadyStateChangeEvent();
    }

    private void DispatchReadyStateChangeEvent()
    {
        bool isReady = IsReady();
        if (isReady != _isReady)
        {
            _isReady = isReady;
            ReadyStateChanged?.Invoke(Container, isReady);
        }
    }

    private bool IsReady()
    {
        FrameworkElement container = Container;
        ReaderFrameUIModel model = UIModel;
        if (container == null || model == null)
        {
            return false;
        }

        double desired_width = model.FrameWidth + model.FrameMargin.Left + model.FrameMargin.Right;
        double desired_height = model.FrameHeight + model.FrameMargin.Top + model.FrameMargin.Bottom;

        if (Math.Abs(container.ActualWidth - desired_width) > 5.0)
        {
            return false;
        }

        if (Math.Abs(container.ActualHeight - desired_height) > 5.0)
        {
            return false;
        }

        return true;
    }
}
