// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class SealedPropertyRequest<T>(long id, PropertyRequestContent<T> requestContent)
{
    public long Id { get; } = id;
    public PropertyRequestContent<T> RequestContent { get; } = requestContent;
}
