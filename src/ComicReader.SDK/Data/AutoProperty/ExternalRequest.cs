// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalRequest<K, V> : IExternalRequest where K : IRequestKey
{
    private BatchInfo? _batch;
    private LockToken? _lockToken;

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
        if (_lockToken is not null)
        {
            _lockToken.Release();
            _lockToken = null;
        }

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

    void IExternalRequest.SetFailedResult(string reason)
    {
        DispatchResult(new ExternalResponse<V>(RequestResult.Failed, reason: reason));
    }

    bool IExternalRequest.TryRequest(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, LockManager lockManager)
    {
        LockResource lockResource = Type switch
        {
            RequestType.Read => Property.GetLockResource(Key, LockType.Read),
            RequestType.Modify => Property.GetLockResource(Key, LockType.Write),
            _ => Property.GetLockResource(Key, LockType.Write),
        };
        if (!lockManager.TryAcquireLock(lockResource, out LockToken? token))
        {
            return false;
        }
        _lockToken = token;

        PropertyRequestContent<K, V> requestContent = new(Type, Key, Value, Option, token);
        SealedPropertyRequest<K, V>? request = context.Request(Property, requestContent, OnResponse);
        if (request is null)
        {
            DispatchResult(new ExternalResponse<V>(RequestResult.Failed));
        }
        return true;
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
