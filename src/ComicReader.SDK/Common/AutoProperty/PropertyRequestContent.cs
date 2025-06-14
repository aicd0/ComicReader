// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public class PropertyRequestContent<K, V> where K : IRequestKey
{
    public RequestType Type { get; }
    public K Key { get; }
    public V? Value { get; }
    public RequestOption Option { get; }
    public LockToken Lock { get; }

    public PropertyRequestContent(RequestType type, K key, V? value, RequestOption option, LockToken lockToken)
    {
        Type = type;
        Key = key;
        Value = value;
        Option = option;
        Lock = lockToken;
    }

    public PropertyRequestContent<A, B> WithKeyAndValue<A, B>(A key, B? value) where A : IRequestKey
    {
        return new(Type, key, value, Option, Lock);
    }

    public PropertyRequestContent<K, V> WithRequestTypeAndValueAndLock(RequestType type, V? value, LockToken token)
    {
        return new(type, Key, value, Option, token);
    }

    internal PropertyRequestContent<K, V> WithLock(LockToken token)
    {
        return new(Type, Key, Value, Option, token);
    }
}
