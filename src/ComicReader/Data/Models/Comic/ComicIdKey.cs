// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AutoProperty;

namespace ComicReader.Data.Models.Comic;

internal readonly struct ComicIdKey(long id) : IRequestKey
{
    public long Id { get; } = id;

    public override string ToString()
    {
        return Id.ToString();
    }

    public override bool Equals(object? obj)
    {
        return obj is ComicIdKey other && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
