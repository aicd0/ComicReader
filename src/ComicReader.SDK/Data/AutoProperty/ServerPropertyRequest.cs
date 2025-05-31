// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ServerPropertyRequest<T>
{
    public long Id { get; }
    public PropertyRequestContent<T> RequestContent { get; }

    public ServerPropertyRequest(long id, PropertyRequestContent<T> requestContent)
    {
        Id = id;
        RequestContent = requestContent;
    }
}
