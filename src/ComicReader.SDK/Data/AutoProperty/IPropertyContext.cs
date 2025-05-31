// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IPropertyContext
{
    void ClearNewRequests();

    void RearrangeRequests();

    void ProcessRequests(IProcessCallback callback);

    void CancelRequest(long id);
}
