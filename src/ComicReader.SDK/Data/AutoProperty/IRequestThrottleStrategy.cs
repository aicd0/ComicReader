// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Data.AutoProperty;

internal interface IRequestThrottleStrategy<T> where T : IReadonlyExternalRequest
{
    public void Enqueue(T request);

    public bool TryDequeue([MaybeNullWhen(false)] out T request);

    public void OnRequestCompleted(T request);
}
