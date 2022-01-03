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
    public class TagViewModel
    {
        public string Tag { get; set; }
        public RoutedEventHandler OnClicked { get; set; }
    };

    public class TagCollectionViewModel
    {
        public TagCollectionViewModel(string name)
        {
            Name = name;
            Tags = new List<TagViewModel>();
        }

        public string Name { get; set; }
        public List<TagViewModel> Tags { get; set; }
    };
}
