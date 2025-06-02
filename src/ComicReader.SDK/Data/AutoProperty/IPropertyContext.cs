// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IPropertyContext
{
    internal void ClearNewRequests();

    internal void RearrangeRequests();

    internal void ProcessRequests(IProcessCallback callback);

    internal void PostProcessRequests(Action action);

    internal void CancelRequest(long id);
}
