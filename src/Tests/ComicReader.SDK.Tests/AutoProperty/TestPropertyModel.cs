// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestPropertyModel<V>
{
    public Queue<SealedPropertyRequest<TestPropertyKey, V>> requests = [];
}
