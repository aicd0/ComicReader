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

    private readonly IRequestThrottleStrategy<IExternalRequest> _throttleStrategy = new ParallelReadThrottleStrategy<IExternalRequest>();
    private int _nextRequestId = 0;
    private readonly ServerProperty _serverProperty = new();
    private readonly RequestManager _requestManager = new();
    private readonly HashSet<ProcessCallback> _processCallbacks = [];
    private readonly Dictionary<IProperty, IPropertyContext> _propertyContextMap = [];
    private readonly Dictionary<IProperty, DependencyToken> _dependencyTokens = [];

    private readonly List<Action<DelayActionResult>> _delayedActions = [];

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
        sDispatcher.Submit($"{name}.{reason}", () =>
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
            EnqueueBatches();
            DequeueProcessCallbacks();
            DequeueBatches();

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
                if (string.IsNullOrEmpty(request.Key))
                {
                    request.SetFailedResult("Key is empty.");
                    continue;
                }
                if (request.Type == RequestType.Modify && request.IsNullValue())
                {
                    request.SetFailedResult("Value is null for modify request.");
                    continue;
                }
                _throttleStrategy.Enqueue(request);
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
        while (_throttleStrategy.TryDequeue(out IExternalRequest? request))
        {
            request.Request(this, _serverProperty, () =>
            {
                _throttleStrategy.OnRequestCompleted(request);
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
        IReadOnlyList<IProperty> properties = _requestManager.StartProcess();
        foreach (IProperty property in properties)
        {
            IPropertyContext propertyContext = GetOrCreatePropertyContext(property);
            ProcessCallback processCallback = new(this, propertyContext);
            _processCallbacks.Add(processCallback);
            propertyContext.ProcessRequests(processCallback);
        }
    }

    public SealedPropertyRequest<Q>? HandleRequest<Q, R>(IProperty sender, IQRProperty<Q, R> receiver, PropertyRequestContent<Q> requestContent, Action<long, PropertyResponseContent<R>> handler)
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
        if (_requestManager.RequestCount >= MAX_REQUEST_COUNT)
        {
            Logger.AssertNotReachHere("EAD7677C9D9E84BE");
            return null;
        }
        int requestId = _nextRequestId++;
        PropertyRequest<Q, R> request = new(requestId, sender, receiver, requestContent, handler);
        _requestManager.AddRequest(request);
        var sealedRequest = new SealedPropertyRequest<Q>(requestId, requestContent);

        _delayedActions.Add((result) =>
        {
            IQRPropertyContext<Q, R> receiverContext = GetOrCreatePropertyContext(receiver);
            receiverContext.AddNewRequest(sealedRequest);
            result.RearrangingProperties.Add(receiverContext);
        });
        return sealedRequest;
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
        _requestManager.RemoveRequest(requestId);

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
        _requestManager.RemoveRequest(requestId);
        request.Receiver = newReceiver;
        _requestManager.AddRequest(request);

        _delayedActions.Add((result) =>
        {
            IQRPropertyContext<Q, R> receiverContext = GetOrCreatePropertyContext(newReceiver);
            receiverContext.AddNewRequest(new SealedPropertyRequest<Q>(requestId, request.RequestContent));
            result.RearrangingProperties.Add(receiverContext);
        });
        return true;
    }

    private IPropertyContext GetOrCreatePropertyContext(IProperty property)
    {
        if (!_propertyContextMap.TryGetValue(property, out IPropertyContext? context))
        {
            DependencyToken token = GetDependencyToken(property);
            context = property.CreatePropertyContext(this, token);
            _propertyContextMap.Add(property, context);
        }
        return context;
    }

    private IEPropertyContext<E> GetOrCreatePropertyContext<E>(IEProperty<E> property) where E : IPropertyExtension
    {
        return (IEPropertyContext<E>)GetOrCreatePropertyContext((IProperty)property); // must agree
    }

    private IQRPropertyContext<Q, R> GetOrCreatePropertyContext<Q, R>(IQRProperty<Q, R> property)
    {
        return (IQRPropertyContext<Q, R>)GetOrCreatePropertyContext((IProperty)property); // must agree
    }

    private DependencyToken GetDependencyToken(IProperty property)
    {
        {
            if (_dependencyTokens.TryGetValue(property, out DependencyToken? token))
            {
                return token;
            }
        }

        Dictionary<IProperty, HashSet<IProperty>> allChildren = [];

        {
            Dictionary<IProperty, List<IProperty>> newChildrenMap = [];

            {
                List<IProperty> searchList = [property];
                List<IProperty> nextSearchList = [];
                while (searchList.Count > 0)
                {
                    foreach (IProperty p in searchList)
                    {
                        List<IProperty> children = p.GetDependentProperties();
                        HashSet<IProperty> childrenSet = [.. children];
                        allChildren[p] = childrenSet;
                        List<IProperty> newChildren = [];
                        foreach (IProperty child in childrenSet)
                        {
                            if (!_dependencyTokens.ContainsKey(child))
                            {
                                newChildren.Add(child);
                            }
                        }
                        if (newChildren.Count > 0)
                        {
                            newChildrenMap[p] = newChildren;
                            foreach (IProperty child in newChildren)
                            {
                                if (!newChildrenMap.ContainsKey(child))
                                {
                                    nextSearchList.Add(child);
                                }
                            }
                        }
                    }
                    (searchList, nextSearchList) = (nextSearchList, searchList);
                    nextSearchList.Clear();
                }
            }

            Dictionary<IProperty, List<IProperty>> nextNewChildrenMap = [];
            while (newChildrenMap.Count > 0)
            {
                foreach (KeyValuePair<IProperty, List<IProperty>> pair in newChildrenMap)
                {
                    IProperty p = pair.Key;
                    HashSet<IProperty> children = allChildren[p];
                    List<IProperty> newChildren = [];
                    foreach (IProperty newChild in pair.Value)
                    {
                        if (newChild == p)
                        {
                            continue;
                        }
                        foreach (IProperty childOfNewChild in allChildren[newChild])
                        {
                            if (children.Add(childOfNewChild) && !_dependencyTokens.ContainsKey(childOfNewChild))
                            {
                                newChildren.Add(childOfNewChild);
                            }
                        }
                    }
                    if (newChildren.Count > 0)
                    {
                        nextNewChildrenMap[p] = newChildren;
                    }
                }
                (nextNewChildrenMap, newChildrenMap) = (newChildrenMap, nextNewChildrenMap);
                nextNewChildrenMap.Clear();
            }
        }

        Dictionary<IProperty, DependencyToken> tokenMap = [];
        foreach (IProperty p in allChildren.Keys)
        {
            tokenMap[p] = new();
        }
        foreach (KeyValuePair<IProperty, HashSet<IProperty>> pair in allChildren)
        {
            HashSet<DependencyToken> tokens = [];
            foreach (IProperty p in pair.Value)
            {
                if (tokenMap.TryGetValue(p, out DependencyToken? subToken))
                {
                    tokens.Add(subToken);
                }
                else
                {
                    subToken = _dependencyTokens[p];
                    tokens.Add(subToken);
                    foreach (DependencyToken subSubToken in subToken.GetTokens())
                    {
                        tokens.Add(subSubToken);
                    }
                }
            }
            DependencyToken token = tokenMap[pair.Key];
            token.SetTokens([.. tokens]);
            _dependencyTokens[pair.Key] = token;
        }
        return tokenMap[property];
    }

    private PropertyRequest<Q, R> GetRequest<Q, R>(long requestId)
    {
        if (!_requestManager.TryGetRequest(requestId, out IPropertyRequest? request))
        {
            throw new InvalidOperationException("Request not found.");
        }
        return (PropertyRequest<Q, R>)request; // must agree
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

    private class ServerProperty : AbsProperty<VoidType, VoidType, VoidType, IPropertyExtension>
    {
        public override VoidType CreateModel()
        {
            return VoidType.Instance;
        }

        public override List<IProperty> GetDependentProperties()
        {
            return [];
        }

        public override void RearrangeRequests(PropertyContext<VoidType, VoidType, VoidType, IPropertyExtension> context)
        {
        }

        public override void ProcessRequests(PropertyContext<VoidType, VoidType, VoidType, IPropertyExtension> context, IProcessCallback callback)
        {
            throw new InvalidOperationException("Not supported.");
        }
    }
}
