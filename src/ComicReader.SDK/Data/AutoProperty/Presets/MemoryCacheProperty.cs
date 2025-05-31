// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

internal class MemoryCacheProperty<T>(IQRProperty<T, T> source) : AbsProperty<T, T, MemoryCachePropertyModel<T>>
{
    public override MemoryCachePropertyModel<T> CreateModel()
    {
        return new MemoryCachePropertyModel<T>();
    }

    public override void RearrangeRequests(PropertyContext<T, T, MemoryCachePropertyModel<T>> context)
    {
        MemoryCachePropertyModel<T> model = context.Model;
        foreach (ServerPropertyRequest<T> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<T> request = serverRequest.RequestContent;
            if (!model.cacheItems.TryGetValue(request.Key, out MemoryCachePropertyModel<T>.CacheItem? cache))
            {
                cache = new MemoryCachePropertyModel<T>.CacheItem();
                model.cacheItems[request.Key] = cache;
            }
            cache.requests.Enqueue(serverRequest);
            if (cache.requests.Count == 1)
            {
                DequeueRequests(context, cache);
            }
        }
    }

    public override void ProcessRequests(PropertyContext<T, T, MemoryCachePropertyModel<T>> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void DequeueRequests(PropertyContext<T, T, MemoryCachePropertyModel<T>> context, MemoryCachePropertyModel<T>.CacheItem cache)
    {
        bool requesting = false;
        while (!requesting && cache.requests.TryPeek(out ServerPropertyRequest<T>? request))
        {
            switch (request.RequestContent.Type)
            {
                case RequestType.Read:
                    {
                        PropertyResponseContent<T>? response = cache.readResponse;
                        if (response is not null)
                        {
                            cache.requests.Dequeue();
                            context.Respond(request.Id, response);
                            break;
                        }
                        ServerPropertyRequest<T>? subRequest = context.Request(source, request.RequestContent, OnReadResponse);
                        if (subRequest is null)
                        {
                            cache.requests.Dequeue();
                            context.Respond(request.Id, PropertyResponseContent<T>.NewFailedResponse());
                            break;
                        }
                        context.Model.requests[subRequest.Id] = cache;
                        requesting = true;
                    }
                    break;
                case RequestType.Modify:
                    {
                        PropertyResponseContent<T>? response = cache.writeResponse;
                        if (response is not null)
                        {
                            cache.writeResponse = null;
                            if (response.Result == RequestResult.Successful)
                            {
                                cache.readResponse = PropertyResponseContent<T>.NewSuccessfuleResponse(request.RequestContent.Value);
                            }
                            cache.requests.Dequeue();
                            context.Respond(request.Id, response);
                            break;
                        }
                        ServerPropertyRequest<T>? subRequest = context.Request(source, request.RequestContent, OnWriteResponse);
                        if (subRequest is null)
                        {
                            cache.requests.Dequeue();
                            context.Respond(request.Id, PropertyResponseContent<T>.NewFailedResponse());
                            break;
                        }
                        context.Model.requests[subRequest.Id] = cache;
                        requesting = true;
                    }
                    break;
                default:
                    Logger.AssertNotReachHere("E6180AB865322377");
                    cache.requests.Dequeue();
                    context.Respond(request.Id, PropertyResponseContent<T>.NewFailedResponse());
                    break;
            }
        }
    }

    private void OnReadResponse(PropertyContext<T, T, MemoryCachePropertyModel<T>> context, long id, PropertyResponseContent<T> response)
    {
        if (!context.Model.requests.Remove(id, out MemoryCachePropertyModel<T>.CacheItem? cache))
        {
            Logger.AssertNotReachHere("62BE9E80ECABA2A9");
            return;
        }
        cache.readResponse = response;
        DequeueRequests(context, cache);
    }

    private void OnWriteResponse(PropertyContext<T, T, MemoryCachePropertyModel<T>> context, long id, PropertyResponseContent<T> response)
    {
        if (!context.Model.requests.Remove(id, out MemoryCachePropertyModel<T>.CacheItem? cache))
        {
            Logger.AssertNotReachHere("438086EBB6951D9E");
            return;
        }
        cache.writeResponse = response;
        DequeueRequests(context, cache);
    }
}
