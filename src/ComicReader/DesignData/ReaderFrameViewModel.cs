using ComicReader.Utils;
using ComicReader.Views.Reader;
using Microsoft.UI.Xaml;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ComicReader.DesignData;

internal class ReaderFrameViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private readonly object _lock = new();

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
    public double Page => PageL != -1 && PageR != -1 ? (PageL + PageR) * 0.5 : (PageL == -1 ? PageR : PageL);
    public bool Processed { get; private set; } = false;
    public bool Ready { get; private set; } = false;

    private bool GetReady()
    {
        Microsoft.UI.Xaml.Controls.Grid container = ItemContainer?.Container;
        if (container == null)
        {
            return false;
        }

        double desired_width = FrameWidth + FrameMargin.Left + FrameMargin.Right;
        double desired_height = FrameHeight + FrameMargin.Top + FrameMargin.Bottom;

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

    public void Notify(bool cancel = false)
    {
        lock (_lock)
        {
            if (Processed)
            {
                Monitor.Pulse(_lock);
            }
            else
            {
                if (!Ready)
                {
                    Ready = GetReady();
                }

                if (Ready || cancel)
                {
                    Processed = true;
                    Monitor.Pulse(_lock);
                }
            }
        }
    }

    public async Task WaitForReady()
    {
        if (!Processed)
        {
            await Task.Run(delegate
            {
                lock (_lock)
                {
                    while (!Processed)
                    {
                        _ = Monitor.Wait(_lock);
                    }
                }
            });
        }
    }

    public void Reset()
    {
        Processed = false;
        Ready = false;
    }

    private WeakReference<ReaderFrame> _itemContainer = null;
    public ReaderFrame ItemContainer
    {
        get => _itemContainer?.Get();
        set
        {
            if (value == null)
            {
                _itemContainer = null;
            }
            else
            {
                _itemContainer = new WeakReference<ReaderFrame>(value);
            }
        }
    }
};
