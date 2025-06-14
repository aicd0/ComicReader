// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Common.AutoProperty.Presets;

public class MemoryCacheProperty<K, V>(IKVProperty<K, V> source) : AbsProperty<K, V, MemoryCachePropertyModel<K, V>, IPropertyExtension> where K : IRequestKey
{
    public override MemoryCachePropertyModel<K, V> CreateModel()
    {
        return new MemoryCachePropertyModel<K, V>();
    }

    public override LockResource GetLockResource(K key, LockType type)
    {
        return source.GetLockResource(key, type);
    }

    public override void RearrangeRequests(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IPropertyExtension> context)
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

    public override void ProcessRequests(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostOnServerThread(true, null);
    }

    private void OnReadResponse(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
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

    private void OnWriteResponse(PropertyContext<K, V, MemoryCachePropertyModel<K, V>, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
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
        }
        context.Respond(request.originalRequest.Id, finalResponse);
    }
}
