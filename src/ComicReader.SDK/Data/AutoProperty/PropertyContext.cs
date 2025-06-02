// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyContext<Q, R, M, E> : IQRPropertyContext<Q, R>, IEPropertyContext<E> where E : IPropertyExtension
{
    private readonly IServerContext _context;
    private readonly Dictionary<long, SealedPropertyRequest<Q>> _ongoingRequests = [];

    private readonly List<SealedPropertyRequest<Q>> _newRequests = [];
    public IReadOnlyList<SealedPropertyRequest<Q>> NewRequests => _newRequests;

    private readonly AbsProperty<Q, R, M, E> _property;
    IProperty IPropertyContext.Property => _property;

    private M _model;
    public M Model => _model;

    private readonly List<E> _extensions = [];
    public IReadOnlyList<E> Extensions => _extensions;

    public DependencyToken Dependency { get; }

    internal PropertyContext(IServerContext context, AbsProperty<Q, R, M, E> property, DependencyToken dependency)
    {
        _context = context;
        _property = property;
        _model = property.CreateModel();
        Dependency = dependency;
    }

    public SealedPropertyRequest<A>? Request<A, B>(IQRProperty<A, B> target, PropertyRequestContent<A> request, Action<PropertyContext<Q, R, M, E>, long, PropertyResponseContent<B>> handler)
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
        });
    }

    public void Respond(long requestId, PropertyResponseContent<R> response)
    {
        if (!_ongoingRequests.Remove(requestId, out SealedPropertyRequest<Q>? _))
        {
            Logger.AssertNotReachHere("14672E06AAB0669E");
            return;
        }
        _context.HandleRespond(_property, requestId, response);
    }

    public void Redirect(long requestId, IQRProperty<Q, R> target)
    {
        if (_context.HandleRedirect(_property, requestId, target))
        {
            _ongoingRequests.Remove(requestId);
        }
    }

    void IPropertyContext.ClearNewRequests()
    {
        _newRequests.Clear();
    }

    void IQRPropertyContext<Q, R>.AddNewRequest(SealedPropertyRequest<Q> request)
    {
        _ongoingRequests.Add(request.Id, request);
        _newRequests.Add(request);
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
        Respond(id, PropertyResponseContent<R>.NewFailedResponse());
    }

    void IEPropertyContext<E>.RegisterExtension(E extension)
    {
        _extensions.Add(extension);
    }

    private void ResetProperty()
    {
        List<SealedPropertyRequest<Q>> requests = [.. _ongoingRequests.Values];
        foreach (SealedPropertyRequest<Q> request in requests)
        {
            Respond(request.Id, PropertyResponseContent<R>.NewFailedResponse());
        }
        _model = _property.CreateModel();
        _newRequests.Clear();
    }
}
