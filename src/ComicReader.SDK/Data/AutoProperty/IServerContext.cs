// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

internal interface IServerContext
{
    SealedPropertyRequest<Q>? HandleRequest<Q, R>(IProperty sender, IQRProperty<Q, R> receiver, PropertyRequestContent<Q> requestContent, Action<long, PropertyResponseContent<R>> handler);

    void HandleRespond<Q, R>(IQRProperty<Q, R> receiver, long requestId, PropertyResponseContent<R> responseContent);

    bool HandleRedirect<Q, R>(IQRProperty<Q, R> oldReceiver, long requestId, IQRProperty<Q, R> newReceiver);
}
