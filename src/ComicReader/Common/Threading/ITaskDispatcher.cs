// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Threading;

internal interface ITaskDispatcher
{
    void Submit(string taskName, Action actionn);
}
