﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common.Imaging;
using ComicReader.Data.Models.Comic;

using Windows.Storage.Streams;

namespace ComicReader.Helpers.Imaging;

internal class ComicCoverImageSource(ComicModel comic) : IImageSource
{
    private readonly ComicModel _comic = comic;

    public async Task<IRandomAccessStream?> GetImageStream()
    {
        using IComicConnection? connection = await _comic.OpenComicAsync();
        if (connection == null)
        {
            return null;
        }

        return await connection.GetImageStream(0);
    }

    public string GetUri()
    {
        return _comic.CoverImageCacheKey;
    }

    public int GetContentSignature()
    {
        return 0;
    }
}
