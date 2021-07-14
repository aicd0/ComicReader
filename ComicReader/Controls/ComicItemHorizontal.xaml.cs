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
using ComicReader.Data;

namespace ComicReader.Controls
{
    public sealed partial class ComicItemHorizontal : UserControl
    {
        public SearchResultData Ctx { get => DataContext as SearchResultData; }

        public ComicItemHorizontal()
        {
            this.InitializeComponent();
            DataContextChanged += (s, e) => Bindings.Update();
        }
    }
}
