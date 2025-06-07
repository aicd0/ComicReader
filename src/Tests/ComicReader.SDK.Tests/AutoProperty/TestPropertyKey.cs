// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestPropertyKey(string name) : IRequestKey
{
    public string Name { get; } = name;

    public override string ToString()
    {
        return Name;
    }

    public override bool Equals(object? obj)
    {
        return obj is TestPropertyKey key && key.Name == Name;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}
