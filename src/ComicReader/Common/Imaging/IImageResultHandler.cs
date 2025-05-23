﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Common.Imaging;

internal interface IImageResultHandler
{
    public void OnSuccess(BitmapImage image);
}
