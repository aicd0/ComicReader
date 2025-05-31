// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalRequest<Q, R> : IExternalRequest
{
    private Action? _callback;

    public RequestType Type { get; }
    public IQRProperty<Q, R> Property { get; }
    public string Key { get; }
    public Q? Value { get; }
    public bool IsPersistent { get; }
    public ExternalResponse<R>? Response { get; internal set; } = null;

    private ExternalRequest(RequestType type, IQRProperty<Q, R> property, string key, Q? value = default, bool isPersistent = false)
    {
        Type = type;
        Property = property;
        Key = key;
        Value = value;
        IsPersistent = isPersistent;
    }

    public bool IsNullValue()
    {
        return Value is null;
    }

    public IExternalRequest Clone()
    {
        return new ExternalRequest<Q, R>(Type, Property, Key, Value, IsPersistent);
    }

    public void SetFailedResult(string reason)
    {
        Logger.Assert(Response == null, "C63AA53A5E9DC7BA");
        Response = new ExternalResponse<R>(RequestResult.Failed, reason: reason);
    }

    public void Request(IServerContext context, IProperty sender, Action callback)
    {
        _callback = callback;
        PropertyRequestContent<Q> requestContent = new(Type, Key, Value, IsPersistent);
        ServerPropertyRequest<Q>? request = context.HandleRequest(sender, Property, requestContent, OnResponse);
        if (request is null)
        {
            Logger.AssertNotReachHere("97FB6CC007779834");
            Response = new ExternalResponse<R>(RequestResult.Failed);
            callback();
        }
    }

    private void OnResponse(long id, PropertyResponseContent<R> response)
    {
        Logger.Assert(Response is null, "41C968642F60220C");
        Response = new ExternalResponse<R>(response.Result, response.Value);
        _callback!();
    }

    public class Builder(IQRProperty<Q, R> property)
    {
        private readonly IQRProperty<Q, R> _property = property;
        private RequestType _type = RequestType.Read;
        private string _key = string.Empty;
        private Q? _value;
        private bool _isPersistent = false;

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

        public Builder SetPersistent(bool isPersistent)
        {
            _isPersistent = isPersistent;
            return this;
        }

        public ExternalRequest<Q, R> Build()
        {
            ArgumentNullException.ThrowIfNull(_property);
            return new ExternalRequest<Q, R>(_type, _property, _key, _value, _isPersistent);
        }
    }
}
