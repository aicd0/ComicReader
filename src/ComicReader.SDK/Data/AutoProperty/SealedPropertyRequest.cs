// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class SealedPropertyRequest<K, V>(long id, PropertyRequestContent<K, V> requestContent) where K : IRequestKey
{
    public long Id { get; } = id;
    public PropertyRequestContent<K, V> RequestContent { get; } = requestContent;
}
