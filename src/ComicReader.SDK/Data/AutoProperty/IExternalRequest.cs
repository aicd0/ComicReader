// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IExternalRequest : IReadonlyExternalRequest
{
    IExternalRequest Clone();

    void SetFailedResult(string reason);

    void Request(IServerContext context, IProperty sender, Action callback);
}
