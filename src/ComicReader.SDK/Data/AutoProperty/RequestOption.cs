// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class RequestOption(bool persistent)
{
    public bool Persistent { get; } = persistent;
}
