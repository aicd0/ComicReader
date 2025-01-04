// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common.Imaging;
using ComicReader.Data.Comic;

using Windows.Storage.Streams;

namespace ComicReader.Helpers.Imaging;

internal class ComicImageSource : IImageSource
{
    private readonly ComicData _comic;
    private readonly IComicConnection _connection;
    private readonly int _index;

    public ComicImageSource(ComicData comic, IComicConnection connection, int index)
    {
        _comic = comic;
        _connection = connection;
        _index = index;
    }

    public async Task<IRandomAccessStream> GetImageStream()
    {
        return await _connection.GetImageStream(_index);
    }

    public string GetUri()
    {
        return _comic.GetImageCacheKey(_index);
    }

    int IImageSource.GetContentSignature()
    {
        return _comic.GetImageSignature(_index);
    }
}
