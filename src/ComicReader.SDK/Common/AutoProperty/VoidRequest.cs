// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

internal class VoidRequest : IRequestKey
{
    public override string ToString()
    {
        return string.Empty;
    }

    public override bool Equals(object? obj)
    {
        return obj is VoidRequest;
    }

    public override int GetHashCode()
    {
        return 0;
    }
}
