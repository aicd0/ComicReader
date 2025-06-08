// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty.Utils;

public interface IPropertyOperator<K, V>
{
    Task<bool> Read(K key);

    Task<bool> Write(K key, V value, RequestOption? option = null);
}
