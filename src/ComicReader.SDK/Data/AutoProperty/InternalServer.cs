// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Data.AutoProperty;

internal class InternalServer : IServerContext
{
    private const int MAX_REQUEST_COUNT = 65536;

    private static readonly ITaskDispatcher sDispatcher = TaskDispatcher.Factory.NewQueue($"PropertyServer");

    private readonly ConcurrentQueue<BatchInfo> _batchInfoQueue = [];
    private readonly ConcurrentQueue<Action> _processCallbackActions = [];
    private readonly ThreadLocal<bool> _threadFlag = new();
    private int _serverRoutineScheduled = 0;

    private readonly string _name;
    private readonly ServerProperty _serverProperty;
    private readonly RequestManager _requestManager = new();
    private readonly LockManager _lockManager = new();
    private readonly HashSet<ProcessCallback> _processCallbacks = [];
    private readonly Dictionary<IProperty, IPropertyContext> _propertyContextMap = [];
    private int _nextRequestId = 1;

    private readonly List<Action<DelayActionResult>> _delayedActions = [];

    public InternalServer(string name)
    {
        _name = name;
        _serverProperty = new(this);
    }

    public Task<ExternalBatchResponse> Request(ExternalBatchRequest request)
    {
        var batchInfo = new BatchInfo(request);
        _batchInfoQueue.Enqueue(batchInfo);
        ScheduleServerRoutine("Request");
        return batchInfo.CompletionSource.Task;
    }

    public void RegisterExtension<E>(IEProperty<E> property, E extension) where E : IPropertyExtension
    {
        PostOnServerThread("RegisterExtension", () =>
        {
            GetOrCreatePropertyContext(property).RegisterExtension(extension);
        });
    }

    private void ScheduleServerRoutine(string reason)
    {
        if (Interlocked.CompareExchange(ref _serverRoutineScheduled, 1, 0) == 0)
        {
            PostOnServerThread(reason, () =>
            {
                Interlocked.Exchange(ref _serverRoutineScheduled, 0);
                ServerRoutine();
            });
        }
    }

    private void PostOnServerThread(string reason, Action action)
    {
        sDispatcher.Submit($"{_name}.{reason}", () =>
        {
            _threadFlag.Value = true;
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("B4604C74D0E10DAC", e);
                throw;
            }
            finally
            {
                _threadFlag.Value = false;
            }
        });
    }

    private void ServerRoutine()
    {
        while (true)
        {
            DequeueProcessCallbacks();
            DequeueBatches();

            if (_delayedActions.Count == 0 && !_lockManager.ServerInvalidated)
            {
                break;
            }

            RearrangeRequests();
            ProcessRequests();
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
        while (_batchInfoQueue.TryDequeue(out BatchInfo? batchInfo))
        {
            List<IExternalRequest> requests = [.. batchInfo.Request.Requests];
            if (requests.Count == 0)
            {
                batchInfo.CompleteNow();
                continue;
            }
            batchInfo.RemainingRequests = requests.Count;
            foreach (IExternalRequest rawRequest in requests)
            {
                IExternalRequest request = rawRequest.Clone();
                request.Activate(batchInfo);
                if (request.Type == RequestType.Modify && request.IsNullValue())
                {
                    request.SetResult(OperationResult.InvalidArgument, "Value is null for modify request.");
                    continue;
                }
                _serverProperty.EnqueueRequest(request);
                _lockManager.ServerInvalidated = true;
            }
        }
    }

    private void RearrangeRequests()
    {
        DelayActionResult delayActionResult = new();
        while (true)
        {
            if (_lockManager.ServerInvalidated)
            {
                _lockManager.ServerInvalidated = false;
                delayActionResult.RearrangingProperties.Add(GetOrCreatePropertyContext((IProperty)_serverProperty));
            }
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
        IReadOnlyList<IProperty> properties = _requestManager.StartProcess();
        foreach (IProperty property in properties)
        {
            IPropertyContext propertyContext = GetOrCreatePropertyContext(property);
            ProcessCallback processCallback = new(this, propertyContext);
            _processCallbacks.Add(processCallback);
            propertyContext.ProcessRequests(processCallback);
        }
    }

    private bool HasOngoingRequests()
    {
        return _requestManager.RequestCount > 0;
    }

    public OperationResult HandleRequest<K, V>(IProperty sender, IKVProperty<K, V> receiver, PropertyRequestContent<K, V> requestContent, Action<long, PropertyResponseContent<V>> handler, out long requestId) where K : IRequestKey
    {
        requestId = 0;
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("A063B60F57EBA565");
            return OperationResult.OperateOnNonServerThread;
        }

        if (sender == receiver)
        {
            Logger.AssertNotReachHere("92922E47E55B3E4A");
            return OperationResult.RecursiveRequest;
        }
        if (_requestManager.RequestCount >= MAX_REQUEST_COUNT)
        {
            Logger.AssertNotReachHere("EAD7677C9D9E84BE");
            return OperationResult.ReachMaxRequests;
        }

        IKVPropertyContext<K, V> receiverContext = GetOrCreatePropertyContext(receiver);
        if (!receiverContext.TryGetLockResource(receiver, requestContent.Key, requestContent.Type, out LockResource? lockResource))
        {
            Logger.AssertNotReachHere("3C8498174D5FCAE5");
            return OperationResult.GetLockResourceFail;
        }
        if (!requestContent.Lock.TryAcquire(lockResource, out LockToken? lockToken))
        {
            Logger.AssertNotReachHere("0BE4547085684DD9");
            return OperationResult.AcquireLockFail;
        }
        if (requestContent.Lock.CanRelease)
        {
            requestContent.Lock.Release();
        }
        requestContent = requestContent.WithLock(lockToken);

        requestId = _nextRequestId++;
        PropertyRequest<K, V> request = new(requestId, sender, receiver, requestContent, handler);
        _requestManager.AddRequest(request);
        var sealedRequest = new SealedPropertyRequest<K, V>(requestId, requestContent);

        _delayedActions.Add((result) =>
        {
            receiverContext.AddNewRequest(sealedRequest);
            result.RearrangingProperties.Add(receiverContext);
        });
        return OperationResult.Successful;
    }

    public OperationResult HandleRespond<K, V>(IKVProperty<K, V> receiver, long requestId, PropertyResponseContent<V> responseContent) where K : IRequestKey
    {
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("CAEA4B19F1AF8BFE");
            return OperationResult.OperateOnNonServerThread;
        }

        PropertyRequest<K, V> request = GetRequest<K, V>(requestId);
        if (request.State == RequestState.Completed)
        {
            Logger.AssertNotReachHere("08E129B618734844");
            return OperationResult.InvalidArgument;
        }
        if (request.ResponseContent != null)
        {
            Logger.AssertNotReachHere("D1873C6A32900151");
            return OperationResult.InvalidArgument;
        }
        if (request.Receiver != receiver)
        {
            Logger.AssertNotReachHere("167FF26E226CE99B");
            return OperationResult.InvalidArgument;
        }
        request.RequestContent.Lock.Release();
        request.ResponseContent = responseContent;
        request.State = RequestState.Completed;
        _requestManager.RemoveRequest(requestId);

        _delayedActions.Add((result) =>
        {
            result.RearrangingProperties.Add(GetOrCreatePropertyContext(request.Sender));
            result.HandlerActions.Add(() =>
            {
                request.Handler(request.Id, responseContent);
            });
        });
        return OperationResult.Successful;
    }

    public OperationResult HandleRedirect<K, V>(IKVProperty<K, V> oldReceiver, long requestId, IKVProperty<K, V> newReceiver) where K : IRequestKey
    {
        if (!IsServerThread())
        {
            Logger.AssertNotReachHere("9D3D2E0E9978756F");
            return OperationResult.OperateOnNonServerThread;
        }

        PropertyRequest<K, V> request = GetRequest<K, V>(requestId);
        if (request.State == RequestState.Completed)
        {
            Logger.AssertNotReachHere("7D849855150D963C");
            return OperationResult.InvalidArgument;
        }
        if (request.ResponseContent != null)
        {
            Logger.AssertNotReachHere("4F239172DE93AAE6");
            return OperationResult.InvalidArgument;
        }
        if (request.Receiver != oldReceiver)
        {
            Logger.AssertNotReachHere("DB8810C2F9FD2A64");
            return OperationResult.InvalidArgument;
        }
        if (oldReceiver == newReceiver)
        {
            Logger.AssertNotReachHere("1FDE7E144FF60CA5");
            return OperationResult.RecursiveRequest;
        }
        _requestManager.RemoveRequest(requestId);
        request.Receiver = newReceiver;
        _requestManager.AddRequest(request);

        _delayedActions.Add((result) =>
        {
            IKVPropertyContext<K, V> receiverContext = GetOrCreatePropertyContext(newReceiver);
            receiverContext.AddNewRequest(new SealedPropertyRequest<K, V>(requestId, request.RequestContent));
            result.RearrangingProperties.Add(receiverContext);
        });
        return OperationResult.Successful;
    }

    public bool TryGetLockResource<K, V>(IKVProperty<K, V> property, K key, RequestType type, [MaybeNullWhen(false)] out LockResource resource) where K : IRequestKey
    {
        IKVPropertyContext<K, V> context = GetOrCreatePropertyContext(property);
        return context.TryGetLockResource(property, key, type, out resource);
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

    private IEPropertyContext<E> GetOrCreatePropertyContext<E>(IEProperty<E> property) where E : IPropertyExtension
    {
        return (IEPropertyContext<E>)GetOrCreatePropertyContext((IProperty)property); // must agree
    }

    private IKVPropertyContext<K, V> GetOrCreatePropertyContext<K, V>(IKVProperty<K, V> property) where K : IRequestKey
    {
        return (IKVPropertyContext<K, V>)GetOrCreatePropertyContext((IProperty)property); // must agree
    }

    private PropertyRequest<K, V> GetRequest<K, V>(long requestId) where K : IRequestKey
    {
        if (!_requestManager.TryGetRequest(requestId, out IPropertyRequest? request))
        {
            throw new InvalidOperationException("Request not found.");
        }
        return (PropertyRequest<K, V>)request; // must agree
    }

    private bool IsServerThread()
    {
        return _threadFlag.Value;
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

    private class ProcessCallback(InternalServer server, IPropertyContext context) : IProcessCallback
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
                OnCompletion(action);
            }
            else
            {
                server._processCallbackActions.Enqueue(() =>
                {
                    OnCompletion(action);
                });
                server.ScheduleServerRoutine("ProcessCallback");
            }
        }

        private void OnCompletion(Action? action)
        {
            server._processCallbacks.Remove(this);

            if (action is not null)
            {
                context.PostProcessRequests(action);
            }

            server._requestManager.EndProcess(context.Property, out IReadOnlyList<IPropertyRequest> cancellingRequests);
            foreach (IPropertyRequest request in cancellingRequests)
            {
                context.CancelRequest(request.Id);
            }
        }
    }

    private class ServerProperty(InternalServer server) : AbsProperty<VoidRequest, VoidType, VoidType, IPropertyExtension>
    {
        private readonly LinkedList<IExternalRequest> _requests = [];

        public override VoidType CreateModel()
        {
            return VoidType.Instance;
        }

        public override LockResource GetLockResource(VoidRequest key, LockType type)
        {
            throw new InvalidOperationException("Not supported.");
        }

        public override void RearrangeRequests(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context)
        {
            LockResource barrier = new();
            for (LinkedListNode<IExternalRequest>? node = _requests.First; node != null;)
            {
                LinkedListNode<IExternalRequest> nodeCopy = node;
                node = node.Next;

                IExternalRequest request = nodeCopy.Value;
                if (!request.TryGetLockResource(server, out LockResource? lockResource))
                {
                    _requests.Remove(nodeCopy);
                    request.SetResult(OperationResult.GetLockResourceFail, "");
                    continue;
                }
                if (barrier.Conflicts(lockResource))
                {
                    barrier.Merge(lockResource);
                    continue;
                }
                if (!server._lockManager.TryAcquireLock(lockResource, out LockToken? token))
                {
                    if (!server.HasOngoingRequests())
                    {
                        Logger.AssertNotReachHere("9575035B2AB12549");
                        _requests.Remove(nodeCopy);
                        request.SetResult(OperationResult.AcquireLockFail, "");
                        continue;
                    }
                    barrier.Merge(lockResource);
                    continue;
                }
                request.Request(context, token);
                _requests.Remove(nodeCopy);
            }
        }

        public override void ProcessRequests(PropertyContext<VoidRequest, VoidType, VoidType, IPropertyExtension> context, IProcessCallback callback)
        {
            throw new InvalidOperationException("Not supported.");
        }

        public void EnqueueRequest(IExternalRequest request)
        {
            _requests.AddLast(request);
        }
    }
}
