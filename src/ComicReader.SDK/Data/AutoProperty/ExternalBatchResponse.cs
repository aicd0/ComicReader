// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalBatchResponse
{
    private readonly Dictionary<IExternalRequest, IExternalResponse> _responses = [];

    internal ExternalBatchResponse() { }

    public ExternalResponse<V>? GetResponse<K, V>(ExternalRequest<K, V> request) where K : IRequestKey
    {
        ArgumentNullException.ThrowIfNull(nameof(request));
        if (_responses.TryGetValue(request, out IExternalResponse? response))
        {
            return (ExternalResponse<V>)response;
        }
        return null;
    }

    internal void SetResponse<K, V>(ExternalRequest<K, V> request, ExternalResponse<V> response) where K : IRequestKey
    {
        _responses.Add(request, response);
    }
}
