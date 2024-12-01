// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Imaging;
using ComicReader.Data;

using Windows.Storage.Streams;

namespace ComicReader.Helpers.Imaging;

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
        if (!(await _comic.UpdateImages(reload: false)).Successful())
        {
            return null;
        }

        return await _comic.GetImageStream(_index);
    }

    public string GetCacheKey()
    {
        if (!_comic.UpdateImages(reload: false).Result.Successful())
        {
            return null;
        }

        return _comic.GetImageCacheKey(_index);
    }

    int IImageSource.GetContentSignature()
    {
        if (!_comic.UpdateImages(reload: false).Result.Successful())
        {
            return 0;
        }

        return _comic.GetImageSignature(_index);
    }
}
