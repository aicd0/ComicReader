// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class SimplePropertyOperator<T>
{
    private readonly PropertyServer _server;
    private readonly IQRProperty<T, T> _property;
    private readonly CoreExtension _coreExtension = new();
    private readonly ConcurrentDictionary<string, T?> _localCache = [];

    public SimplePropertyOperator(PropertyServer server, IQREProperty<T, T, IValueObserverExtension<T>> property)
    {
        _server = server;
        _property = property;
        server.RegisterExtension(property, _coreExtension);
    }

    public T? LocalRead(string key)
    {
        if (_localCache.TryGetValue(key, out T? value))
        {
            return value;
        }
        if (_coreExtension.cache.TryGetValue(key, out value))
        {
            return value;
        }
        return default;
    }

    public void LocalWrite(string key, T value)
    {
        _localCache[key] = value;
    }

    public async Task<T?> Read(string key)
    {
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(_property).SetRequestType(RequestType.Read).SetKey(key).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<T>? response = batchResponse.GetResponse(request);
        if (response != null && response.Result == RequestResult.Successful)
        {
            _localCache[key] = response.Value;
            return response.Value;
        }
        return default;
    }

    public async Task Write(string key, T value, RequestOption? option = null)
    {
        await WriteInternal(key, value, option);
        _localCache.TryRemove(key, out _);
    }

    private async Task<bool> WriteInternal(string key, T value, RequestOption? option)
    {
        ExternalBatchRequest batchRequest = new();
        ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(_property).SetRequestType(RequestType.Modify).SetKey(key).SetValue(value).SetOption(option).Build();
        batchRequest.Requests.Add(request);
        ExternalBatchResponse batchResponse = await _server.Request(batchRequest);
        ExternalResponse<T>? response = batchResponse.GetResponse(request);
        return response != null && response.Result == RequestResult.Successful;
    }

    private class CoreExtension : IValueObserverExtension<T>
    {
        public readonly ConcurrentDictionary<string, T?> cache = [];

        public void UpdateValue(string key, T? value)
        {
            cache[key] = value;
        }
    }
}
