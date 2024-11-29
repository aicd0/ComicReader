// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Converters;

public class FavoritesItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate NormalTemplate { get; set; }
    public DataTemplate RenamingTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        var model = item as FavoriteItemViewModel;
        return model.IsRenaming ? RenamingTemplate : NormalTemplate;
    }
};