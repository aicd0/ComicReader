// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReader.SDK.Data.AutoProperty.Extension;

namespace ComicReader.SDK.Data.AutoProperty.Utils;

public class SimplePropertyOperator<K, V> where K : IRequestKey
{
    private readonly PropertyServer _server;
    private readonly IKVProperty<K, V> _property;
    private readonly CoreExtension _coreExtension = new();
    private readonly ConcurrentDictionary<K, V?> _localCache = [];

    public SimplePropertyOperator(PropertyServer server, IKVEProperty<K, V, IValueObserverExtension<K, V>> property)
    {
        _server = server;
        _property = property;
        server.RegisterExtension(property, _coreExtension);
    }

    public V? LocalRead(K key)
    {
        if (_localCache.TryGetValue(key, out V? value))
        {
            return value;
        }
        if (_coreExtension.cache.TryGetValue(key, out value))
        {
            return value;
        }
        return default;
    }

    public void LocalWrite(K key, V value)
    {
        _localCache[key] = value;
    }

    public async Task<V?> Read(K key)
    {
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<K, V> request = new ExternalRequest<K, V>.Builder(_property, key).SetRequestType(RequestType.Read).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<V>? response = batchResponse.GetResponse(request);
        if (response != null && response.Result == RequestResult.Successful)
        {
            _localCache[key] = response.Value;
            return response.Value;
        }
        return default;
    }

    public async Task Write(K key, V value, RequestOption? option = null)
    {
        await WriteInternal(key, value, option);
        _localCache.TryRemove(key, out _);
    }

    private async Task<bool> WriteInternal(K key, V value, RequestOption? option)
    {
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<K, V> request = new ExternalRequest<K, V>.Builder(_property, key).SetRequestType(RequestType.Modify).SetValue(value).SetOption(option).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<V>? response = batchResponse.GetResponse(request);
        return response != null && response.Result == RequestResult.Successful;
    }

    private class CoreExtension : IValueObserverExtension<K, V>
    {
        public readonly ConcurrentDictionary<K, V?> cache = [];

        public void UpdateValue(K key, V? value)
        {
            cache[key] = value;
        }
    }
}
