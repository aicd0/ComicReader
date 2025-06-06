// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalResponse<V> : IExternalResponse
{
    public RequestResult Result { get; }
    public V? Value { get; }
    public string Reason { get; }

    internal ExternalResponse(RequestResult result, V? value = default, string reason = "")
    {
        Result = result;
        Value = value;
        Reason = reason;
    }
}
