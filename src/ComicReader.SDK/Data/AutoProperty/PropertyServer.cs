// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyServer(string name)
{
    private readonly InternalServer _server = new(name);

    public async Task<ExternalBatchResponse> Request(ExternalBatchRequest? request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _server.Request(request);
    }
}
