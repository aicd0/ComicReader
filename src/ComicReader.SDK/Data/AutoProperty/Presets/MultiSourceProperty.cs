// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class MultiSourceProperty<K, V>(List<IKVProperty<K, V>> sources) : AbsProperty<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> where K : IRequestKey
{
    private readonly List<IKVProperty<K, V>> _sources = [.. sources];

    public override MultiSourcePropertyModel<K, V> CreateModel()
    {
        return new MultiSourcePropertyModel<K, V>();
    }

    public override LockResource GetLockResource(K key, LockType type)
    {
        var lockResource = new LockResource();
        for (int i = 0; i < _sources.Count; i++)
        {
            if (i == _sources.Count - 1)
            {
                lockResource.Merge(_sources[i].GetLockResource(key, type));
            }
            else
            {
                lockResource.Merge(_sources[i].GetLockResource(key, LockType.Write));
            }
        }
        return lockResource;
    }

    public override void RearrangeRequests(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context)
    {
        MultiSourcePropertyModel<K, V> model = context.Model;
        foreach (SealedPropertyRequest<K, V> request in context.NewRequests)
        {
            if (_sources.Count == 0)
            {
                Logger.AssertNotReachHere("7572F634B2A6413C");
                context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                break;
            }

            PropertyRequestContent<K, V> requestContent = request.RequestContent;
            if (!model.cacheItems.TryGetValue(requestContent.Key, out MultiSourcePropertyModel<K, V>.CacheItem? cache))
            {
                cache = new MultiSourcePropertyModel<K, V>.CacheItem();
                model.cacheItems[requestContent.Key] = cache;
            }

            switch (requestContent.Type)
            {
                case RequestType.Read:
                    {
                        cache.pendingRequests.Add(request.Id);
                        if (cache.pendingRequests.Count == 1)
                        {
                            ContinueReadRequest(context, request, cache);
                        }
                    }
                    break;
                case RequestType.Modify:
                    {
                        PropertyRequestContent<K, V> subRequestContent = request.RequestContent.WithLock(request.RequestContent.Lock.Readonly());
                        MultiSourcePropertyModel<K, V>.OriginalRequestItem originalRequest = new(request);
                        foreach (IKVProperty<K, V> source in _sources)
                        {
                            OperationResult result = context.Request(source, subRequestContent, OnWriteResponse, out long requestId);
                            if (result != OperationResult.Successful)
                            {
                                if (!originalRequest.responded)
                                {
                                    originalRequest.responded = true;
                                    context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                                }
                                continue;
                            }
                            context.Model.requests[requestId] = new(originalRequest, cache);
                            originalRequest.requesting++;
                        }
                        request.RequestContent.Lock.Release();
                    }
                    break;
                default:
                    Logger.AssertNotReachHere("0703A032230BD03C");
                    context.Respond(request.Id, PropertyResponseContent<V>.NewFailedResponse());
                    break;
            }
        }
    }

    public override void ProcessRequests(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void ContinueReadRequest(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context, SealedPropertyRequest<K, V> request, MultiSourcePropertyModel<K, V>.CacheItem cache)
    {
        long requestId = 0;
        OperationResult result = OperationResult.PropertyError;
        PropertyRequestContent<K, V> requestContent = request.RequestContent.WithLock(request.RequestContent.Lock.Readonly());
        while (result != OperationResult.Successful && cache.readIndex < _sources.Count)
        {
            result = context.Request(_sources[cache.readIndex], requestContent, OnReadResponse, out requestId);
            cache.readIndex++;
        }
        if (result != OperationResult.Successful)
        {
            cache.readIndex = 0;
            foreach (long pendingRequest in cache.pendingRequests)
            {
                context.Respond(pendingRequest, PropertyResponseContent<V>.NewFailedResponse());
            }
            context.Model.cacheItems.Remove(request.RequestContent.Key);
            return;
        }
        MultiSourcePropertyModel<K, V>.OriginalRequestItem originalRequest = new(request);
        context.Model.requests[requestId] = new(originalRequest, cache);
    }

    private void OnReadResponse(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
    {
        if (!context.Model.requests.Remove(id, out MultiSourcePropertyModel<K, V>.RequestItem? request))
        {
            Logger.AssertNotReachHere("9701FB2B66EC550E");
            return;
        }

        request.originalRequest.responses.Add(response);
        if (response.Result != OperationResult.Successful)
        {
            ContinueReadRequest(context, request.originalRequest.request, request.cache);
            return;
        }

        PropertyRequestContent<K, V> originalRequestContent = request.originalRequest.request.RequestContent;
        PropertyRequestContent<K, V> compensationRequest = originalRequestContent.WithRequestTypeAndValueAndLock(RequestType.Modify, response.Value, originalRequestContent.Lock.Readonly());
        for (int i = request.cache.readIndex - 2; i >= 0; i--)
        {
            _ = context.Request(_sources[i], compensationRequest, OnCompensateResponse, out _);
        }
        originalRequestContent.Lock.Release();

        request.cache.readIndex = 0;
        {
            List<IReadonlyResponseTracker> trackers = [];
            int version = 0;
            foreach (PropertyResponseContent<V> res in request.originalRequest.responses)
            {
                if (res.Tracker is not null)
                {
                    trackers.Add(res.Tracker);
                    version += res.Version;
                }
            }
            if (trackers.Count > 0)
            {
                ResponseTracker tracker = context.TrackerManager.GetOrAddTracker(originalRequestContent.Key);
                tracker.UpdateTrackers(trackers, version);
                response = PropertyResponseContent<V>.NewSuccessfulResponse(response.Value, tracker, version);
            }
        }
        foreach (long pendingRequest in request.cache.pendingRequests)
        {
            context.Respond(pendingRequest, response);
        }
        context.Model.cacheItems.Remove(originalRequestContent.Key);
    }

    private void OnWriteResponse(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
    {
        if (!context.Model.requests.Remove(id, out MultiSourcePropertyModel<K, V>.RequestItem? request))
        {
            Logger.AssertNotReachHere("9E8E40A5965DEC73");
            return;
        }

        request.originalRequest.requesting--;
        Logger.Assert(request.originalRequest.requesting >= 0, "A535A27D0D6F1E51");
        request.originalRequest.responses.Add(response);

        if (!request.originalRequest.responded)
        {
            if (response.Result != OperationResult.Successful)
            {
                request.originalRequest.responded = true;
                context.Respond(request.originalRequest.request.Id, PropertyResponseContent<V>.NewFailedResponse());
            }
            else if (request.originalRequest.requesting == 0)
            {
                Logger.Assert(request.originalRequest.responses.Count == _sources.Count, "67C26DE2CD2834B6");
                request.originalRequest.responded = true;
                {
                    List<IReadonlyResponseTracker> trackers = [];
                    int version = 0;
                    foreach (PropertyResponseContent<V> res in request.originalRequest.responses)
                    {
                        if (res.Tracker is not null)
                        {
                            trackers.Add(res.Tracker);
                            version += res.Version;
                        }
                    }
                    if (trackers.Count > 0)
                    {
                        ResponseTracker tracker = context.TrackerManager.GetOrAddTracker(request.originalRequest.request.RequestContent.Key);
                        tracker.UpdateTrackers(trackers, version);
                        response = PropertyResponseContent<V>.NewSuccessfulResponse(default, tracker, version);
                    }
                }
                context.Respond(request.originalRequest.request.Id, response);
            }
        }
    }

    private void OnCompensateResponse(PropertyContext<K, V, MultiSourcePropertyModel<K, V>, IPropertyExtension> context, long id, PropertyResponseContent<V> response)
    {
    }
}
