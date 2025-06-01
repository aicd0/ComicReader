// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyRequestContent<T>
{
    public RequestType Type { get; }
    public string Key { get; }
    public T? Value { get; }
    public RequestOption Option { get; }

    public PropertyRequestContent(RequestType type, string key, T? value, RequestOption option)
    {
        Type = type;
        Key = key;
        Value = value;
        Option = option;
    }

    public PropertyRequestContent<T> WithRequestType(RequestType type)
    {
        return new(type, Key, Value, Option);
    }

    public PropertyRequestContent<A> WithValue<A>(A? value)
    {
        return new(Type, Key, value, Option);
    }
}
