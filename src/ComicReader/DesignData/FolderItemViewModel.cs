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
    public class FolderItemViewModel
    {
        public string Folder { get; set; }
        public bool IsAddNew { get; set; }

        // events
        public PointerEventHandler OnItemPressed { get; set; }
        public RoutedEventHandler OnRemoveClicked { get; set; }

        // methods
        public static Func<FolderItemViewModel, FolderItemViewModel, bool> ContentEquals = delegate (FolderItemViewModel a, FolderItemViewModel b)
        {
            if (a.IsAddNew != b.IsAddNew)
            {
                return false;
            }

            if (a.IsAddNew)
            {
                return true;
            }

            return a.Folder == b.Folder;
        };
    }
}