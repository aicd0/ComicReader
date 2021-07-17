using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed partial class ReadeFrameVertical : UserControl
    {
        public ReaderFrameModel Ctx => DataContext as ReaderFrameModel;

        public ReadeFrameVertical()
        {
            Utils.Methods.Run(async delegate
            {
                InitializeComponent();

                if (Ctx != null)
                {
                    await WriteContainer();
                }
            });
        }

        private async Task WriteContainer()
        {
            await Utils.Methods.WaitFor(() => ContainerGrid != null);
            Ctx.Container = ContainerGrid;
            Ctx.OnContainerSet?.Invoke(Ctx);
        }

        private void ContainerGrid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Utils.Methods.Run(async delegate
            {
                Bindings.Update();

                if (Ctx != null)
                {
                    await WriteContainer();
                }
            });
        }
    }
}