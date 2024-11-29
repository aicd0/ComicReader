// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Data;

using Windows.Storage.Streams;

namespace ComicReader.Common.Imaging;

internal class ComicCoverImageSource(ComicData comic) : IImageSource
{
    private readonly ComicData _comic = comic;

    public async Task<IRandomAccessStream> GetImageStream()
    {
        TaskException result = await _comic.UpdateImages(reload: false);
        if (!result.Successful())
        {
            return null;
        }
        return await _comic.GetImageStream(0);
    }

    public string GetUniqueKey()
    {
        string coverKey = _comic.CoverFileCache;
        if (coverKey != null && coverKey.Length > 0)
        {
            return coverKey;
        }

        if (!_comic.UpdateImages(reload: false).Result.Successful())
        {
            return null;
        }
        coverKey = _comic.GetImageCacheKey(0);
        _comic.SetCoverFileCacheKey(coverKey);
        return coverKey;
    }
}
