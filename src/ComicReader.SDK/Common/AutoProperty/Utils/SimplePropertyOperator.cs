// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty.Utils;

public class SimplePropertyOperator<K, K2, V> : IPropertyOperator<K, V> where K2 : IRequestKey
{
    private readonly Func<K, K2> _keyConverter;
    private readonly PropertyServer _server;
    private readonly IKVProperty<K2, V> _property;

    public SimplePropertyOperator(PropertyServer server, IKVProperty<K2, V> property, Func<K, K2> keyConverter)
    {
        _server = server;
        _property = property;
        _keyConverter = keyConverter;
    }

    public async Task<bool> Read(K key)
    {
        K2 realKey = _keyConverter(key);
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<K2, V> request = new ExternalRequest<K2, V>.Builder(_property, realKey).SetRequestType(RequestType.Read).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<V>? response = batchResponse.GetResponse(request);
        return response != null && response.Result == OperationResult.Successful;
    }

    public async Task<bool> Write(K key, V value, RequestOption? option = null)
    {
        K2 realKey = _keyConverter(key);
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<K2, V> request = new ExternalRequest<K2, V>.Builder(_property, realKey).SetRequestType(RequestType.Modify).SetValue(value).SetOption(option).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<V>? response = batchResponse.GetResponse(request);
        return response != null && response.Result == OperationResult.Successful;
    }
}
