// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

internal class PropertyRequest<K, V> : IPropertyRequest where K : IRequestKey
{
    public long Id { get; }
    public RequestState State { get; set; } = RequestState.Requesting;
    public IProperty Sender { get; }
    public IKVProperty<K, V> Receiver { get; set; }
    public PropertyRequestContent<K, V> RequestContent { get; }
    public PropertyResponseContent<V>? ResponseContent { get; set; }
    public Action<long, PropertyResponseContent<V>> Handler { get; }

    IProperty IPropertyRequest.Sender => Sender;
    IProperty IPropertyRequest.Receiver => Receiver;

    public PropertyRequest(long id, IProperty sender, IKVProperty<K, V> receiver, PropertyRequestContent<K, V> request, Action<long, PropertyResponseContent<V>> handler)
    {
        Id = id;
        Sender = sender;
        Receiver = receiver;
        RequestContent = request;
        Handler = handler;
    }
}
