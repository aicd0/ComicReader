// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalResponse<T> : IExternalResponse
{
    public RequestResult Result { get; }
    public T? Value { get; }
    public string Reason { get; }

    internal ExternalResponse(RequestResult result, T? value = default, string reason = "")
    {
        Result = result;
        Value = value;
        Reason = reason;
    }
}
