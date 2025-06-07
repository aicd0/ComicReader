// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IKVEProperty<K, V, E> : IKVProperty<K, V>, IEProperty<E> where K : IRequestKey where E : IPropertyExtension
{
}
