// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IExternalRequest : IReadonlyExternalRequest
{
    internal IExternalRequest Clone();

    internal void Activate(BatchInfo batch);

    internal void SetFailedResult(string reason);

    internal bool TryRequest(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, LockManager lockManager);
}
