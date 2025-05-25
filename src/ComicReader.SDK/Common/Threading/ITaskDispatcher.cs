﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Threading;

public interface ITaskDispatcher
{
    void Submit(string taskName, Action action);
}
