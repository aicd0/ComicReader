using System;
using System.ComponentModel;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData
{
    public class ReaderFrameViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private BitmapImage m_ImageL = null;
        public BitmapImage ImageL
        {
            get => m_ImageL;
            set
            {
                m_ImageL = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageL"));
            }
        }

        private BitmapImage m_ImageR = null;
        public BitmapImage ImageR
        {
            get => m_ImageR;
            set
            {
                m_ImageR = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageR"));
            }
        }

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

        private Thickness m_FrameMargin = new Thickness(0.0, 0.0, 0.0, 0.0);
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
        public double Width => Container.ActualWidth;
        public double Height => Container.ActualHeight;
        public bool Processed { get; private set; } = false;
        public bool Ready { get; private set; } = false;

        private bool GetReady()
        {
            if (Container == null)
            {
                return false;
            }

            double desired_width = FrameWidth + FrameMargin.Left + FrameMargin.Right;
            double desired_height = FrameHeight + FrameMargin.Top + FrameMargin.Bottom;

            if (Math.Abs(Container.ActualWidth - desired_width) > 5.0)
            {
                return false;
            }

            if (Math.Abs(Container.ActualHeight - desired_height) > 5.0)
            {
                return false;
            }

            return true;
        }

        public void Notify(bool cancel = false)
        {
            lock (this)
            {
                if (Processed)
                {
                    Monitor.Pulse(this);
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
                        Monitor.Pulse(this);
                    }
                }
            }
        }

        public void Reset()
        {
            Processed = false;
            Ready = false;
        }

        public Grid Container = null;
    };
}
