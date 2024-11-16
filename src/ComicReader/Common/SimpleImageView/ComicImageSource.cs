// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Database;

using Windows.Storage.Streams;

namespace ComicReader.Common.SimpleImageView;

internal class ComicImageSource : SimpleImageView.IImageSource
{
    private readonly ComicData _comic;
    private readonly int _index;

    public ComicImageSource(ComicData comic, int index)
    {
        _comic = comic;
        _index = index;
    }

    public IRandomAccessStream GetImageStream()
    {
        return _comic.GetImageStream(_index).Result;
    }
}
