// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Common.AutoProperty;

public class ExternalRequest<K, V> : IExternalRequest where K : IRequestKey
{
    private BatchInfo? _batch;

    public RequestType Type { get; }
    public IKVProperty<K, V> Property { get; }
    public K Key { get; }
    public V? Value { get; }
    public RequestOption Option { get; }
    private ExternalRequest<K, V> OriginalRequest { get; set; }

    RequestType IReadonlyExternalRequest.Type => Type;

    private ExternalRequest(RequestType type, IKVProperty<K, V> property, K key, V? value, RequestOption? option, ExternalRequest<K, V>? originalRequest)
    {
        Type = type;
        Property = property;
        Key = key;
        Value = value;
        Option = option ?? new(true);
        OriginalRequest = originalRequest ?? this;
    }

    private void OnResponse(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
    {
        DispatchResult(new ExternalResponse<V>(response.Result, response.Value));
    }

    private void DispatchResult(ExternalResponse<V> response)
    {
        if (_batch == null)
        {
            throw new InvalidOperationException("Request before activation.");
        }
        _batch.Response.SetResponse(OriginalRequest, response);
        _batch.CompleteOne();
        _batch = null;
    }

    IExternalRequest IExternalRequest.Clone()
    {
        return new ExternalRequest<K, V>(Type, Property, Key, Value, Option, OriginalRequest);
    }

    void IExternalRequest.Activate(BatchInfo batch)
    {
        _batch = batch;
    }

    void IExternalRequest.SetResult(OperationResult result, string message)
    {
        DispatchResult(new ExternalResponse<V>(result, message: message));
    }

    bool IExternalRequest.TryGetLockResource(IServerContext server, [MaybeNullWhen(false)] out LockResource resource)
    {
        return server.TryGetLockResource(Property, Key, Type, out resource);
    }

    void IExternalRequest.Request(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, LockToken token)
    {
        PropertyRequestContent<K, V> requestContent = new(Type, Key, Value, Option, token);
        OperationResult result = context.Request(Property, requestContent, OnResponse, out _);
        if (result != OperationResult.Successful)
        {
            DispatchResult(new ExternalResponse<V>(result));
        }
    }

    bool IReadonlyExternalRequest.IsNullValue()
    {
        return Value is null;
    }

    public class Builder(IKVProperty<K, V> property, K key)
    {
        private readonly IKVProperty<K, V> _property = property;
        private readonly K _key = key;
        private RequestType _type = RequestType.Read;
        private V? _value;
        private RequestOption? _option;

        public Builder SetRequestType(RequestType type)
        {
            _type = type;
            return this;
        }

        public Builder SetValue(V? value)
        {
            _value = value;
            return this;
        }

        public Builder SetOption(RequestOption? option)
        {
            _option = option;
            return this;
        }

        public ExternalRequest<K, V> Build()
        {
            ArgumentNullException.ThrowIfNull(_property);
            return new ExternalRequest<K, V>(_type, _property, _key, _value, _option, null);
        }
    }
}
