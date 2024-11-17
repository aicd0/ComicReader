// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Windows.Storage.Streams;

namespace ComicReader.Common.SimpleImageView;

internal interface IImageSource
{
    IRandomAccessStream GetImageStream();

    string GetUniqueKey();
}
