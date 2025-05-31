// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IReadonlyExternalRequest
{
    public RequestType Type { get; }
    public string Key { get; }

    public bool IsNullValue();
}
