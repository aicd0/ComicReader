// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common.Imaging;
using ComicReader.Data.Models.Comic;

using Windows.Storage.Streams;

namespace ComicReader.Helpers.Imaging;

internal class ComicCoverImageSource(ComicData comic) : IImageSource
{
    private readonly ComicData _comic = comic;

    public async Task<IRandomAccessStream> GetImageStream()
    {
        using IComicConnection connection = await _comic.OpenComicAsync();
        return await connection.GetImageStream(0);
    }

    public string GetUri()
    {
        return _comic.GetCoverImageCacheKey();
    }

    public int GetContentSignature()
    {
        return 0;
    }
}
