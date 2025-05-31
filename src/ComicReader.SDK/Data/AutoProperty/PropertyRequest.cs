// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

internal class PropertyRequest<Q, R> : IPropertyRequest
{
    public long Id { get; }
    public RequestState State { get; set; } = RequestState.Requesting;
    public IProperty Sender { get; }
    public IQRProperty<Q, R> Receiver { get; set; }
    public PropertyRequestContent<Q> RequestContent { get; }
    public PropertyResponseContent<R>? ResponseContent { get; set; }
    public Action<long, PropertyResponseContent<R>> Handler { get; }

    IProperty IPropertyRequest.Sender => Sender;
    IProperty IPropertyRequest.Receiver => Receiver;

    public PropertyRequest(long id, IProperty sender, IQRProperty<Q, R> receiver, PropertyRequestContent<Q> request, Action<long, PropertyResponseContent<R>> handler)
    {
        Id = id;
        Sender = sender;
        Receiver = receiver;
        RequestContent = request;
        Handler = handler;
    }
}
