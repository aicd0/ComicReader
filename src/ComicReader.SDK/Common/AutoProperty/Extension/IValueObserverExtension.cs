// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty.Extension;

public interface IValueObserverExtension<K, V> : IPropertyExtension where K : IRequestKey
{
    void UpdateValue(K key, V? value);
}
