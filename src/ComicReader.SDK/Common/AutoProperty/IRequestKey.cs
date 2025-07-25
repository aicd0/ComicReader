﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IRequestKey
{
    string ToString();

    bool Equals(object? obj);

    int GetHashCode();
}
