// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalRequest<Q, R> : IExternalRequest
{
    private BatchInfo? _batch;
    private Action? _callback;

    public RequestType Type { get; }
    public IQRProperty<Q, R> Property { get; }
    public string Key { get; }
    public Q? Value { get; }
    public RequestOption Option { get; }
    private ExternalRequest<Q, R> OriginalRequest { get; set; }

    RequestType IReadonlyExternalRequest.Type => Type;

    string IReadonlyExternalRequest.Key => Key;

    private ExternalRequest(RequestType type, IQRProperty<Q, R> property, string key, Q? value, RequestOption? option, ExternalRequest<Q, R>? originalRequest)
    {
        Type = type;
        Property = property;
        Key = key;
        Value = value;
        Option = option ?? new(true);
        OriginalRequest = originalRequest ?? this;
    }

    private void OnResponse(long id, PropertyResponseContent<R> response)
    {
        DispatchResult(new ExternalResponse<R>(response.Result, response.Value));
    }

    private void DispatchResult(ExternalResponse<R> response)
    {
        _batch!.Response.SetResponse(OriginalRequest, response);
        _callback?.Invoke();
        _batch.CompleteOne();
        _batch = null;
        _callback = null;
    }

    IExternalRequest IExternalRequest.Clone()
    {
        return new ExternalRequest<Q, R>(Type, Property, Key, Value, Option, OriginalRequest);
    }

    void IExternalRequest.Activate(BatchInfo batch)
    {
        _batch = batch;
    }

    void IExternalRequest.SetFailedResult(string reason)
    {
        DispatchResult(new ExternalResponse<R>(RequestResult.Failed, reason: reason));
    }

    void IExternalRequest.Request(IServerContext context, IProperty sender, Action callback)
    {
        _callback = callback;
        PropertyRequestContent<Q> requestContent = new(Type, Key, Value, Option);
        SealedPropertyRequest<Q>? request = context.HandleRequest(sender, Property, requestContent, OnResponse);
        if (request is null)
        {
            DispatchResult(new ExternalResponse<R>(RequestResult.Failed));
        }
    }

    bool IReadonlyExternalRequest.IsNullValue()
    {
        return Value is null;
    }

    public class Builder(IQRProperty<Q, R> property)
    {
        private readonly IQRProperty<Q, R> _property = property;
        private RequestType _type = RequestType.Read;
        private string _key = string.Empty;
        private Q? _value;
        private RequestOption? _option;

        public Builder SetRequestType(RequestType type)
        {
            _type = type;
            return this;
        }

        public Builder SetKey(string key)
        {
            _key = key;
            return this;
        }

        public Builder SetValue(Q? value)
        {
            _value = value;
            return this;
        }

        public Builder SetOption(RequestOption? option)
        {
            _option = option;
            return this;
        }

        public ExternalRequest<Q, R> Build()
        {
            ArgumentNullException.ThrowIfNull(_property);
            return new ExternalRequest<Q, R>(_type, _property, _key, _value, _option, null);
        }
    }
}
