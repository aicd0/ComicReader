// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Threading;

internal interface IDispatcher
{
    void Queue(Action action, string debugDescription);
}
