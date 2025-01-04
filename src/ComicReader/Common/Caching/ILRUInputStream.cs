// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace ComicReader.Common.Caching;

internal interface ILRUInputStream : IDisposable
{
    Task WriteAsync(IBuffer buffer);
}
