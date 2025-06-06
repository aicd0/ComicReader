// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IKVPropertyContext<K, V> : IPropertyContext where K : IRequestKey
{
    internal void AddNewRequest(SealedPropertyRequest<K, V> request);
}
