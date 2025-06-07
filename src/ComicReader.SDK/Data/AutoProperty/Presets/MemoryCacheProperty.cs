// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.AutoProperty.Extension;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MemoryCacheProperty<K, V>(IKVProperty<K, V> source) : AbsProperty<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> where K : IRequestKey
{
    public override MemoryCachePropertyModel<K, V> CreateModel()
    {
        return new MemoryCachePropertyModel<K, V>();
    }

    public override LockResource GetLockResource(K key, LockType type)
    {
        return source.GetLockResource(key, type);
    }

    public override void RearrangeRequests(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> context)
    {
        MemoryCachePropertyModel<K, V> model = context.Model;
        foreach (SealedPropertyRequest<K, V> request in context.NewRequests)
        {
            PropertyRequestContent<K, V> requestContnet = request.RequestContent;
            if (!model.cacheItems.TryGetValue(requestContnet.Key, out MemoryCachePropertyModel<K, V>.CacheItem? cache))
            {
                cache = new();
                model.cacheItems[requestContnet.Key] = cache;
            }

            if (cache.response is not null)
            {
                if (cache.response.Tracker is null || cache.response.Version < cache.response.Tracker.Version)
                {
                    cache.response = null;
                }
            }

            switch (requestContnet.Type)
            {
                case RequestType.Read:
                    {
                        PropertyResponseContent<V>? response = cache.response;
                        if (response is not null)
                        {
                            MemoryCacheProperty<K, V>.OnValueUpdate(context, request.RequestContent.Key, response.Value);
                            context.Respond(request.Id, response);
                            break;
                        }
                        if (cache.pendingRequests.Count == 0)
                        {
                            OperationResult result = context.Request(source, request.RequestContent, OnReadResponse, out long requestId);
                            if (result != OperationResult.Successful)
                            {
                                context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                                break;
                            }
                            context.Model.requests[requestId] = new(request, cache);
                        }
                        cache.pendingRequests.Add(request.Id);
                    }
                    break;
                case RequestType.Modify:
                    {
                        OperationResult result = context.Request(source, request.RequestContent, OnWriteResponse, out long requestId);
                        if (result != OperationResult.Successful)
                        {
                            context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                            break;
                        }
                        context.Model.requests[requestId] = new(request, cache);
                    }
                    break;
                default:
                    Logger.AssertNotReachHere("E6180AB865322377");
                    context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                    break;
            }
        }
    }

    public override void ProcessRequests(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void OnReadResponse(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> context, long id, PropertyResponseContent<V> response)
    {
        if (!context.Model.requests.Remove(id, out MemoryCachePropertyModel<K, V>.RequestItem? request))
        {
            Logger.AssertNotReachHere("62BE9E80ECABA2A9");
            return;
        }
        request.cache.response = response;
        foreach (long requestId in request.cache.pendingRequests)
        {
            context.Respond(requestId, response);
        }
        request.cache.pendingRequests.Clear();
    }

    private void OnWriteResponse(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> context, long id, PropertyResponseContent<V> response)
    {
        if (!context.Model.requests.Remove(id, out MemoryCachePropertyModel<K, V>.RequestItem? request))
        {
            Logger.AssertNotReachHere("438086EBB6951D9E");
            return;
        }
        PropertyResponseContent<V> finalResponse;
        if (response.Result != OperationResult.Successful)
        {
            finalResponse = PropertyResponseContent<V>.NewFailedResponse(response.Tracker, response.Version);
        }
        else
        {
            finalResponse = PropertyResponseContent<V>.NewSuccessfulResponse(request.originalRequest.RequestContent.Value, response.Tracker, response.Version);
            OnValueUpdate(context, request.originalRequest.RequestContent.Key, finalResponse.Value);
        }
        context.Respond(request.originalRequest.Id, finalResponse);
    }

    private static void OnValueUpdate(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IValueObserverExtension<K, V>> context, K key, V? value)
    {
        if (value is null)
        {
            Logger.AssertNotReachHere("5F1124DE08843941");
        }
        foreach (IValueObserverExtension<K, V> extension in context.Extensions)
        {
            extension.UpdateValue(key, value);
        }
    }
}
