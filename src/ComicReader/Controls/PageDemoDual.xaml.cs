using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace ComicReader.Controls
{
    public sealed partial class PageDemoDual : UserControl
    {
        public PageDemoDual()
        {
            this.InitializeComponent();
        }

        public string HeaderL
        {
            get
            {
                return (string)GetValue(HeaderLProperty);
            }
            set
            {
                SetValue(HeaderLProperty, value);
            }
        }
        public static readonly DependencyProperty HeaderLProperty =
            DependencyProperty.Register(nameof(HeaderL), typeof(string), typeof(PageDemoDual), new PropertyMetadata(null));

        public string HeaderR
        {
            get
            {
                return (string)GetValue(HeaderRProperty);
            }
            set
            {
                SetValue(HeaderRProperty, value);
            }
        }
        public static readonly DependencyProperty HeaderRProperty =
            DependencyProperty.Register(nameof(HeaderR), typeof(string), typeof(PageDemoDual), new PropertyMetadata(null));
    }
}
