// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Common.AutoProperty;

public interface IKVPropertyContext<K, V> : IPropertyContext where K : IRequestKey
{
    internal void AddNewRequest(SealedPropertyRequest<K, V> request);

    internal bool TryGetLockResource(IKVProperty<K, V> property, K key, RequestType type, [MaybeNullWhen(false)] out LockResource resource);
}
