using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace ComicReader.Controls
{
    public sealed partial class PageDemoSingle : UserControl
    {
        public PageDemoSingle()
        {
            this.InitializeComponent();
        }

        public string Header
        {
            get
            {
                return (string)GetValue(HeaderProperty);
            }
            set
            {
                SetValue(HeaderProperty, value);
            }
        }
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(PageDemoSingle), new PropertyMetadata(null));

        private bool m_IsHighlight = false;
        public bool IsHighlight
        {
            get
            {
                return m_IsHighlight;
            }
            set
            {
                m_IsHighlight = value;
                if (RootGrid != null)
                {
                    if (value)
                    {
                        RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
                    }
                    else
                    {
                        RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
                    }
                }
            }
        }
    }
}
