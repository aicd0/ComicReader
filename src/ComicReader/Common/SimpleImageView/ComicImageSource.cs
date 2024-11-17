// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Database;

using Windows.Storage.Streams;

namespace ComicReader.Common.SimpleImageView;

internal class ComicImageSource : IImageSource
{
    private readonly ComicData _comic;
    private readonly int _index;

    public ComicImageSource(ComicData comic, int index)
    {
        _comic = comic;
        _index = index;
    }

    public async Task<IRandomAccessStream> GetImageStream()
    {
        return await _comic.GetImageStream(_index);
    }

    public string GetUniqueKey()
    {
        return _comic.Location + ":" + _index.ToString();
    }
}
