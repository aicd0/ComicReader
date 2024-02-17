using ComicReader.Utils;
using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace ComicReader.DesignData
{
    public class TagViewModel
    {
        public string Tag { get; set; }

        // handlers
        private WeakReference<IItemHandler> _itemHandler;
        public IItemHandler ItemHandler
        {
            get
            {
                return _itemHandler?.Get() ?? EmptyItemHandler.Instance;
            }
            set
            {
                _itemHandler = new WeakReference<IItemHandler>(value);
            }
        }

        public interface IItemHandler
        {
            void OnClicked(object sender, RoutedEventArgs e);
        }

        private class EmptyItemHandler : IItemHandler
        {
            private EmptyItemHandler() { }

            private static IItemHandler _instance;
            public static IItemHandler Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new EmptyItemHandler();
                    }
                    return _instance;
                }
            }

            public void OnClicked(object sender, RoutedEventArgs e)
            {
            }
        }
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
