// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IKVProperty<K, V> : IProperty where K : IRequestKey
{
    LockResource GetLockResource(K key, LockType type);
}
