using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.DesignData
{
    public class ReaderFrameViewModel : INotifyPropertyChanged
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsImageVisible"));
            }
        }

        public bool IsImageVisible => ImageSource != null;

        private double m_ImageWidth = 0.0;
        public double ImageWidth
        {
            get => m_ImageWidth;
            set
            {
                m_ImageWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageWidth"));
            }
        }

        private double m_ImageHeight = 0.0;
        public double ImageHeight
        {
            get => m_ImageHeight;
            set
            {
                m_ImageHeight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ImageHeight"));
            }
        }

        public int Page { get; set; } = -1;
        public bool TopPadding { get; set; } = false;
        public bool BottomPadding { get; set; } = false;
        public bool LeftPadding { get; set; } = false;
        public bool RightPadding { get; set; } = false;

        private Thickness? m_Margin = null;
        public Thickness Margin
        {
            get
            {
                if (m_Margin == null)
                {
                    m_Margin = new Thickness(
                        LeftPadding ? 200 : 0,
                        TopPadding ? 10 : 0,
                        RightPadding ? 200 : 0,
                        BottomPadding ? 10 : 0);
                }

                return m_Margin.Value;
            }
        }

        public double Width => Container.ActualWidth;
        public double Height => Container.ActualHeight;
        public Grid Container = null;
        public Func<ReaderFrameViewModel, Task> OnContainerLoadedAsync;
    };
}
