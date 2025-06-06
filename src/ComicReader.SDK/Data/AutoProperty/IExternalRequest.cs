// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Data.AutoProperty;

public interface IExternalRequest : IReadonlyExternalRequest
{
    internal IExternalRequest Clone();

    internal void Activate(BatchInfo batch);

    internal void SetFailedResult(string reason);

    internal bool TryGetLockResource(IServerContext server, [MaybeNullWhen(false)] out LockResource resource);

    internal void Request(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, LockToken token);
}
