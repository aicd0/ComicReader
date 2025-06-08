// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IProcessCallback
{
    void PostOnServerThread(bool completed, Action? action);
}
