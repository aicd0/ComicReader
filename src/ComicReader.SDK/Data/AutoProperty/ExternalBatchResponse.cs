// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ExternalBatchResponse
{
    private readonly Dictionary<IExternalRequest, IExternalResponse> _responses = [];

    internal ExternalBatchResponse() { }

    public ExternalResponse<R>? GetResponse<Q, R>(ExternalRequest<Q, R> request)
    {
        ArgumentNullException.ThrowIfNull(nameof(request));
        if (_responses.TryGetValue(request, out IExternalResponse? response))
        {
            return (ExternalResponse<R>)response;
        }
        return null;
    }

    internal void SetResponse<Q, R>(ExternalRequest<Q, R> request, ExternalResponse<R> response)
    {
        _responses.Add(request, response);
    }
}
