// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

internal interface IServerContext
{
    SealedPropertyRequest<K, V>? HandleRequest<K, V>(IProperty sender, IKVProperty<K, V> receiver, PropertyRequestContent<K, V> requestContent, Action<long, PropertyResponseContent<V>> handler) where K : IRequestKey;

    void HandleRespond<K, V>(IKVProperty<K, V> receiver, long requestId, PropertyResponseContent<V> responseContent) where K : IRequestKey;

    bool HandleRedirect<K, V>(IKVProperty<K, V> oldReceiver, long requestId, IKVProperty<K, V> newReceiver) where K : IRequestKey;
}
