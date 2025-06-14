// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.UI.Xaml;

namespace ComicReader.ViewModels;

public class TagViewModel
{
    public string Tag { get; set; } = string.Empty;

    private IItemHandler? _itemHandler;
    public IItemHandler ItemHandler
    {
        get
        {
            return _itemHandler ?? EmptyItemHandler.Instance;
        }
        set
        {
            _itemHandler = value;
        }
    }

    public interface IItemHandler
    {
        void OnClicked(object sender, RoutedEventArgs e);
    }

    private class EmptyItemHandler : IItemHandler
    {
        private EmptyItemHandler() { }

        public static EmptyItemHandler Instance = new();

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
