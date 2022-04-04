using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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
        public double Width => Container.ActualWidth;
        public double Height => Container.ActualHeight;
        public bool Ready { get; set; } = false;

        private bool EvalReady()
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

        public void NotifyReady()
        {
            lock (this)
            {
                Ready = EvalReady();

                if (Ready)
                {
                    Monitor.Pulse(this);
                }
            }
        }

        public Grid Container = null;
    };
}
