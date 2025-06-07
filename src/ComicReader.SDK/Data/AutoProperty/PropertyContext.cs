// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyContext<K, V, M, E> : IKVPropertyContext<K, V>, IEPropertyContext<E> where K : IRequestKey where E : IPropertyExtension
{
    private readonly IServerContext _context;
    private readonly Dictionary<long, SealedPropertyRequest<K, V>> _ongoingRequests = [];

    private readonly List<SealedPropertyRequest<K, V>> _newRequests = [];
    public IReadOnlyList<SealedPropertyRequest<K, V>> NewRequests => _newRequests;

    private readonly AbsProperty<K, V, M, E> _property;
    IProperty IPropertyContext.Property => _property;

    private M _model;
    public M Model => _model;

    public ResponseTrackerManager<K> TrackerManager { get; } = new();

    private readonly List<E> _extensions = [];
    public IReadOnlyList<E> Extensions => _extensions;

    internal PropertyContext(IServerContext context, AbsProperty<K, V, M, E> property)
    {
        _context = context;
        _property = property;
        _model = property.CreateModel();
    }

    public OperationResult Request<A, B>(IKVProperty<A, B> target, PropertyRequestContent<A, B> request, Action<PropertyContext<K, V, M, E>, long, PropertyResponseContent<B>> handler, out long requestId) where A : IRequestKey
    {
        return _context.HandleRequest(_property, target, request, (p1, p2) =>
        {
            try
            {
                handler(this, p1, p2);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("E8FC3756E7055FA8", e);
                ResetProperty();
            }
        }, out requestId);
    }

    public OperationResult Respond(long requestId, PropertyResponseContent<V> response)
    {
        if (!_ongoingRequests.Remove(requestId, out SealedPropertyRequest<K, V>? _))
        {
            Logger.AssertNotReachHere("14672E06AAB0669E");
            return OperationResult.NoPermission;
        }
        return _context.HandleRespond(_property, requestId, response);
    }

    public OperationResult Redirect(long requestId, IKVProperty<K, V> target)
    {
        OperationResult result = _context.HandleRedirect(_property, requestId, target);
        if (result == OperationResult.Successful)
        {
            _ongoingRequests.Remove(requestId);
        }
        return result;
    }

    void IPropertyContext.ClearNewRequests()
    {
        _newRequests.Clear();
    }

    void IKVPropertyContext<K, V>.AddNewRequest(SealedPropertyRequest<K, V> request)
    {
        _ongoingRequests.Add(request.Id, request);
        _newRequests.Add(request);
    }

    bool IKVPropertyContext<K, V>.TryGetLockResource(IKVProperty<K, V> property, K key, RequestType type, [MaybeNullWhen(false)] out LockResource resource)
    {
        try
        {
            resource = type switch
            {
                RequestType.Read => property.GetLockResource(key, LockType.Read),
                RequestType.Modify => property.GetLockResource(key, LockType.Write),
                _ => property.GetLockResource(key, LockType.Write),
            };
            return true;
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("989E9B8FB5D10683", e);
            resource = null;
            return false;
        }
    }

    void IPropertyContext.RearrangeRequests()
    {
        try
        {
            _property.RearrangeRequests(this);
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("5C58129E65F52513", e);
            ResetProperty();
        }
    }

    void IPropertyContext.ProcessRequests(IProcessCallback callback)
    {
        try
        {
            _property.ProcessRequests(this, callback);
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("88927512645D06B0", e);
            ResetProperty();
            callback.PostCompletion(null);
        }
    }

    void IPropertyContext.PostProcessRequests(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("588E267CBF041BCD", e);
            ResetProperty();
        }
    }

    void IPropertyContext.CancelRequest(long id)
    {
        Respond(id, PropertyResponseContent<V>.NewResponse(OperationResult.UnhandledRequest));
    }

    void IEPropertyContext<E>.RegisterExtension(E extension)
    {
        _extensions.Add(extension);
    }

    private void ResetProperty()
    {
        List<SealedPropertyRequest<K, V>> requests = [.. _ongoingRequests.Values];
        foreach (SealedPropertyRequest<K, V> request in requests)
        {
            Respond(request.Id, PropertyResponseContent<V>.NewResponse(OperationResult.ExceptionInUserCode));
        }
        _model = _property.CreateModel();
        _newRequests.Clear();
    }
}
