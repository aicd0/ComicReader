// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IPropertyContext
{
    internal IProperty Property { get; }

    internal void ClearNewRequests();

    internal void RearrangeRequests();

    internal void ProcessRequests(IProcessCallback callback);

    internal void HandleProcessCallback(Action action);

    internal void CancelRequest(long id);
}
