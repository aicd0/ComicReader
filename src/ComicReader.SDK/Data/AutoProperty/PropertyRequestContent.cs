// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyRequestContent<T>
{
    public RequestType Type { get; }
    public string Key { get; }
    public T? Value { get; }
    public bool IsPersistent { get; }

    public PropertyRequestContent(RequestType type, string key, T? value, bool isPersistent)
    {
        Type = type;
        Key = key;
        Value = value;
        IsPersistent = isPersistent;
    }
}
