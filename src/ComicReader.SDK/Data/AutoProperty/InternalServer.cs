// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Data.AutoProperty;

internal class InternalServer(string name) : IServerContext
{
    private const int MAX_REQUEST_COUNT = 65536;

    private static readonly ITaskDispatcher sDispatcher = TaskDispatcher.Factory.NewQueue($"PropertyServer");

    private readonly ConcurrentQueue<BatchInfo> _batchInfoQueue = [];
    private readonly ConcurrentQueue<Action> _processCallbackActions = [];
    private readonly ThreadLocal<bool> _threadFlag = new();
    private int _serverRoutineScheduled = 0;

    private readonly IRequestThrottleStrategy<ExternalRequestWrapper> _throttleStrategy = new ParallelReadThrottleStrategy<ExternalRequestWrapper>();
    private int _nextRequestId = 0;
    private readonly Dictionary<long, IPropertyRequest> _requests = [];
    private readonly HashSet<ProcessCallback> _processCallbacks = [];
    private readonly Dictionary<IProperty, IPropertyContext> _propertyContextMap = [];
    private readonly ServerProperty _serverProperty = new();

    private readonly List<Action<DelayActionResult>> _delayedActions = [];

    public async Task<ExternalBatchResponse> Request(ExternalBatchRequest request)
    {
        var batchInfo = new BatchInfo(request);
        _batchInfoQueue.Enqueue(batchInfo);
        ScheduleServerRoutine("Request");
        return await batchInfo.CompletionSource.Task;
    }

    private void ScheduleServerRoutine(string reason)
    {
        if (Interlocked.CompareExchange(ref _serverRoutineScheduled, 1, 0) == 0)
        {
            sDispatcher.Submit($"{name}.{reason}", delegate
            {
                Interlocked.Exchange(ref _serverRoutineScheduled, 0);
                _threadFlag.Value = true;
                ServerRoutine();
                _threadFlag.Value = false;
            });
        }
    }

    private void ServerRoutine()
    {
        while (true)
        {
            EnqueueBatches();
            DequeueProcessCallbacks();
            DequeueBatches();

            if (_delayedActions.Count == 0 && _requests.Count > 0 && _processCallbacks.Count == 0)
            {
                Logger.AssertNotReachHere("58F8BEB2DA18331E");
                foreach (IPropertyRequest request in _requests.Values)
                {
                    GetOrCreatePropertyContext(request.Receiver).CancelRequest(request.Id);
                }
            }

            if (_delayedActions.Count == 0)
            {
                break;
            }

            RearrangeRequests();
            ProcessRequests();
        }
    }

    private void EnqueueBatches()
    {
        while (_batchInfoQueue.TryDequeue(out BatchInfo? batchInfo))
        {
            List<IExternalRequest> requests = [.. batchInfo.Request.Requests];
            batchInfo.RemainingRequests = requests.Count;
            foreach (IExternalRequest request in requests)
            {
                if (string.IsNullOrEmpty(request.Key))
                {
                    request.SetFailedResult("Key is empty.");
                    batchInfo.CompleteOne();
                    continue;
                }
                if (request.Type == RequestType.Modify && request.IsNullValue())
                {
                    request.SetFailedResult("Value is null for modify request.");
                    batchInfo.CompleteOne();
                    continue;
                }
                ExternalRequestWrapper wrapper = new(request.Clone(), batchInfo);
                _throttleStrategy.Enqueue(wrapper);
            }
        }
    }

    private void DequeueProcessCallbacks()
    {
        while (_processCallbackActions.TryDequeue(out Action? action))
        {
            action();
        }
    }

    private void DequeueBatches()
    {
        while (_throttleStrategy.TryDequeue(out ExternalRequestWrapper? wrapper))
        {
            wrapper.Request.Request(this, _serverProperty, delegate
            {
                _throttleStrategy.OnRequestCompleted(wrapper);
                wrapper.Batch.CompleteOne();
            });
        }
    }

    private void RearrangeRequests()
    {
        DelayActionResult delayActionResult = new();
        while (true)
        {
            foreach (Action<DelayActionResult> action in _delayedActions)
            {
                action(delayActionResult);
            }
            _delayedActions.Clear();
            foreach (Action action in delayActionResult.HandlerActions)
            {
                action();
            }
            if (delayActionResult.RearrangingProperties.Count == 0)
            {
                break;
            }
            foreach (IPropertyContext property in delayActionResult.RearrangingProperties)
            {
                property.RearrangeRequests();
                property.ClearNewRequests();
            }
            delayActionResult.Reset();
        }
    }

    private void ProcessRequests()
    {
        List<IPropertyRequest> requests = [.. _requests.Values];
        HashSet<IProperty> senders = [];
        foreach (IPropertyRequest request in requests)
        {
            senders.Add(request.Sender);
        }
        HashSet<IProperty> receivers = [];
        foreach (IPropertyRequest request in requests)
        {
            if (!senders.Contains(request.Receiver))
            {
                if (request.State == RequestState.Processing)
                {
                    Logger.AssertNotReachHere("929C4A72E8E3C8E6");
                    GetOrCreatePropertyContext(request.Receiver).CancelRequest(request.Id);
                    continue;
                }
                request.State = RequestState.Processing;
            }
            receivers.Add(request.Receiver);
        }
        foreach (IProperty property in receivers)
        {
            IPropertyContext propertyContext = GetOrCreatePropertyContext(property);
            ProcessCallback processCallback = new(this);
            _processCallbacks.Add(processCallback);
            propertyContext.ProcessRequests(processCallback);
        }
    }

    public ServerPropertyRequest<Q>? HandleRequest<Q, R>(IProperty sender, IQRProperty<Q, R> receiver, PropertyRequestContent<Q> requestContent, Action<long, PropertyResponseContent<R>> handler)
    {
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("A063B60F57EBA565");
            return null;
        }

        if (sender == receiver)
        {
            Logger.AssertNotReachHere("92922E47E55B3E4A");
            return null;
        }
        if (_requests.Count >= MAX_REQUEST_COUNT)
        {
            Logger.AssertNotReachHere("EAD7677C9D9E84BE");
            return null;
        }
        int requestId = _nextRequestId++;
        PropertyRequest<Q, R> request = new(requestId, sender, receiver, requestContent, handler);
        _requests[requestId] = request;

        _delayedActions.Add((result) =>
        {
            IQRPropertyContext<Q, R> receiverContext = GetOrCreatePropertyContext(receiver);
            receiverContext.AddNewRequest(requestId, requestContent);
            result.RearrangingProperties.Add(receiverContext);
        });
        return new ServerPropertyRequest<Q>(requestId, requestContent);
    }

    public void HandleRespond<Q, R>(IQRProperty<Q, R> receiver, long requestId, PropertyResponseContent<R> responseContent)
    {
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("CAEA4B19F1AF8BFE");
            return;
        }

        PropertyRequest<Q, R> request = GetRequest<Q, R>(requestId);
        if (request.State == RequestState.Completed)
        {
            Logger.AssertNotReachHere("08E129B618734844");
            return;
        }
        if (request.ResponseContent != null)
        {
            Logger.AssertNotReachHere("D1873C6A32900151");
            return;
        }
        if (request.Receiver != receiver)
        {
            Logger.AssertNotReachHere("167FF26E226CE99B");
            return;
        }
        request.ResponseContent = responseContent;
        request.State = RequestState.Completed;
        _requests.Remove(requestId);

        _delayedActions.Add((result) =>
        {
            result.RearrangingProperties.Add(GetOrCreatePropertyContext(request.Sender));
            result.HandlerActions.Add(() =>
            {
                request.Handler(request.Id, responseContent);
            });
        });
    }

    public bool HandleRedirect<Q, R>(IQRProperty<Q, R> oldReceiver, long requestId, IQRProperty<Q, R> newReceiver)
    {
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("9D3D2E0E9978756F");
            return false;
        }

        PropertyRequest<Q, R> request = GetRequest<Q, R>(requestId);
        if (request.State == RequestState.Completed)
        {
            Logger.AssertNotReachHere("7D849855150D963C");
            return false;
        }
        if (request.ResponseContent != null)
        {
            Logger.AssertNotReachHere("4F239172DE93AAE6");
            return false;
        }
        if (request.Receiver != oldReceiver)
        {
            Logger.AssertNotReachHere("DB8810C2F9FD2A64");
            return false;
        }
        if (oldReceiver == newReceiver)
        {
            Logger.AssertNotReachHere("1FDE7E144FF60CA5");
            return false;
        }
        request.Receiver = newReceiver;

        _delayedActions.Add((result) =>
        {
            IQRPropertyContext<Q, R> receiverContext = GetOrCreatePropertyContext(newReceiver);
            receiverContext.AddNewRequest(requestId, request.RequestContent);
            result.RearrangingProperties.Add(receiverContext);
        });
        return true;
    }

    private IPropertyContext GetOrCreatePropertyContext(IProperty property)
    {
        if (!_propertyContextMap.TryGetValue(property, out IPropertyContext? context))
        {
            context = property.CreatePropertyContext(this);
            _propertyContextMap.Add(property, context);
        }
        return context;
    }

    private IQRPropertyContext<Q, R> GetOrCreatePropertyContext<Q, R>(IQRProperty<Q, R> property)
    {
        if (!_propertyContextMap.TryGetValue(property, out IPropertyContext? context))
        {
            context = property.CreatePropertyContext(this);
            _propertyContextMap.Add(property, context);
        }
        return (IQRPropertyContext<Q, R>)context; // must agree
    }

    private PropertyRequest<Q, R> GetRequest<Q, R>(long requestId)
    {
        if (!_requests.TryGetValue(requestId, out IPropertyRequest? request))
        {
            throw new InvalidOperationException("Request not found.");
        }
        return (PropertyRequest<Q, R>)request; // must agree
    }

    private bool IsServerThread()
    {
        return _threadFlag.Value;
    }

    private class ExternalRequestWrapper : IReadonlyExternalRequest
    {
        public IExternalRequest Request { get; }
        public BatchInfo Batch { get; }

        public ExternalRequestWrapper(IExternalRequest request, BatchInfo batch)
        {
            Request = request;
            Batch = batch;
        }

        RequestType IReadonlyExternalRequest.Type => Request.Type;
        string IReadonlyExternalRequest.Key => Request.Key;

        bool IReadonlyExternalRequest.IsNullValue()
        {
            return Request.IsNullValue();
        }
    }

    private class DelayActionResult
    {
        public HashSet<IPropertyContext> RearrangingProperties { get; } = [];
        public List<Action> HandlerActions { get; } = [];

        public void Reset()
        {
            RearrangingProperties.Clear();
            HandlerActions.Clear();
        }
    }

    private class RequestInfo
    {
        public IPropertyRequest Request { get; }
        public ExternalRequestWrapper Wrapper { get; }

        public RequestInfo(IPropertyRequest request, ExternalRequestWrapper wrapper)
        {
            Request = request;
            Wrapper = wrapper;
        }
    }

    internal class BatchInfo
    {
        public ExternalBatchRequest Request { get; }
        public TaskCompletionSource<ExternalBatchResponse> CompletionSource { get; } = new TaskCompletionSource<ExternalBatchResponse>();
        public int RemainingRequests { get; set; }

        public BatchInfo(ExternalBatchRequest request)
        {
            Request = request;
        }

        public void CompleteOne()
        {
            Logger.Assert(RemainingRequests > 0, "A5E3AF05B00F7CED");
            if (--RemainingRequests == 0)
            {
                CompletionSource.TrySetResult(new ExternalBatchResponse());
            }
        }
    }

    private class ProcessCallback(InternalServer server) : IProcessCallback
    {
        private int _completed = 0;

        public void PostCompletion(Action? action)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            {
                Logger.AssertNotReachHere("D82EB9E9BB98CC69");
                return;
            }

            if (server.IsServerThread())
            {
                server._processCallbacks.Remove(this);
                action?.Invoke();
            }
            else
            {
                server._processCallbackActions.Enqueue(() =>
                {
                    server._processCallbacks.Remove(this);
                    action?.Invoke();
                });
                server.ScheduleServerRoutine("ProcessCallback");
            }
        }
    }

    private class ServerProperty : AbsProperty<VoidType, VoidType, VoidType>
    {
        public override VoidType CreateModel()
        {
            return new VoidType();
        }

        public override void RearrangeRequests(PropertyContext<VoidType, VoidType, VoidType> context)
        {
        }

        public override void ProcessRequests(PropertyContext<VoidType, VoidType, VoidType> context, IProcessCallback callback)
        {
            throw new InvalidOperationException("Not supported.");
        }
    }
}
