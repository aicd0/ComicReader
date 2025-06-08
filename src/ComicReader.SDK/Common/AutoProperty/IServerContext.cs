// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Common.AutoProperty;

internal interface IServerContext
{
    OperationResult HandleRequest<K, V>(IProperty sender, IKVProperty<K, V> receiver, PropertyRequestContent<K, V> requestContent, Action<long, PropertyResponseContent<V>> handler, out long requestId) where K : IRequestKey;

    OperationResult HandleRespond<K, V>(IKVProperty<K, V> receiver, long requestId, PropertyResponseContent<V> responseContent) where K : IRequestKey;

    OperationResult HandleRedirect<K, V>(IKVProperty<K, V> oldReceiver, long requestId, IKVProperty<K, V> newReceiver) where K : IRequestKey;

    bool TryGetLockResource<K, V>(IKVProperty<K, V> property, K key, RequestType type, [MaybeNullWhen(false)] out LockResource resource) where K : IRequestKey;
}
