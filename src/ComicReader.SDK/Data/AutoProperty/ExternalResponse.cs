// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalResponse<V> : IExternalResponse
{
    public OperationResult Result { get; }
    public V? Value { get; }
    public string Message { get; }

    internal ExternalResponse(OperationResult result, V? value = default, string message = "")
    {
        Result = result;
        Value = value;
        Message = message;
    }
}
