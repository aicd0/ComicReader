// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyResponseContent<T>
{
    public RequestResult Result { get; }
    public T? Value { get; }

    private PropertyResponseContent(RequestResult result, T? value)
    {
        Result = result;
        Value = value;
    }

    public static PropertyResponseContent<T> NewSuccessfuleResponse(T? value = default)
    {
        return new(RequestResult.Successful, value);
    }

    public static PropertyResponseContent<T> NewFailedResponse()
    {
        return new(RequestResult.Failed, default);
    }
}
