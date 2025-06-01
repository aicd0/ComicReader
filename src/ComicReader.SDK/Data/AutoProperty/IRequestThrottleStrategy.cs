// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReader.SDK.Data.AutoProperty;

public interface IRequestThrottleStrategy<T> where T : IReadonlyExternalRequest
{
    void Enqueue(T request);

    bool TryDequeue([MaybeNullWhen(false)] out T request);

    void OnRequestCompleted(T request);
}
