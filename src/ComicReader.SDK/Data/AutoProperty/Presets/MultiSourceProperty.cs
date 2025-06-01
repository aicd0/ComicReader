// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MultiSourceProperty<T>(List<IQRProperty<T, T>> sources) : AbsProperty<T, T, MultiSourcePropertyModel<T>, IPropertyExtension>
{
    private readonly List<IQRProperty<T, T>> _sources = [.. sources];

    public override MultiSourcePropertyModel<T> CreateModel()
    {
        return new MultiSourcePropertyModel<T>();
    }

    public override List<IProperty> GetDependentProperties()
    {
        return [.. _sources];
    }

    public override void RearrangeRequests(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context)
    {
        MultiSourcePropertyModel<T> model = context.Model;
        foreach (SealedPropertyRequest<T> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<T> request = serverRequest.RequestContent;
            if (!model.cacheItems.TryGetValue(request.Key, out MultiSourcePropertyModel<T>.CacheItem? cache))
            {
                cache = new MultiSourcePropertyModel<T>.CacheItem();
                model.cacheItems[request.Key] = cache;
            }
            cache.requests.Enqueue(serverRequest);
            if (cache.requests.Count == 1)
            {
                DequeueRequests(context, cache);
            }
        }
    }

    public override void ProcessRequests(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void DequeueRequests(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context, MultiSourcePropertyModel<T>.CacheItem cache)
    {
        bool requesting = false;
        while (!requesting && cache.requests.TryPeek(out SealedPropertyRequest<T>? request))
        {
            if (_sources.Count == 0)
            {
                Logger.AssertNotReachHere("7572F634B2A6413C");
                cache.requests.Dequeue();
                context.Respond(request.Id, PropertyResponseContent<T>.NewFailedResponse());
                break;
            }
            switch (request.RequestContent.Type)
            {
                case RequestType.Read:
                    {
                        PropertyResponseContent<T>? response = cache.readResponse;
                        if (response is not null)
                        {
                            cache.readResponse = null;
                            if (response.Result == RequestResult.Successful)
                            {
                                cache.readIndex = 0;
                                cache.requests.Dequeue();
                                context.Respond(request.Id, response);
                                PropertyRequestContent<T> compensateRequest = request.RequestContent.WithRequestType(RequestType.Modify);
                                for (int i = 0; i < cache.readIndex; ++i)
                                {
                                    context.Request(_sources[i], compensateRequest, OnCompensateResponse);
                                }
                                break;
                            }
                            if (++cache.readIndex >= _sources.Count)
                            {
                                cache.readIndex = 0;
                                cache.requests.Dequeue();
                                context.Respond(request.Id, response);
                                break;
                            }
                        }
                        SealedPropertyRequest<T>? subRequest = context.Request(_sources[cache.readIndex], request.RequestContent, OnReadResponse);
                        if (subRequest is null)
                        {
                            cache.readIndex = 0;
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
                            if (response.Result != RequestResult.Successful)
                            {
                                cache.writeIndex = -1;
                                cache.requests.Dequeue();
                                context.Respond(request.Id, response);
                                break;
                            }
                            if (--cache.writeIndex < 0)
                            {
                                cache.requests.Dequeue();
                                context.Respond(request.Id, response);
                                break;
                            }
                        }
                        if (cache.writeIndex < 0)
                        {
                            cache.writeIndex = _sources.Count - 1;
                        }
                        SealedPropertyRequest<T>? subRequest = context.Request(_sources[cache.writeIndex], request.RequestContent, OnWriteResponse);
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
                    Logger.AssertNotReachHere("0703A032230BD03C");
                    cache.requests.Dequeue();
                    context.Respond(request.Id, PropertyResponseContent<T>.NewFailedResponse());
                    break;
            }
        }
    }

    private void OnReadResponse(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context, long id, PropertyResponseContent<T> response)
    {
        if (!context.Model.requests.Remove(id, out MultiSourcePropertyModel<T>.CacheItem? cache))
        {
            Logger.AssertNotReachHere("9701FB2B66EC550E");
            return;
        }
        cache.readResponse = response;
        DequeueRequests(context, cache);
    }

    private void OnWriteResponse(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context, long id, PropertyResponseContent<T> response)
    {
        if (!context.Model.requests.Remove(id, out MultiSourcePropertyModel<T>.CacheItem? cache))
        {
            Logger.AssertNotReachHere("9E8E40A5965DEC73");
            return;
        }
        cache.writeResponse = response;
        DequeueRequests(context, cache);
    }

    private void OnCompensateResponse(PropertyContext<T, T, MultiSourcePropertyModel<T>, IPropertyExtension> context, long id, PropertyResponseContent<T> response)
    {
    }
}
