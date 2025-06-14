// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IReadonlyExternalRequest
{
    internal RequestType Type { get; }

    internal bool IsNullValue();
}
