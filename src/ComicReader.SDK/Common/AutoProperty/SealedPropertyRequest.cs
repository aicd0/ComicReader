// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public class SealedPropertyRequest<K, V> where K : IRequestKey
{
    public long Id { get; }
    public PropertyRequestContent<K, V> RequestContent { get; }

    internal SealedPropertyRequest(long id, PropertyRequestContent<K, V> requestContent)
    {
        Id = id;
        RequestContent = requestContent;
    }
}
